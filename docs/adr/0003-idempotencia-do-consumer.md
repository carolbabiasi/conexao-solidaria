# ADR 0003 — Idempotência pelo índice único do ledger

**Status:** aceito
**Contexto da issue:** MSG-06 (#41) · risco R2

## Contexto

A entrega é *at-least-once*, por duas razões somadas: o RabbitMQ reentrega quando o ACK se perde, e o outbox ([ADR 0002](0002-transactional-outbox.md)) garante que a mensagem sai, não que sai uma vez só.

O consumer incrementa dinheiro. Processar a mesma doação duas vezes não é um detalhe de consistência — **infla o valor arrecadado da campanha**, que é exatamente o número exibido no painel público.

Com duas réplicas do Worker, a mesma mensagem pode ainda chegar a pods diferentes em tentativas diferentes.

## Decisão

Índice único em `idDoacao` na coleção `doacoes_processadas` do MongoDB. O consumer **não consulta antes de inserir**: tenta inserir e deixa o banco decidir.

```csharp
try
{
    await _colecao.InsertOneAsync(registro, cancellationToken: ct);
    return true;                       // inédita: pode somar
}
catch (MongoWriteException e)
    when (e.WriteError?.Code == 11000) // chave duplicada
{
    return false;                      // redelivery: descarta sem somar
}
```

Só quando o insert é inédito o Worker segue para o incremento.

## Justificativa

Consultar e depois inserir tem exatamente a mesma janela de corrida da checagem de e-mail no cadastro: dois consumidores passam os dois pela consulta e inserem os dois. Tentar inserir e tratar a violação **não tem janela nenhuma**, porque checagem e escrita são a mesma operação, resolvida pelo índice.

O ledger existiria de qualquer forma, para auditoria e reconciliação. Usá-lo como chave de idempotência não adiciona estrutura nova: a trava é um efeito colateral gratuito de um registro que já precisávamos manter.

## Consequências

- **O MongoDB entra no caminho crítico do processamento.** Se ele estiver fora, o Worker não processa — e é por isso que o `/health/ready` o inclui.
- **A ordem das escritas importa.** O Worker grava no ledger *antes* de somar. Se cair entre as duas, a doação fica registrada sem ter sido somada, e a reentrega será descartada como duplicata: perde-se uma soma. A ordem inversa somaria duas vezes. Perder é reconciliável contra o ledger; somar a mais, não.
- **As duplicatas são observáveis.** Cada descarte incrementa `doacoes_duplicadas_descartadas_total`, o que torna a idempotência visível no dashboard em vez de invisível.

## Alternativas descartadas

**Redis com `SETNX`.** A trava mais barata que existe, e volátil. Expirada a chave, uma reentrega tardia voltaria a somar. Além disso separaria a trava do histórico, criando dois lugares que podem divergir.

**Tabela de deduplicação no PostgreSQL.** Funcionaria igual, e duplicaria no relacional um registro que já existe no ledger — com a diferença de colocar mais escrita append-only no banco que está no caminho da requisição.

**Confiar em entrega *exactly-once*.** Não existe em RabbitMQ, nem em nenhum broker, sem que o consumidor faça a sua parte. A garantia é sempre construída no consumo.
