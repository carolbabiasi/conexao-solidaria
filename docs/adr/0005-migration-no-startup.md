# ADR 0005 — Migrations aplicadas na partida da API

**Status:** aceito
**Contexto da issue:** DATA-03 (#16) · K8S-05 (#49)

## Contexto

O schema precisa existir antes do primeiro request. No Kubernetes não há passo manual entre `kubectl apply` e o tráfego chegar, e o critério de correção do edital é que o professor suba tudo com um comando.

Duas réplicas da API sobem ao mesmo tempo, o que levanta a pergunta imediata: as duas vão tentar migrar o banco juntas?

## Decisão

A API aplica as migrations na partida, antes de servir tráfego:

```csharp
await contexto.Database.MigrateAsync(cancellationToken);
```

O **Worker não migra**. Ele consome um schema que a API já criou.

## Justificativa

Para o ambiente de correção, é o que faz `deploy.ps1` ser suficiente. A alternativa honesta — um `Job` de migration separado — adiciona um recurso, uma ordem de dependência e um modo de falha novo ao roteiro que o professor vai seguir.

Sobre a concorrência entre réplicas: o EF Core adquire um lock de banco antes de aplicar migrations (`IMigrationsDatabaseLock`, presente no EF Core Relational 10 que usamos), então réplicas simultâneas serializam em vez de colidir. A segunda encontra o schema já atualizado e segue.

**Só a API migra** porque um único dono do schema é mais fácil de raciocinar do que dois serviços disputando a mesma responsabilidade.

## Consequências

- **A partida fica mais lenta**, e é por isso que os Deployments têm `startupProbe` com folga de até 150 segundos. Sem ela, a liveness mataria o pod no meio da migration e ele entraria em loop de restart antes de conseguir subir uma vez.
- **O Worker depende da API ter subido primeiro** numa base vazia. Na prática o `startupProbe` e o retry do MassTransit absorvem isso, mas a dependência existe e não está expressa em nenhum manifesto.
- **O seed do gestor tem uma janela de corrida conhecida.** `SemearGestorAsync` consulta pelo e-mail e insere se não achar. Com duas réplicas partindo juntas numa base vazia, as duas podem passar pela consulta antes de qualquer commit; a segunda tomaria violação de índice único e falharia a partida. O lock de migration não cobre o seed, que roda depois dele. Não foi observado em uso — inclusive porque o cluster sobe com o banco já migrado na maioria das vezes — mas é um risco real, e a correção seria tratar a violação como "já existe" em vez de deixar subir.
- **Em produção, isto não seria adequado.** Migration na partida dá ao processo da aplicação permissão de DDL e acopla deploy a alteração de schema. Lá, o certo é um passo de migration separado e controlado.

## Alternativas descartadas

**`initContainer` ou `Job` de migration.** Tecnicamente melhor, e é o que se usaria em produção. Descartado aqui porque adiciona um recurso e uma ordem de dependência ao caminho que precisa ser "um comando sobe tudo".

**Migration manual antes do deploy.** Quebra o critério de aceite do edital: o professor teria que executar um passo extra, com uma connection string, antes de a aplicação funcionar.

**Gerar o schema com `EnsureCreated`.** Não versiona nada, não evolui, e torna impossível alterar o schema depois sem apagar a base.
