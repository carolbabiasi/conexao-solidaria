# ADR 0006 — Meta e data de início congelam após a primeira doação

**Status:** aceito
**Contexto da issue:** CAMP-02 (#29)

## Contexto

O edital pede "criar/editar" campanhas. Editar levanta uma pergunta que a issue deixou explicitamente em aberto para o time: **o que pode mudar depois que a campanha já recebeu dinheiro?**

Uma campanha é uma promessa pública. Quem doou viu um título, uma meta e um período, e decidiu com base nisso. Editar livremente permitiria reescrever esse combinado depois do fato — inclusive de formas que mudam o significado do que já foi doado.

## Decisão

Depois da primeira doação (`ValorArrecadado > 0`), ficam **congelados**:

- `MetaFinanceira`
- `DataInicio`

Continuam editáveis, sempre:

- `Titulo`
- `Descricao`
- `DataFim`

`ValorArrecadado` **não é editável em hipótese alguma** — não existe parâmetro para ele em `Atualizar`, e nenhum caminho pela API o altera. Só o Worker escreve esse campo, via `RegistrarArrecadacao` ([ADR 0004](0004-ownership-do-valor-arrecadado.md)).

Além disso, só campanhas **ativas** podem ser editadas: concluída e cancelada são estados finais.

## Justificativa

**A meta muda o significado do que já foi doado.** Alguém que doou para uma campanha de R$ 5.000 contribuiu com uma fração do objetivo. Baixar a meta para R$ 1.000 transformaria a campanha em "concluída com folga" sem que um centavo a mais entrasse; subir para R$ 500.000 faria a mesma doação virar ruído. O percentual exibido no painel público é calculado sobre ela.

**A data de início delimita o período que foi anunciado.** Movê-la para trás faz a campanha parecer que já durava mais do que durou; para frente, joga doações já recebidas para fora do próprio período.

**A data de término é diferente das outras duas.** Prorrogar uma campanha é uma operação legítima e comum — não trai ninguém que já doou, e quem doou continua tendo doado para a mesma causa com a mesma meta. Encurtar também é aceitável: encerra a arrecadação mais cedo, mas não reescreve nada do passado.

**Título e descrição** precisam continuar editáveis pelo motivo mais banal: erro de digitação em texto que está numa página pública.

## Consequências

- **Um erro de meta vira problema operacional.** Se o gestor publicar R$ 50.000 quando queria R$ 5.000 e alguém doar antes da correção, a única saída é cancelar a campanha e criar outra. É o preço de não permitir reescrita retroativa, e é por isso que o cancelamento existe.
- **A regra depende de um único sinal:** `ValorArrecadado > 0`. Uma campanha que recebeu e teve tudo estornado — cenário que não existe hoje — continuaria congelada.
- A validação vive no domínio, em `Campanha.Atualizar`, e não no controller. Isso a mantém testável sem HTTP e impossível de contornar por outro caminho de escrita.

## Alternativas descartadas

**Congelar tudo depois da primeira doação.** Mais simples de explicar, e impede corrigir um erro de digitação numa descrição pública. O custo é alto demais para o ganho.

**Não congelar nada, e registrar o histórico de alterações.** Seria a resposta correta num sistema real: auditoria em vez de proibição. Exige versionamento da campanha e uma tela para consultar o histórico — fora do escopo de um MVP de cinco casos de uso.

**Congelar apenas a meta, deixando a data de início livre.** Metade do problema. Mover o início para depois de doações já recebidas produz um estado incoerente, com doação fora do período da campanha.
