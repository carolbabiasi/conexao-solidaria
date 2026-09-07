# ADR 0001 — Campanha não transita para Concluída ao atingir a meta

**Status:** aceito
**Contexto da issue:** DOM-08 (#12)

## Contexto

`Campanha.RegistrarArrecadacao` é chamado pelo Worker a cada doação processada. A pergunta que a issue deixou em aberto: quando `ValorArrecadado` alcança `MetaFinanceira`, a campanha deve mudar sozinha para `Concluida`?

O edital não diz. Ele define os três status e determina que campanhas concluídas não aceitam doações, mas não descreve nenhuma transição automática.

## Decisão

**Não transitar.** A campanha permanece `Ativa` mesmo depois de bater a meta. A mudança para `Concluida` é sempre explícita, feita pelo gestor via `PATCH /api/v1/campanhas/{id}/status` (CAMP-03).

## Justificativa

Transitar automaticamente parece cuidadoso, mas produz um comportamento que a ONG não pediu: **a campanha passa a recusar dinheiro**. Uma meta de R$ 5.000 que arrecadou R$ 5.000 e continua recebendo doações é sucesso, não erro. Encerrar sozinha transformaria a meta em teto.

Existe também um problema de corrida. Como o Worker roda com múltiplas réplicas e o incremento é atômico no banco (DATA-07), duas doações concorrentes podem cruzar a meta ao mesmo tempo. Se a transição fosse automática, o efeito dependeria da ordem de chegada — e a doação perdedora seria rejeitada por uma campanha que ainda estava aberta quando o doador clicou.

Por fim, isso mantém o `RegistrarArrecadacao` com uma responsabilidade só: somar. Transição de status é decisão de negócio do gestor, e já tem endpoint próprio.

## Consequências

- `ValorArrecadado` pode ultrapassar `MetaFinanceira`. O painel público (PUB-03) precisa lidar com percentual acima de 100%, exibindo o excedente em vez de truncar.
- A campanha continua aceitando doações até o gestor encerrá-la ou a `DataFim` passar — o que `Doacao.Criar` já verifica.
- Se a ONG quiser encerramento automático depois, é uma regra nova, não uma correção.

## Alternativas descartadas

**Transitar para `Concluida` ao atingir a meta.** Descartada pelos motivos acima.

**Transitar, mas continuar aceitando doações em campanha concluída.** Descartada por contrariar o edital, que é explícito: campanhas concluídas não aceitam doações.
