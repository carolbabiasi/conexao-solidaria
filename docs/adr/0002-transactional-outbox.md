# ADR 0002 — Doação e evento na mesma transação, via outbox

**Status:** aceito
**Contexto da issue:** MSG-02 (#37) · risco R1

## Contexto

Ao aceitar uma doação, a API precisa fazer duas coisas: gravar a intenção no PostgreSQL e publicar `DoacaoRecebidaEvent` no RabbitMQ. São dois sistemas diferentes, e não existe transação que abranja os dois.

Qualquer ordem ingênua tem um modo de falha:

- **Gravar e depois publicar:** se o publish falha após o commit, a doação existe no banco e nunca é processada. O valor arrecadado nunca sobe, e **nenhum erro aparece em lugar nenhum**.
- **Publicar e depois gravar:** se o commit falha, o Worker recebe um evento para uma doação que não existe.

O primeiro caso é pior, porque é silencioso. Dinheiro que o doador acha que entregou, e que some sem rastro.

## Decisão

**Transactional Outbox**, com o suporte do MassTransit sobre EF Core. A mensagem é gravada numa tabela de outbox dentro da **mesma transação** da doação, e o bus entrega depois:

```csharp
configurador.AddEntityFrameworkOutbox<AppDbContext>(outbox =>
{
    outbox.UsePostgres();
    outbox.UseBusOutbox();
});
```

Ou a doação e a mensagem existem juntas, ou nenhuma das duas existe.

## Justificativa

O padrão troca um problema sem solução (transação distribuída entre banco e broker) por um que o banco já resolve: uma transação local. A entrega ao broker deixa de ser parte da operação do usuário e vira responsabilidade de um processo de entrega que pode tentar de novo quantas vezes precisar.

O custo é baixo porque já temos um banco transacional no caminho — o outbox não adiciona infraestrutura nova, só três tabelas.

## Consequências

- **A entrega é assíncrona em relação ao commit.** Existe uma latência entre aceitar a doação e a mensagem chegar ao broker. Para este sistema é irrelevante: o doador já recebeu `202 Accepted`.
- **A entrega é *at-least-once*.** O outbox garante que a mensagem sai, não que sai uma vez só. Isso torna a idempotência do consumer obrigatória, não opcional — ver [ADR 0003](0003-idempotencia-do-consumer.md).
- **Amarra a escolha do banco.** O padrão depende de uma transação que cubra as duas escritas, o que exclui um banco sem transação local barata.
- Três tabelas a mais no schema: `InboxState`, `OutboxMessage`, `OutboxState`.

## Alternativas descartadas

**Publicar direto, sem outbox.** É a dupla escrita descrita acima. Funciona no caminho feliz e perde doações em silêncio quando o broker oscila.

**Two-phase commit entre PostgreSQL e RabbitMQ.** Acopla os dois recursos, exige coordenador transacional e degrada a disponibilidade dos dois ao mesmo tempo. Custo desproporcional para o problema.

**Change Data Capture (Debezium lendo o WAL).** Resolveria, e adicionaria um componente de infraestrutura inteiro — conector, tópicos, operação — a um MVP de dois serviços.
