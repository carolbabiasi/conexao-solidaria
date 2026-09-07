# Conexão Solidária

[![CI](https://github.com/carolbabiasi/conexao-solidaria/actions/workflows/ci.yml/badge.svg)](https://github.com/carolbabiasi/conexao-solidaria/actions/workflows/ci.yml)

Plataforma digital da ONG Esperança Solidária — MVP do Hackathon 11NETT.

> **Este README ainda é um esqueleto.** As instruções passo a passo de como
> subir a infraestrutura e a aplicação localmente são a issue
> [DOC-01](https://github.com/carolbabiasi/conexao-solidaria/issues/67).

## Documentação

- [Análise técnica e backlog](docs/BACKLOG.md) — arquitetura, riscos mapeados e as 79 tarefas
- [Issues](https://github.com/carolbabiasi/conexao-solidaria/issues) organizadas por épico e fase

## Subindo a infraestrutura local

```bash
cd docker
cp .env.example .env
docker compose up -d
```

| Serviço | Porta | Interface |
|---|---|---|
| PostgreSQL | 5432 | — |
| MongoDB | 27017 | — |
| RabbitMQ | 5672 | http://localhost:15672 |
| Prometheus | 9090 | http://localhost:9090 |
| Grafana | 3000 | http://localhost:3000 |

## Compilando

```bash
dotnet build GestorONG.slnx -c Release
```
