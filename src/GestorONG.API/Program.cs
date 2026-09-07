using GestorONG.API.Middleware;
using GestorONG.Infrastructure;
using GestorONG.Infrastructure.Mensageria;
using GestorONG.Infrastructure.Observabilidade;
using GestorONG.Infrastructure.Persistencia;
using GestorONG.Infrastructure.Seguranca;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorGlobalDeExcecoes>();

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarAutenticacao(builder.Configuration);
builder.Services.AdicionarMensageria(builder.Configuration, comOutbox: true);
builder.Services.AdicionarObservabilidade(builder.Configuration, "gestorong-api");

var app = builder.Build();

await app.Services.MigrarESemearAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapearObservabilidade();

app.Run();
