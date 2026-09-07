using GestorONG.Application.Abstracoes;
using GestorONG.Infrastructure;
using GestorONG.Infrastructure.Mensageria;
using GestorONG.Worker.Consumidores;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AdicionarInfraestrutura(builder.Configuration);

builder.Services.AdicionarMensageria(
    builder.Configuration,
    consumidores => consumidores.AddConsumer<DoacaoRecebidaConsumer>());

var host = builder.Build();

using (var escopo = host.Services.CreateScope())
{
    var ledger = escopo.ServiceProvider.GetRequiredService<IDoacaoLedgerRepository>();
    await ledger.GarantirIndicesAsync();
}

await host.RunAsync();
