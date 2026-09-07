using GestorONG.Application.Abstracoes;
using GestorONG.Infrastructure;
using GestorONG.Infrastructure.Mensageria;
using GestorONG.Infrastructure.Observabilidade;
using GestorONG.Worker.Consumidores;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarInfraestrutura(builder.Configuration);

builder.Services.AdicionarMensageria(
    builder.Configuration,
    consumidores => consumidores.AddConsumer<DoacaoRecebidaConsumer>());

builder.Services.AdicionarObservabilidade(builder.Configuration, "gestorong-worker");

var app = builder.Build();

using (var escopo = app.Services.CreateScope())
{
    var ledger = escopo.ServiceProvider.GetRequiredService<IDoacaoLedgerRepository>();
    await ledger.GarantirIndicesAsync();
}

app.MapearObservabilidade();

await app.RunAsync();
