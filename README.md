# Conexão Solidária

[![CI](https://github.com/carolbabiasi/conexao-solidaria/actions/workflows/ci.yml/badge.svg)](https://github.com/carolbabiasi/conexao-solidaria/actions/workflows/ci.yml)

Plataforma digital da ONG Esperança Solidária — MVP do Hackathon 11NETT.

Uma doação entra pela API, que **não** atualiza o valor arrecadado. A intenção é
gravada e publicada como evento na mesma transação; um Worker idempotente consome,
registra no ledger do MongoDB e incrementa o total da campanha no PostgreSQL com um
`UPDATE` atômico. O painel público reflete o valor em segundos.

## Índice

- [Arquitetura](#arquitetura)
  - [Os três problemas difíceis](#os-três-problemas-difíceis)
  - [Por que dois bancos](#por-que-dois-bancos)
  - [Estrutura da solução](#estrutura-da-solução)
- [Pré-requisitos](#pré-requisitos)
- [Caminho A — Docker Compose (mais rápido)](#caminho-a--docker-compose-mais-rápido)
- [Caminho B — Kubernetes (ambiente completo)](#caminho-b--kubernetes-ambiente-completo)
- [Credenciais](#credenciais)
- [Fluxo de teste ponta a ponta](#fluxo-de-teste-ponta-a-ponta)
- [Endpoints](#endpoints)
- [Observabilidade](#observabilidade)
- [Rodando os testes](#rodando-os-testes)
- [Troubleshooting](#troubleshooting)
- [Documentação](#documentação)

## Arquitetura

![Arquitetura da Conexão Solidária: os clientes chamam a API, que grava a doação e a mensagem na mesma transação do PostgreSQL; o outbox entrega o evento ao RabbitMQ, o Worker consome, registra no ledger do MongoDB com índice único e incrementa o valor arrecadado com UPDATE atômico; Prometheus e Grafana coletam as métricas](docs/diagramas/arquitetura.png)

> Fonte versionada em [`docs/diagramas/arquitetura.svg`](docs/diagramas/arquitetura.svg).
> Editou o SVG? Reexporte o PNG com `.\scripts\exportar-diagramas.ps1`.

A frase que define o projeto inteiro está no edital:

> "Ao receber uma nova doação, a API **NÃO** deve atualizar o valor arrecadado da
> campanha diretamente no banco de dados."

Ou seja: a API é *write-only* na intenção de doação e responde `202 Accepted`. O
Worker é o **único dono da escrita** do valor agregado. Todo o resto do desenho
deriva disso.

### Os três problemas difíceis

Processamento assíncrono de dinheiro tem três modos de falha que não aparecem em
teste manual. Cada um tem uma mitigação explícita no código:

| Problema | O que aconteceria | Mitigação |
|---|---|---|
| **Dupla escrita** | A API grava a doação e publica o evento. Se o publish falha depois do commit, a doação existe e nunca é processada — o valor nunca sobe. | **Transactional Outbox** (MassTransit + EF Core). Doação e mensagem entram na mesma transação; o bus entrega depois. [`ConfiguracaoDeMensageria.cs`](src/GestorONG.Infrastructure/Mensageria/ConfiguracaoDeMensageria.cs) |
| **Redelivery infla o total** | RabbitMQ é *at-least-once*. Se o Worker processa, incrementa e o ACK se perde, a mensagem volta e soma duas vezes. | Índice único por `IdDoacao` no ledger do Mongo. O insert falhando com `11000` significa "já processei" — descarta sem incrementar. [`DoacaoLedgerRepository.cs`](src/GestorONG.Infrastructure/Persistencia/Mongo/DoacaoLedgerRepository.cs) |
| **Lost update** | Dois pods do Worker lendo `ValorArrecadado`, somando em memória e gravando: uma das doações desaparece. | `ExecuteUpdateAsync` gera `SET valor_arrecadado = valor_arrecadado + @valor`. Nunca read-modify-write. [`CampanhaRepository.cs`](src/GestorONG.Infrastructure/Persistencia/Repositorios/CampanhaRepository.cs#L29) |

É por isso que o Worker roda com **2 réplicas** no Kubernetes: com uma só, os dois
últimos problemas nunca são exercitados. A verificação é reprodutível — 30 doações
simultâneas de R$ 10,00 fecham em exatamente R$ 300,00.

O consumer também tem retry exponencial (3 tentativas, de 1s a 15s) para falhas
transitórias de banco.

### Por que dois bancos

| Banco | Guarda | Por quê |
|---|---|---|
| **PostgreSQL** | Usuários, campanhas, doações e o outbox | Integridade forte. Unicidade de e-mail e CPF, e o incremento atômico do valor arrecadado. ACID não é negociável em dinheiro. |
| **MongoDB** | Ledger append-only de doações processadas | Escrita alta, sem joins, schema flexível. É lido só para auditoria e reconciliação — e é o que dá idempotência ao Worker. |

O `valor_arrecadado` em `campanhas` é uma coluna materializada, escrita **somente**
pelo Worker. O ledger do Mongo permite reconciliar esse total a qualquer momento.
A decisão de não transitar a campanha para `Concluída` automaticamente ao atingir a
meta está registrada em [ADR 0001](docs/adr/0001-transicao-ao-atingir-a-meta.md).

### Estrutura da solução

```
src/
├── GestorONG.API/            # Auth, campanhas, doações, painel público
├── GestorONG.Worker/         # Consumer do DoacaoRecebidaEvent
├── GestorONG.Domain/         # Entidades, VOs (Cpf, Email), regras. Zero dependências.
├── GestorONG.Application/    # Abstrações de repositório, exceções de aplicação
├── GestorONG.Infrastructure/ # EF Core/Npgsql, Mongo, BCrypt, JWT, MassTransit, métricas
└── GestorONG.Contracts/      # Eventos compartilhados API ↔ Worker
tests/
└── GestorONG.Domain.Tests/   # xUnit, roda no CI sem subir infraestrutura
k8s/                          # 26 recursos + deploy.ps1 e teardown.ps1
docker/                       # docker-compose da infraestrutura local
docs/                         # ADRs e backlog técnico
```

`Domain` não referencia nada — é o que permite rodar os testes de unidade na esteira
de CI sem banco, broker ou container.

## Pré-requisitos

| Ferramenta | Versão mínima | Necessária em |
|---|---|---|
| .NET SDK | 10.0 | A e B |
| Docker Desktop | 4.30 | A e B |
| kubectl | 1.30 | B |
| Cluster local (kind 0.23 ou Kubernetes do Docker Desktop) | — | B |
| PowerShell | 7.0 (ou 5.1 do Windows) | B |
| `jq` | qualquer | só para o [fluxo de teste](#fluxo-de-teste-ponta-a-ponta) |

Confira o que está instalado:

```bash
dotnet --version && docker --version && kubectl version --client
```

O `kubectl` só é usado no Caminho B. O Caminho A precisa apenas do .NET SDK e do Docker.

## Caminho A — Docker Compose (mais rápido)

O Compose sobe **só a infraestrutura** (PostgreSQL, MongoDB, RabbitMQ, Prometheus e
Grafana). A API e o Worker rodam na sua máquina com `dotnet run`. É o caminho mais
rápido para validar a aplicação e depurar.

### 1. Suba a infraestrutura

```bash
cd docker
cp .env.example .env
docker compose up -d
```

Espere todos ficarem `healthy` — leva cerca de 40 segundos por causa do RabbitMQ:

```bash
docker compose ps
```

### 2. Compile uma vez

```bash
dotnet build GestorONG.slnx
```

Não é opcional: API e Worker compartilham o projeto `Infrastructure`, e dois
`dotnet run` simultâneos disputam o mesmo `.dll`
(ver [Troubleshooting](#error-cs2012-ao-subir-api-e-worker-ao-mesmo-tempo)).

### 3. Rode a API

```bash
dotnet run --project src/GestorONG.API
```

A API aplica as migrations e cria o gestor semeado na primeira execução. Quando
aparecer `Now listening on: http://localhost:5231`, abra o Swagger:

**http://localhost:5231/swagger**

### 4. Rode o Worker

Em outro terminal:

```bash
dotnet run --project src/GestorONG.Worker -- --urls http://localhost:5002
```

A porta explícita evita conflito com a API. Sem o Worker, as doações são aceitas
com `202` mas o valor arrecadado nunca sobe — é exatamente o que o processamento
assíncrono faz.

### O que o Caminho A não entrega

O dashboard do Grafana **não aparece** aqui. No Compose, o Prometheus está com os
alvos da API e do Worker comentados em
[`docker/prometheus/prometheus.yml`](docker/prometheus/prometheus.yml), e o Grafana
sobe sem datasource nem dashboard provisionados. A observabilidade completa é o
Caminho B.

### Encerrando

```bash
cd docker
docker compose down      # para tudo, mantém os dados
docker compose down -v   # para tudo e APAGA os volumes
```

## Caminho B — Kubernetes (ambiente completo)

Aqui sobe tudo: bancos, broker, API e Worker com 2 réplicas cada, Prometheus
coletando métricas dos pods e do cAdvisor, e Grafana com o dashboard já
provisionado. É o ambiente que o edital cobra.

### 1. Crie o cluster

Com kind:

```bash
kind create cluster --name conexao-solidaria
```

Ou habilite o Kubernetes no Docker Desktop (Settings → Kubernetes → Enable).

### 2. Suba tudo com um comando

```powershell
.\k8s\deploy.ps1
```

O script faz, na ordem: verifica os pré-requisitos, constrói as imagens da API e
do Worker, carrega-as no cluster kind, aplica namespace, ConfigMap e Secret, sobe
as dependências e **espera cada camada ficar pronta** antes de seguir, provisiona
Prometheus e Grafana, e por último sobe a aplicação.

Leva de 3 a 6 minutos na primeira execução, quase tudo em build de imagem. No
final ele imprime os 9 pods e os comandos de acesso.

Opções:

```powershell
.\k8s\deploy.ps1 -PularBuild           # reusa as imagens já no cluster
.\k8s\deploy.ps1 -Senha "outra-senha"  # troca a senha de toda a stack
```

### 3. Abra as interfaces

Os Services são `ClusterIP`, então o acesso é por `port-forward`. Um terminal para
cada um dos que você for usar:

```bash
kubectl port-forward -n conexao-solidaria svc/gestorong-api 8080:80
kubectl port-forward -n conexao-solidaria svc/rabbitmq 15672:15672
kubectl port-forward -n conexao-solidaria svc/grafana 3000:3000
kubectl port-forward -n conexao-solidaria svc/prometheus 9090:9090
```

| Interface | URL |
|---|---|
| Swagger | http://localhost:8080/swagger |
| Painel público | http://localhost:8080/api/v1/publico/campanhas |
| RabbitMQ Management | http://localhost:15672 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |

### Sobre o Secret e a chave JWT

Não existe Secret versionado no repositório. O
[`k8s/base/02-secret.example.yaml`](k8s/base/02-secret.example.yaml) só tem
placeholders, para servir de referência do formato.

O `deploy.ps1` cria o Secret real com `kubectl create secret --from-literal` e
**gera a chave JWT aleatoriamente a cada execução** (48 caracteres). Você não
precisa fazer nada — mas se quiser criar o Secret à mão, copie o exemplo, troque
os valores e aplique antes de subir a aplicação:

```bash
cp k8s/base/02-secret.example.yaml k8s/base/02-secret.yaml
# edite os valores, a chave JWT precisa de no mínimo 32 caracteres
kubectl apply -f k8s/base/02-secret.yaml
```

> `k8s/base/02-secret.yaml` (sem o `.example`) está no `.gitignore`. Nunca versione um Secret real.

### Encerrando

```powershell
.\k8s\teardown.ps1                # remove o namespace inteiro, incluindo os dados
.\k8s\teardown.ps1 -ManterDados   # remove os workloads, preserva os PVCs
```

## Credenciais

Todas de desenvolvimento local. Em produção viriam de um Secret gerenciado.

### Gestor semeado

Criado automaticamente na primeira subida da API. É o único usuário com perfil
`GestorONG` — só ele cria campanhas.

| | |
|---|---|
| E-mail | `gestor@esperancasolidaria.org` |
| Senha | `devlocal123` |

No Caminho B a senha é a que você passou em `-Senha`, e `devlocal123` é o padrão.
Os valores ficam em
[`appsettings.Development.json`](src/GestorONG.API/appsettings.Development.json)
(Caminho A) e no ConfigMap + Secret (Caminho B).

Novos usuários se cadastram em `POST /api/v1/auth/registrar` e sempre nascem com
perfil `Doador` — não há endpoint para criar outro gestor.

### Infraestrutura

| Serviço | Usuário | Senha |
|---|---|---|
| PostgreSQL | `gestorong` | `devlocal123` |
| MongoDB | `gestorong` | `devlocal123` |
| RabbitMQ | `gestorong` | `devlocal123` |
| Grafana | `admin` | `devlocal123` |

No Caminho A vêm de `docker/.env` (que não é versionado — você o cria a partir do
`.env.example`). No Caminho B, do Secret gerado pelo `deploy.ps1`.

## Fluxo de teste ponta a ponta

Seis comandos, do cadastro ao painel público. Ajuste o `BASE` conforme o caminho
que você escolheu:

```bash
BASE=http://localhost:5231     # Caminho A
# BASE=http://localhost:8080   # Caminho B
```

**1. Cadastre um doador**

```bash
curl -s -X POST $BASE/api/v1/auth/registrar -H 'Content-Type: application/json' -d '{"nomeCompleto":"Maria Doadora","email":"maria@exemplo.com","cpf":"123.456.789-09","senha":"senha12345"}'
```

O CPF passa por validação de dígito verificador, então não serve qualquer número.
Se você já rodou este fluxo antes, troque o e-mail **e** o CPF — os dois têm índice
único (ver [Troubleshooting](#registrar-devolve-500)).

**2. Autentique o gestor**

```bash
TOKEN_GESTOR=$(curl -s -X POST $BASE/api/v1/auth/login -H 'Content-Type: application/json' -d '{"email":"gestor@esperancasolidaria.org","senha":"devlocal123"}' | jq -r .accessToken)
```

**3. Crie uma campanha**

```bash
ID_CAMPANHA=$(curl -s -X POST $BASE/api/v1/campanhas -H "Authorization: Bearer $TOKEN_GESTOR" -H 'Content-Type: application/json' -d '{"titulo":"Cestas basicas de inverno","descricao":"Arrecadacao para 200 familias.","dataInicio":"2026-01-01T00:00:00Z","dataFim":"2026-12-31T23:59:59Z","metaFinanceira":10000.00}' | jq -r .id)
```

**4. Autentique a doadora**

```bash
TOKEN_DOADOR=$(curl -s -X POST $BASE/api/v1/auth/login -H 'Content-Type: application/json' -d '{"email":"maria@exemplo.com","senha":"senha12345"}' | jq -r .accessToken)
```

**5. Doe**

```bash
curl -s -X POST $BASE/api/v1/doacoes -H "Authorization: Bearer $TOKEN_DOADOR" -H 'Content-Type: application/json' -d "{\"idCampanha\":\"$ID_CAMPANHA\",\"valorDoacao\":125.50}"
```

Responde `202 Accepted` — a doação foi aceita, não processada. O Worker consome o
evento em seguida.

**6. Confira o painel público**

```bash
curl -s $BASE/api/v1/publico/campanhas | jq
```

O `valorArrecadado` sai de `0` para `125.50`. Se continuar em zero, o Worker não
está rodando.

> **No PowerShell**, aspas simples não delimitam JSON do jeito esperado. Use o
> Swagger em `/swagger` — mesmo fluxo, sem briga de escaping: faça o login, copie
> o `accessToken`, clique em **Authorize** e cole o token (sem o prefixo `Bearer`).

### Provando a idempotência

Repita o passo 5 várias vezes em paralelo, com o mesmo valor. O total no painel
fecha exatamente com a soma — nunca a mais, nunca a menos — e o painel
*Redeliveries descartadas* do Grafana mostra as duplicatas que o consumer barrou.
É a demonstração prática das mitigações da
[seção de arquitetura](#os-três-problemas-difíceis).

## Endpoints

| Método | Rota | Acesso |
|---|---|---|
| `POST` | `/api/v1/auth/registrar` | público |
| `POST` | `/api/v1/auth/login` | público |
| `GET` | `/api/v1/publico/campanhas` | público |
| `POST` | `/api/v1/campanhas` | GestorONG |
| `GET` | `/api/v1/campanhas` | GestorONG |
| `GET` | `/api/v1/campanhas/{id}` | GestorONG |
| `POST` | `/api/v1/doacoes` | Doador |
| `GET` | `/health/live` | público |
| `GET` | `/health/ready` | público |
| `GET` | `/metrics` | público |

`/health/live` não consulta dependência alguma — é o que a liveness probe usa.
`/health/ready` verifica PostgreSQL, MongoDB e RabbitMQ, e é o que tira o pod do
balanceamento sem matá-lo.

## Observabilidade

Além das métricas de runtime e de GC, a aplicação expõe cinco métricas de negócio
em `/metrics`:

| Métrica | O que mede |
|---|---|
| `doacoes_recebidas_total` | Intenções aceitas pela API |
| `doacoes_processadas_total` | Doações efetivamente somadas pelo Worker |
| `doacoes_duplicadas_descartadas_total` | Redeliveries barradas pela idempotência |
| `doacoes_valor_total` | Soma dos valores doados, em BRL |
| `doacao_processamento_duracao_seconds` | Histograma da duração no Worker |

A diferença entre `recebidas` e `processadas` é o lag da fila em tempo real.

No Caminho B o dashboard **Conexao Solidaria** já vem provisionado no Grafana, com
10 painéis: doações recebidas e processadas, lag da fila, redeliveries descartadas,
requisições HTTP/s, latência p95, CPU e memória por pod, taxa de processamento e
duração p95 no Worker. CPU e memória vêm do cAdvisor via kubelet.

## Rodando os testes

```bash
dotnet test GestorONG.slnx
```

Compilação limpa, com `TreatWarningsAsErrors`:

```bash
dotnet build GestorONG.slnx -c Release
```

## Troubleshooting

### Porta já em uso

`Failed to bind to address http://localhost:5231` significa que outra instância da
API está no ar. Encontre e encerre:

```bash
# Windows
netstat -ano | findstr :5231
# Linux/macOS
lsof -i :5231
```

As portas do Compose são configuráveis no `docker/.env` (`POSTGRES_PORT`,
`MONGO_PORT`, `RABBITMQ_PORT`, `GRAFANA_PORT`).

### `error CS2012` ao subir API e Worker ao mesmo tempo

```
CSC : error CS2012: Não foi possível abrir "...GestorONG.Infrastructure.dll" para escrever
```

Os dois projetos compilam o `GestorONG.Infrastructure`, e dois `dotnet run`
simultâneos disputam o mesmo arquivo. Compile uma vez antes de subir qualquer um
dos dois:

```bash
dotnet build GestorONG.slnx
```

Depois `dotnet run` em cada terminal, sem corrida de build.

### `registrar` devolve 500

Acontece quando o **CPF** já está cadastrado. E-mail duplicado é tratado e
responde `409 Conflict`, mas o CPF tem índice único no banco e a violação sobe
como erro não tratado — é uma lacuna conhecida da DATA-02.

Use outro CPF válido, ou limpe a base:

```bash
cd docker && docker compose down -v && docker compose up -d
```

### A API sobe e morre com erro de conexão

Ela aplica migrations na partida, então precisa do PostgreSQL pronto **antes**.
Rode `docker compose ps` e confirme que todos estão `healthy`, não apenas `Up`.

### Pod em `CrashLoopBackOff`

```bash
kubectl get pods -n conexao-solidaria
kubectl logs -n conexao-solidaria <pod> --previous
kubectl describe pod -n conexao-solidaria <pod>
```

O `--previous` é o que importa: mostra o log da instância que morreu. Em
`describe`, olhe os *Events* no final — é onde aparece probe falhando ou
`ImagePullBackOff`.

### `ImagePullBackOff` no kind

As imagens são locais, com `imagePullPolicy: IfNotPresent`. Se não foram
carregadas no cluster, o pod tenta buscar no GHCR. Rode `.\k8s\deploy.ps1` sem
`-PularBuild`, ou carregue à mão:

```bash
kind load docker-image ghcr.io/carolbabiasi/conexao-solidaria-api:latest --name conexao-solidaria
```

### Os bancos não ficam prontos no tempo esperado

Em cluster local, PostgreSQL e MongoDB podem levar mais de 2 minutos na primeira
subida, provisionando os PVCs. O script avisa e continua. Acompanhe:

```bash
kubectl get pods -n conexao-solidaria -w
```

### Doação aceita mas o valor arrecadado não sobe

O Worker não está consumindo. No Caminho A, confirme que ele está rodando; no
Caminho B:

```bash
kubectl logs -n conexao-solidaria -l app=gestorong-worker --tail=50
```

Verifique também a fila `DoacaoRecebidaEvent` na Management UI do RabbitMQ: se as
mensagens acumulam em *Ready*, nada está consumindo.

### Painéis do Grafana vazios

Abra o Prometheus em http://localhost:9090/targets. Os alvos `gestorong-pods`
(4/4) e `kubelet-cadvisor` (1/1) precisam estar `UP`. CPU e memória por pod vêm do
cAdvisor — se ele estiver em `403`, falta a permissão `nodes/proxy` no ClusterRole.

### Recomeçar do zero

```powershell
.\k8s\teardown.ps1
.\k8s\deploy.ps1
```

## Documentação

- [Diagrama de arquitetura](docs/diagramas/arquitetura.svg) — fonte SVG; o PNG ao lado é o que vai para o relatório e os slides
- [Análise técnica e backlog](docs/BACKLOG.md) — riscos mapeados, decisões travadas e as 79 tarefas
- [ADRs](docs/adr) — decisões arquiteturais registradas
- [Issues](https://github.com/carolbabiasi/conexao-solidaria/issues) organizadas por épico e fase
