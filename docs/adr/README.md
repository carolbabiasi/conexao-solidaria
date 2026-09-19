# Decisões arquiteturais

Registro curto das decisões que não se explicam sozinhas lendo o código — cada
uma responde a uma pergunta que a banca provavelmente vai fazer.

| ADR | Decisão | Pergunta que responde |
|---|---|---|
| [0001](0001-transicao-ao-atingir-a-meta.md) | Campanha não se encerra sozinha ao atingir a meta | *"E quando a campanha bate a meta?"* |
| [0002](0002-transactional-outbox.md) | Doação e evento na mesma transação, via outbox | *"E se o publish falhar depois do commit?"* |
| [0003](0003-idempotencia-do-consumer.md) | Idempotência pelo índice único do ledger | *"E se a mensagem chegar duas vezes?"* |
| [0004](0004-ownership-do-valor-arrecadado.md) | Só o Worker escreve o valor arrecadado | *"Por que coluna materializada e não `SUM()`?"* |
| [0005](0005-migration-no-startup.md) | Migrations aplicadas na partida da API | *"E se duas réplicas subirem juntas?"* |
| [0006](0006-campos-imutaveis-apos-doacao.md) | Meta e data de início congelam após a primeira doação | *"O gestor pode editar uma campanha que já recebeu dinheiro?"* |

Formato: contexto, decisão, justificativa, consequências e alternativas
descartadas. As consequências incluem o que a decisão **custa** — um ADR que só
lista vantagens não sobrevive à primeira pergunta.

A justificativa da escolha dos bancos está separada, em
[`docs/escolha-dos-bancos.md`](../escolha-dos-bancos.md).
