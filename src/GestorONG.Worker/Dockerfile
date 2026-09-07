FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY GestorONG.slnx ./
COPY src/GestorONG.Domain/GestorONG.Domain.csproj                 src/GestorONG.Domain/
COPY src/GestorONG.Application/GestorONG.Application.csproj       src/GestorONG.Application/
COPY src/GestorONG.Contracts/GestorONG.Contracts.csproj           src/GestorONG.Contracts/
COPY src/GestorONG.Infrastructure/GestorONG.Infrastructure.csproj src/GestorONG.Infrastructure/
COPY src/GestorONG.Worker/GestorONG.Worker.csproj                 src/GestorONG.Worker/

RUN dotnet restore src/GestorONG.Worker/GestorONG.Worker.csproj

COPY src/ src/
RUN dotnet publish src/GestorONG.Worker/GestorONG.Worker.csproj \
      -c Release \
      -o /app/publish \
      --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

RUN adduser --disabled-password --home /app --gecos '' gestorong \
    && chown -R gestorong /app
USER gestorong

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1
EXPOSE 8080

ENTRYPOINT ["dotnet", "GestorONG.Worker.dll"]
