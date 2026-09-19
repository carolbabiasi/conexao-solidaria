using GestorONG.API.Configuracao;
using GestorONG.API.Middleware;
using GestorONG.Infrastructure;
using GestorONG.Infrastructure.Mensageria;
using GestorONG.Infrastructure.Observabilidade;
using GestorONG.Infrastructure.Persistencia;
using GestorONG.Infrastructure.Seguranca;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(opcoes =>
{
    opcoes.AddDocumentTransformer<TransformadorDeSeguranca>();
    opcoes.AddSchemaTransformer<TransformadorDeExemplos>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorGlobalDeExcecoes>();

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarAutenticacao(builder.Configuration);
builder.Services.AdicionarMensageria(builder.Configuration, comOutbox: true);
builder.Services.AdicionarObservabilidade(builder.Configuration, "gestorong-api");

var app = builder.Build();

await app.Services.MigrarESemearAsync();

app.UseExceptionHandler();

app.MapOpenApi();
app.UseSwaggerUI(opcoes =>
{
    opcoes.SwaggerEndpoint("/openapi/v1.json", "Conexão Solidária v1");
    opcoes.RoutePrefix = "swagger";
    opcoes.DocumentTitle = "Conexão Solidária — API";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapearObservabilidade();

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();

app.Run();
