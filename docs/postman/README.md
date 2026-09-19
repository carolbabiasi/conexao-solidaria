# Coleção Postman

Fluxo completo da demo, encadeado. **Nenhum token é copiado à mão** — cada
requisição guarda o que a próxima precisa em variáveis de coleção.

## Como usar

1. Importe os três arquivos desta pasta no Postman
2. Selecione o environment: **Local** ou **Kubernetes**
3. Clique em **Run collection**

Precisa da API no ar. Para o painel público refletir a doação, o **Worker
também** precisa estar rodando — é ele que soma.

| Environment | `baseUrl` | Pré-requisito |
|---|---|---|
| Local | `http://localhost:5231` | `docker compose up -d` e os dois `dotnet run` |
| Kubernetes | `http://localhost:8080` | `kubectl port-forward svc/gestorong-api 8080:80` |

## O que a coleção faz

**Pasta 1 — Fluxo da demo.** Login do gestor, criar campanha, registrar doador,
login do doador, doar, e consultar o painel público até o valor subir.

A última requisição se repete até cinco vezes enquanto o valor estiver zerado. É
o que permite o `Run collection` provar o resultado **assíncrono** sem ninguém
apertar nada: o `202` volta na hora, mas o total só sobe quando o Worker
processa.

**Pasta 2 — Regras de negócio.** Requisições que devem falhar:

| Requisição | Esperado | Regra demonstrada |
|---|---|---|
| Cadastro repetido | `409` | Índice único de e-mail e CPF |
| Doador criando campanha | `403` | RBAC: só GestorONG cria |
| Campanha com data no passado | `400` | Validação de domínio |
| Doação em campanha cancelada | `400` | Campanha cancelada não aceita doações |
| Reabrir campanha cancelada | `409` | Concluída e cancelada são estados finais |

**Pasta 3 — Gestão da campanha.** Edição (`PUT`) e listagem paginada, a visão
que o gestor tem e o painel público não dá. Inclui o `pageSize` absurdo sendo
limitado ao teto em vez de recusado.

## Decisões que valem explicar

**O cadastro aceita `201` ou `409`.** Na primeira execução cria o doador; nas
seguintes ele já existe. Aceitar os dois é o que torna a coleção re-executável
sem limpar o banco antes — e o `409` já é prova de que o índice único funciona.

**As datas são geradas na hora.** Um `dataFim` fixo no arquivo venceria, e a
coleção passaria a falhar sozinha com o tempo.

**O `status` vai numérico** (1 Ativa, 2 Concluída, 3 Cancelada). Não há
`JsonStringEnumConverter` configurado, então `"Ativa"` quebraria com `400`.

**A campanha cancelada é cancelada via `PATCH`.** A pasta 2 cancela a própria
campanha da pasta 1, depois de a doação já ter sido processada — e a
requisição seguinte confirma que ela sumiu do painel público.

## Se o login do doador falhar

O environment assume que o e-mail configurado ou não existe, ou existe com a
senha configurada. Num banco que já foi usado com outros dados, pode haver um
usuário com esse e-mail e senha diferente — aí o cadastro devolve `409` e o login
seguinte falha com `401`.

Troque `emailDoador` **e** `cpfDoador` no environment, ou limpe a base:

```bash
cd docker && docker compose down -v && docker compose up -d
```

> As senhas nos environments são as de desenvolvimento local, as mesmas do
> `README` e do `.env.example`. Não use nada disso fora da máquina de quem
> desenvolve.
