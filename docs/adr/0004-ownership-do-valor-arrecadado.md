# ADR 0004 — Só o Worker escreve o valor arrecadado

**Status:** aceito
**Contexto da issue:** DATA-07 (#40) · risco R4

## Contexto

O edital é explícito sobre a API:

> "Ao receber uma nova doação, a API **NÃO** deve atualizar o valor arrecadado da campanha diretamente no banco de dados."

E também diz que o painel exibe o valor *"calculado com base nas doações processadas"*. As duas frases juntas deixam uma ambiguidade: o total é uma coluna que alguém escreve, ou uma soma calculada na hora da leitura?

## Decisão

Coluna materializada `valor_arrecadado` em `campanhas`, com **um único escritor: o Worker**, através de incremento atômico:

```csharp
.ExecuteUpdateAsync(a => a.SetProperty(
    c => c.ValorArrecadado,
    c => c.ValorArrecadado + valor));
```

O ledger do MongoDB é a fonte para reconciliar esse total quando for preciso.

## Justificativa

**Um escritor só** é o que torna a regra do edital verificável. Não é uma convenção de equipe: a API não tem nenhum caminho de código que escreva nessa coluna, e é isso que se demonstra na defesa.

**Incremento atômico, nunca read-modify-write.** Com duas réplicas do Worker, ler o valor, somar em memória e gravar de volta perderia doações — duas atualizações concorrentes, uma sobrescreve a outra. O `SET valor_arrecadado = valor_arrecadado + @valor` é resolvido pelo banco sob o lock da linha, e sobrevive a qualquer número de réplicas.

**Materializar em vez de somar na leitura** protege o endpoint mais exposto do sistema. `GET /api/v1/publico/campanhas` é anônimo e é o que fica aberto na tela durante a demo. Um `SUM()` sobre doações ali cresce com o volume e transforma o painel público num caminho caro e sem autenticação.

## Consequências

- **O total é estado derivado, e pode divergir do ledger.** Reconciliar é procedimento operacional, não garantia do banco.
- **O painel é eventualmente consistente.** Entre o `202 Accepted` e o valor subir passam alguns segundos. É visível na demo, e é o comportamento correto para o desenho assíncrono que o edital pede.
- `ValorArrecadado` pode ultrapassar `MetaFinanceira`, já que a campanha não se encerra sozinha ao bater a meta — ver [ADR 0001](0001-transicao-ao-atingir-a-meta.md).

## Alternativas descartadas

**`SUM()` das doações a cada leitura do painel.** Sempre consistente, e cobra esse preço no endpoint público a cada requisição, com custo crescente. Também deixaria a coluna `valor_arrecadado` sem sentido, contrariando a parte do edital que fala em atualizar o valor.

**Trigger no banco somando a cada insert de doação.** Funciona, e esconde a regra num lugar que não aparece no código, não roda nos testes e não pode ser explicado lendo o Worker. O edital quer ver o processamento assíncrono acontecendo; um trigger o tornaria invisível.

**Projeção por event sourcing.** Trocaria uma coluna e um `UPDATE` por replay e projeções. Complexidade sem retorno para cinco casos de uso.
