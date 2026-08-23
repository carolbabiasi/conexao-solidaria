# Conexão Solidária — Análise Técnica e Backlog

> Hackathon 11NETT — MVP da plataforma da ONG Esperança Solidária.
> Documento de planejamento. Fonte de verdade para abertura das issues.
>
> As 79 tarefas deste backlog estão em `docs/issues/issues.json`, prontas para virar issues do GitHub via `scripts/create-github-issues.ps1`.

---

## 1. Leitura crítica do edital

O enunciado é curto nos requisitos funcionais e pesado nos requisitos de arquitetura. Isso define a estratégia: **o domínio é simples de propósito** (5 casos de uso), e a nota está na plumbing — mensageria, Kubernetes, observabilidade e CI. Gastar tempo demais em regra de negócio é o erro clássico aqui.

Frase que define o projeto inteiro:

> "Ao receber uma nova doação, a API **NÃO** deve atualizar o valor arrecadado da campanha diretamente no banco de dados."

Ou seja: a API é *write-only* na intenção de doação, o Worker é o **único dono da escrita** do valor agregado. Tudo no design deriva disso.

### 1.1 Armadilhas que precisam de decisão explícita

Estas não estão escritas no edital, mas são exatamente onde a banca vai furar a demo:

| # | Risco | Por quê | Mitigação |
|---|---|---|---|
| R1 | **Dupla escrita** entre Postgres e RabbitMQ | A API grava a doação e publica o evento. Se o publish falhar depois do commit, a doação existe e nunca é processada — o valor nunca sobe. | **Transactional Outbox** (MassTransit + EF Core). Grava doação e mensagem na mesma transação; o bus entrega depois. |
| R2 | **Redelivery infla o valor arrecadado** | RabbitMQ é *at-least-once*. Se o Worker processa, incrementa e o ACK se perde, a mensagem volta e soma duas vezes. | Idempotência por `IdDoacao` — tabela/coleção de doações processadas com índice único. Se já existe, descarta. |
| R3 | **Race condition no incremento** | Dois pods do Worker lendo `ValorArrecadado`, somando em memória e gravando = *lost update*. | `UPDATE ... SET valor_arrecadado = valor_arrecadado + @valor` atômico no banco. Nunca read-modify-write. |
| R4 | **Ambiguidade "valor calculado"** | O edital diz que o painel exibe o valor "calculado com base nas doações processadas", mas também que o Worker atualiza o valor. | Coluna materializada `ValorArrecadado` em `Campanha`, escrita **só** pelo Worker. O Mongo guarda o ledger de doações processadas para auditoria e reconciliação. Documentar como ADR. |
| R5 | **Fuso horário na regra de data** | "Não pode ser criada com data de término no passado" — comparar `DateTime.Now` local dentro de um container UTC gera bug fantasma. | `DateTimeOffset` em todo lugar, `timestamptz` no Postgres, comparar sempre contra `TimeProvider.System.GetUtcNow()` (injetável = testável). |
| R6 | **E-mail único só na aplicação** | Dois cadastros simultâneos passam pela checagem e ambos inserem. | Índice único no Postgres + tratar violação como 409. |
| R7 | **Claim de role não bate no `[Authorize]`** | O `JwtBearer` mapeia claims; `role` vs `ClaimTypes.Role` derruba a autorização silenciosamente com 403. | Fixar `RoleClaimType` explicitamente no `TokenValidationParameters` e cobrir com teste de integração. |
| R8 | **`UseHttpsRedirection` dentro do pod** | Sem certificado no container, o health check da liveness probe toma 307 e o pod entra em CrashLoop. | Aplicar redirect só fora de container / terminar TLS no Ingress. |
| R9 | **CPF é dado pessoal (LGPD)** | Vai parar em log estruturado sem querer. | Normalizar (só dígitos), validar dígito verificador, e redigir em logs. |
| R10 | **Zabbix vs Prometheus** | O título do requisito cita Zabbix, mas o texto obrigatório é só "expor `/health` ou `/metrics`" + 1 dashboard Grafana com métrica real. | Caminho de menor risco: `/metrics` no formato Prometheus → Prometheus → Grafana. Zabbix entra como épico opcional. |

### 1.2 Estado atual do repositório

Praticamente zero. Existe `GestorONG.API/GestorONG.API/` — template Web API .NET 10, `Controllers/` vazio, único pacote é `Microsoft.AspNetCore.OpenApi`, `Program.cs` intocado. A pasta `.vs/` e os `obj/`/`bin/` estão versionáveis por engano (não há `.gitignore`) e o repositório ainda não é um repo Git.

Consequência prática: **INFRA-01 a INFRA-03 são bloqueantes de tudo.**

---

## 2. Arquitetura alvo

```
                          ┌─────────────────┐
   Público ──────────────▶│                 │
   Doador (JWT) ─────────▶│  GestorONG.API  │────▶ PostgreSQL
   GestorONG (JWT) ──────▶│                 │      (Usuarios, Campanhas,
                          └────────┬────────┘       Doacoes, Outbox)
                                   │
                       DoacaoRecebidaEvent
                                   │
                          ┌────────▼────────┐
                          │    RabbitMQ     │
                          └────────┬────────┘
                                   │
                          ┌────────▼─────────┐
                          │ GestorONG.Worker │──▶ MongoDB (ledger de doações
                          │   (consumer)     │        processadas)
                          └────────┬─────────┘
                                   │  UPDATE atômico
                                   └──────────────▶ PostgreSQL (ValorArrecadado)

   /metrics ──▶ Prometheus ──▶ Grafana          Tudo em Kubernetes
```

### 2.1 Decisões travadas

| Decisão | Escolha | Justificativa (vai para o PDF do entregável 2) |
|---|---|---|
| Broker | **RabbitMQ + MassTransit** | Management UI pronta para o vídeo de demo (o edital exige mostrar a mensagem na fila). MassTransit entrega retry, DLQ e outbox transacional sem código de plumbing. |
| Banco relacional | **PostgreSQL** | Usuários e Campanhas exigem integridade forte, unicidade de e-mail e o incremento atômico do valor arrecadado. ACID não é negociável no dinheiro. |
| Banco documental | **MongoDB** | Ledger de doações processadas: escrita alta, append-only, schema flexível, sem joins. Cresce linearmente e é lido só para auditoria. |
| Serviços | **2** (`API` + `Worker`) | Atende o mínimo obrigatório sem multiplicar custo de infra, K8s e CI. Gateway fica como bônus. |
| Estilo | Clean Architecture enxuta | `Domain` puro é o que permite testes de unidade rodando na esteira de CI (requisito bônus) sem subir infra. |

### 2.2 Estrutura de solução proposta

```
GestorONG/
├── src/
│   ├── GestorONG.API/              # Web API: auth, campanhas, doações, painel público
│   ├── GestorONG.Worker/           # Consumer do DoacaoRecebidaEvent
│   ├── GestorONG.Domain/           # Entidades, VOs (CPF, Email), regras. Zero dependências.
│   ├── GestorONG.Application/      # Casos de uso, DTOs, interfaces de repositório
│   ├── GestorONG.Infrastructure/   # EF Core/Postgres, Mongo, BCrypt, JWT, MassTransit
│   └── GestorONG.Contracts/        # Eventos compartilhados API ↔ Worker
├── tests/
│   ├── GestorONG.Domain.Tests/     # xUnit — roda no CI
│   └── GestorONG.IntegrationTests/ # Testcontainers (opcional)
├── k8s/                            # Deployments, Services, ConfigMaps, Secrets, HPA
├── docker/                         # docker-compose para dev local
├── docs/                           # ADRs, diagrama, justificativa dos bancos
└── .github/workflows/              # CI
```

> A estrutura atual (`GestorONG.API/GestorONG.API/`) deve ser movida para `src/GestorONG.API/`. É a primeira tarefa.

---

## 3. Backlog

Legenda de tamanho: **P** ≈ até 2h · **M** ≈ meio dia · **G** ≈ 1 dia · **GG** ≈ 2+ dias.
Prioridade: 🔴 bloqueante · 🟠 obrigatório · 🟡 entregável · 🟢 bônus.

---

### ÉPICO 0 — Fundação do repositório  🔴

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **INFRA-01** | `git init`, `.gitignore` do .NET, remover `.vs/`, `bin/`, `obj/` do controle de versão. Criar repositório público no GitHub. | P | — |
| **INFRA-02** | Reorganizar para `src/` + `tests/`. Criar os 6 projetos e as referências entre eles. `Domain` não referencia nada. | M | INFRA-01 |
| **INFRA-03** | `Directory.Build.props` com `TargetFramework net10.0`, `Nullable enable`, `TreatWarningsAsErrors`. `Directory.Packages.props` para versionamento central de pacotes. | P | INFRA-02 |
| **INFRA-04** | `docker-compose.yml` de desenvolvimento: Postgres, MongoDB, RabbitMQ (com management), Prometheus, Grafana. | M | INFRA-01 |

**Critério de aceite do épico:** `dotnet build` limpo na raiz e `docker compose up -d` sobe as 5 dependências com healthcheck verde.

---

### ÉPICO 1 — Domínio  🟠

Sem I/O. É onde ficam os testes de unidade que rodam na esteira.

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **DOM-01** | Value Object `Cpf`: normaliza para dígitos, valida os dois dígitos verificadores, rejeita sequências repetidas (`111.111.111-11`). | P | INFRA-02 |
| **DOM-02** | Value Object `Email`: normaliza para lowercase/trim, valida formato. | P | INFRA-02 |
| **DOM-03** | Entidade `Usuario`: Id, NomeCompleto, Email, Cpf, SenhaHash, Role (`GestorONG` \| `Doador`), CriadoEm. O hash **nunca** entra no construtor como texto puro. | P | DOM-01, DOM-02 |
| **DOM-04** | Entidade `Campanha`: Titulo, Descricao, DataInicio, DataFim, MetaFinanceira, Status (`Ativa` \| `Concluida` \| `Cancelada`), ValorArrecadado. | M | INFRA-02 |
| **DOM-05** | **Regras de campanha:** `DataFim` não pode ser no passado (comparar contra `TimeProvider` injetado — R5); `MetaFinanceira > 0`; `DataFim > DataInicio`. Lançar exceção de domínio tipada. | M | DOM-04 |
| **DOM-06** | Entidade `Doacao`: Id, IdCampanha, IdDoador, Valor, DataCriacao, Status (`Pendente` \| `Processada`). | P | DOM-04 |
| **DOM-07** | **Regra de doação:** rejeitar doação para campanha com status `Concluida` ou `Cancelada`, e para campanha com `DataFim` já vencida. `Valor > 0`. | P | DOM-06 |
| **DOM-08** | Método `Campanha.RegistrarArrecadacao(decimal)` — usado só pelo Worker. Encapsula o incremento e a transição automática para `Concluida` quando a meta é atingida (decidir com o time se essa transição entra no MVP). | P | DOM-04 |
| **TEST-01** | xUnit cobrindo DOM-01, DOM-05 e DOM-07. Mínimo: CPF inválido, data no passado, meta zero, doação em campanha cancelada. | M | DOM-05, DOM-07 |

**Critério de aceite:** `dotnet test tests/GestorONG.Domain.Tests` verde, sem nenhuma dependência de infraestrutura.

---

### ÉPICO 2 — Persistência  🟠

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **DATA-01** | `AppDbContext` (EF Core + Npgsql). Mapear `Usuario`, `Campanha`, `Doacao`. `decimal(18,2)` no dinheiro, `timestamptz` nas datas. | M | DOM-* |
| **DATA-02** | **Índice único em `Usuario.Email`** e índice único em `Usuario.Cpf` (R6). Tratar `DbUpdateException` de violação como 409 Conflict. | P | DATA-01 |
| **DATA-03** | Migration inicial + `Database.MigrateAsync()` no startup (aceitável para hackathon; documentar a limitação). | P | DATA-01 |
| **DATA-04** | Seed de um usuário `GestorONG` via ConfigMap/Secret — sem ele não há como criar campanha na demo. | P | DATA-03 |
| **DATA-05** | Contexto MongoDB + coleção `doacoes_processadas`. **Índice único em `IdDoacao`** (base da idempotência — R2). | M | INFRA-04 |
| **DATA-06** | Repositórios: `IUsuarioRepository`, `ICampanhaRepository`, `IDoacaoRepository` (Postgres) e `IDoacaoLedgerRepository` (Mongo). | M | DATA-01, DATA-05 |
| **DATA-07** | `ICampanhaRepository.IncrementarArrecadadoAsync(id, valor)` com `UPDATE ... SET valor_arrecadado = valor_arrecadado + @valor` atômico (R3). | P | DATA-06 |

---

### ÉPICO 3 — Autenticação e Autorização (RF 1 e 3)  🟠

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **AUTH-01** | `IPasswordHasher` com BCrypt.Net-Next (work factor 12). | P | INFRA-02 |
| **AUTH-02** | `IJwtTokenService`: emite JWT com `sub`, `email`, `role`, `exp`. Chave lida de `IConfiguration` (K8s Secret em produção, **nunca** no `appsettings.json` versionado). | M | AUTH-01 |
| **AUTH-03** | Configurar `AddAuthentication().AddJwtBearer()` fixando `RoleClaimType` explicitamente (R7). Policies `GestorOnly` e `DoadorOnly`. | M | AUTH-02 |
| **AUTH-04** | `POST /api/v1/auth/registrar` — **público**. Cadastro de Doador: NomeCompleto, Email (único), CPF (validado), Senha (hash). Retorna 201. Role é sempre `Doador` — nunca aceitar `role` do payload. | M | AUTH-03, DATA-02 |
| **AUTH-05** | `POST /api/v1/auth/login` — retorna JWT. Mensagem de erro genérica (não revelar se o e-mail existe). | P | AUTH-04 |
| **AUTH-06** | Middleware global de exceções → ProblemDetails (RFC 7807). Nunca vazar stack trace. | M | — |
| **AUTH-07** | Redigir CPF e senha do log estruturado (R9). | P | AUTH-04 |

**Critério de aceite:** endpoint protegido responde 401 sem token, 403 com token de `Doador`, 200 com token de `GestorONG`.

---

### ÉPICO 4 — Gestão de Campanhas (RF 2)  🟠

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **CAMP-01** | `POST /api/v1/campanhas` — **`GestorONG`**. Aplica DOM-05. 201 + Location. | M | AUTH-03, DATA-06 |
| **CAMP-02** | `PUT /api/v1/campanhas/{id}` — **`GestorONG`**. Editar campanha. Definir com o time o que é imutável após haver doações. | M | CAMP-01 |
| **CAMP-03** | `PATCH /api/v1/campanhas/{id}/status` — **`GestorONG`**. Transições válidas: Ativa→Concluida, Ativa→Cancelada. Bloquear o resto. | P | CAMP-01 |
| **CAMP-04** | `GET /api/v1/campanhas` + `GET /api/v1/campanhas/{id}` — **`GestorONG`**, lista todas (qualquer status), com paginação. | M | CAMP-01 |
| **CAMP-05** | Validação de request com FluentValidation ou DataAnnotations → 400 com ProblemDetails detalhando os campos. | M | CAMP-01 |

---

### ÉPICO 5 — Painel de Transparência (RF 4)  🟠

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **PUB-01** | `GET /api/v1/publico/campanhas` — **público, `[AllowAnonymous]`**. Retorna **apenas** campanhas `Ativa`. Campos: Titulo, MetaFinanceira, ValorArrecadado. | M | CAMP-01, DATA-07 |
| **PUB-02** | Garantir que o DTO público **não vaza** Descricao interna, IdGestor ou qualquer dado de doador. DTO dedicado, nunca a entidade. | P | PUB-01 |
| **PUB-03** | Adicionar `PercentualAtingido` calculado e `Cache-Control` curto. Este é o endpoint que a banca vai chamar no vídeo para provar que o Worker funcionou. | P | PUB-01 |

---

### ÉPICO 6 — Doação e Mensageria (RF 5 + requisito técnico central)  🔴🟠

**O coração da avaliação.** Se algo aqui falhar, a demo cai.

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **MSG-01** | Projeto `GestorONG.Contracts` com `DoacaoRecebidaEvent { IdDoacao, IdCampanha, IdDoador, Valor, OcorridoEm }`. Record imutável, versionado. | P | INFRA-02 |
| **MSG-02** | MassTransit + RabbitMQ na API (producer). Configuração de host via env var. | M | MSG-01, INFRA-04 |
| **MSG-03** | `POST /api/v1/doacoes` — **`Doador` logado**. Valida DOM-07, persiste `Doacao` com status `Pendente`, publica o evento. Retorna **202 Accepted** (não 201 — o processamento é assíncrono). | G | MSG-02, DOM-07 |
| **MSG-04** | **Transactional Outbox** do MassTransit com EF Core (R1). Doação + mensagem na mesma transação. | G | MSG-03 |
| **MSG-05** | Projeto `GestorONG.Worker` como `Microsoft.NET.Sdk.Worker` + MassTransit consumer. | M | MSG-01 |
| **MSG-06** | `DoacaoRecebidaConsumer`: **(1)** tenta inserir no ledger Mongo — se violar o índice único de `IdDoacao`, é redelivery, dá ACK e sai (R2); **(2)** chama o `UPDATE` atômico no Postgres (R3); **(3)** marca a `Doacao` como `Processada`. | GG | MSG-05, DATA-05, DATA-07 |
| **MSG-07** | Política de retry exponencial + **Dead Letter Queue**. Mensagem envenenada não pode travar a fila. | M | MSG-06 |
| **MSG-08** | Log estruturado no Worker com `IdDoacao` e valor antes/depois — é o que aparece no terminal durante o vídeo. | P | MSG-06 |
| **TEST-02** | Teste do consumer com `InMemoryTestHarness` do MassTransit: processar a mesma mensagem 2x e provar que o valor sobe **uma** vez. | M | MSG-06 |

**Critério de aceite do épico:** `POST /doacoes` → mensagem visível na Management UI do RabbitMQ → `GET /publico/campanhas` mostra o valor atualizado. Reenviar a mesma mensagem não altera o valor.

---

### ÉPICO 7 — Containerização e Kubernetes  🟠

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **K8S-01** | `Dockerfile` multi-stage da API (SDK → runtime, usuário não-root, imagem final `aspnet:10.0-alpine`). | M | INFRA-02 |
| **K8S-02** | `Dockerfile` multi-stage do Worker. | P | K8S-01 |
| **K8S-03** | Remover/condicionar `UseHttpsRedirection` para não quebrar a liveness probe (R8). | P | K8S-01 |
| **K8S-04** | Namespace + `ConfigMap` (connection strings sem senha, host do RabbitMQ) + `Secret` (JWT key, senhas de banco). | M | K8S-01 |
| **K8S-05** | `Deployment` + `Service` (ClusterIP) da API, com `readinessProbe` → `/health/ready` e `livenessProbe` → `/health/live`, `resources.requests/limits`. | G | K8S-04 |
| **K8S-06** | `Deployment` do Worker (sem Service — não expõe HTTP; probe via endpoint de health próprio ou `exec`). Provar escala com `replicas: 2` para validar a idempotência. | M | K8S-05 |
| **K8S-07** | Manifests de PostgreSQL, MongoDB e RabbitMQ (StatefulSet ou Deployment + PVC). Alternativa: Helm charts do Bitnami — documentar no README. | G | K8S-04 |
| **K8S-08** | `Ingress` ou `NodePort` para expor a API e a Management UI do RabbitMQ na demo. | M | K8S-05 |
| **K8S-09** | Script `k8s/deploy.sh` + `k8s/teardown.sh` — o professor precisa subir tudo com um comando. | M | K8S-07 |

---

### ÉPICO 8 — Observabilidade  🟠

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **OBS-01** | Health checks: `/health/live`, `/health/ready` (checando Postgres, Mongo e RabbitMQ) via `AspNetCore.HealthChecks.*`. | M | DATA-01 |
| **OBS-02** | `/metrics` no formato Prometheus via `OpenTelemetry.Exporter.Prometheus.AspNetCore`, com métricas de runtime + ASP.NET Core. | M | OBS-01 |
| **OBS-03** | Métricas de negócio customizadas: `doacoes_recebidas_total`, `doacoes_processadas_total`, `doacao_processamento_duracao_seconds`. É o que faz o dashboard parecer real. | M | OBS-02, MSG-06 |
| **OBS-04** | Deployment do Prometheus no K8s + `scrape_config` apontando para os pods. | G | K8S-05, OBS-02 |
| **OBS-05** | Deployment do Grafana + datasource Prometheus provisionado por ConfigMap. | M | OBS-04 |
| **OBS-06** | **Dashboard Grafana** (JSON versionado em `k8s/grafana/`) com no mínimo: requisições HTTP/s, latência p95, CPU/memória dos pods, doações processadas. **Entregável obrigatório do vídeo.** | G | OBS-05, OBS-03 |
| **OBS-07** | Logging estruturado com Serilog + `TraceId` correlacionando API → RabbitMQ → Worker. | M | AUTH-06 |
| **OBS-08** 🟢 | Zabbix monitorando os nós/pods, se sobrar tempo. O texto obrigatório do edital já é atendido por OBS-02 e OBS-06. | GG | OBS-06 |

---

### ÉPICO 9 — CI/CD  🟠

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **CI-01** | `.github/workflows/ci.yml` disparado em `push` na `main` e em `pull_request`. | P | INFRA-01 |
| **CI-02** | Job de build: `dotnet restore` → `dotnet build -c Release`. **Obrigatório.** | P | CI-01 |
| **CI-03** | Job de testes: `dotnet test` rodando TEST-01 e TEST-02 na esteira. **Bônus do edital** — barato, vale a pena. | P | CI-02, TEST-01 |
| **CI-04** | Build e **push** das imagens Docker (API e Worker) para o GHCR, com tag `sha` + `latest`. **Obrigatório** — o vídeo precisa mostrar o pipeline verde gerando a imagem. | M | CI-02, K8S-02 |
| **CI-05** 🟢 | Deploy automatizado no K8s (opcional pelo edital). | G | CI-04, K8S-09 |

---

### ÉPICO 10 — Entregáveis  🟡

Estes **valem nota diretamente**. Não deixar para a última hora.

| ID | Tarefa | Tam | Dep |
|---|---|---|---|
| **DOC-01** | **`README.md` passo a passo** para o professor subir infra + aplicação localmente. Pré-requisitos, comandos, credenciais de seed, URLs (Swagger, RabbitMQ UI, Grafana), troubleshooting. **Critério de aceite explícito do edital.** | G | K8S-09 |
| **DOC-02** | **Diagrama de arquitetura** (draw.io/Excalidraw, exportar PNG + fonte) mostrando os 2 microsserviços, os 2 bancos, o broker e as ferramentas de observabilidade. | M | — |
| **DOC-03** | **PDF justificando a escolha dos bancos** — usar a tabela da seção 2.1, expandida: por que Postgres para o transacional, por que Mongo para o ledger, e por que não um só. | M | DOC-02 |
| **DOC-04** | ADRs em `docs/adr/`: outbox, idempotência, ownership do `ValorArrecadado` (R4). Dá substância à defesa oral. | M | — |
| **DOC-05** | Swagger/OpenAPI com autenticação Bearer configurada, exemplos de request e agrupamento por tag. É por onde a demo vai passar. | M | CAMP-01 |
| **DOC-06** | **Coleção Postman** com o fluxo completo encadeado (login → cria campanha → doa → consulta público), variáveis de ambiente e token capturado automaticamente. Reduz muito o risco de errar ao vivo. | M | MSG-03 |
| **VID-01** | Roteiro escrito do vídeo, cronometrado, seguindo **exatamente** a ordem do edital: diagrama → pipeline CI → `kubectl get pods` + Grafana → login/JWT → criar campanha → doar → mensagem na fila do RabbitMQ → API pública com valor atualizado. | M | tudo |
| **VID-02** | Gravação (**máx. 15 min**) + upload no YouTube (não listado). Ensaiar uma vez antes. | G | VID-01 |
| **DOC-07** | **Relatório de entrega** (PDF/TXT): nome do grupo, participantes + usernames do Discord, link da documentação, link do(s) repositório(s), link do vídeo. Postar na data da entrega. | P | VID-02 |

---

### ÉPICO 11 — Bônus  🟢

| ID | Tarefa | Tam |
|---|---|---|
| **BON-01** | API Gateway com Ocelot roteando para a API (e futuros serviços). | G |
| **BON-02** | Testes de integração com Testcontainers (Postgres + RabbitMQ reais). | G |
| **BON-03** | HPA no K8s escalando o Worker por profundidade de fila. | G |
| **BON-04** | Rate limiting no endpoint público e no login. | M |

---

## 4. Ordem de execução sugerida

O caminho crítico não é o domínio — é a infra. Sugestão para um grupo pequeno:

| Fase | Foco | Épicos |
|---|---|---|
| **1. Fundação** | Repo, solução, compose local, CI mínimo já no ar | 0, CI-01→CI-03 |
| **2. Núcleo** | Domínio + persistência + auth funcionando local | 1, 2, 3 |
| **3. Fluxo de valor** | Campanhas, painel público e **a doação assíncrona ponta a ponta** | 4, 5, 6 |
| **4. Infra de entrega** | Docker, K8s, observabilidade, CI publicando imagem | 7, 8, CI-04 |
| **5. Entrega** | Documentação, diagrama, Postman, vídeo | 10 |
| **6. Se sobrar** | Bônus | 11 |

**Regra de ouro:** o épico 6 (doação assíncrona) precisa estar rodando ponta a ponta **antes** de qualquer polimento. É o que a banca vai olhar, e é o que tem mais chance de dar errado.

### Sugestão de paralelização

Com 3 pessoas, depois da fase 1:

- **Dev A** — épicos 1, 2, 3 (domínio, dados, auth)
- **Dev B** — épicos 4, 5, 6 (campanhas, painel, mensageria) — recebe as interfaces do Dev A cedo
- **Dev C** — épicos 7, 8, 9 (Docker, K8s, observabilidade, CI) — trabalha em paralelo desde o dia 1

O épico 10 é responsabilidade compartilhada e começa junto com a fase 4, não depois.

---

## 5. Definition of Done

Uma tarefa só está pronta quando:

1. Compila sem warnings (`TreatWarningsAsErrors`).
2. Tem teste de unidade se toca regra de domínio.
3. Passa no pipeline de CI.
4. Endpoints estão documentados no Swagger.
5. Variáveis novas estão no ConfigMap/Secret **e** no README.
6. Foi revisada por outra pessoa do grupo.
