<#
.SYNOPSIS
    Sobe a Conexao Solidaria inteira em um cluster Kubernetes local.

.DESCRIPTION
    Aplica os manifests na ordem correta, aguardando cada camada ficar pronta
    antes de seguir. Constroi as imagens localmente por padrao, o que evita
    depender do GHCR. Em cluster kind, carrega as imagens com kind load.

.PARAMETER PularBuild
    Nao constroi as imagens; usa as que ja estiverem no cluster.

.PARAMETER Senha
    Senha usada para Postgres, Mongo, RabbitMQ, Grafana e o gestor semeado.
    Apenas para ambiente local.

.EXAMPLE
    .\k8s\deploy.ps1
#>
[CmdletBinding()]
param(
    [switch]$PularBuild,
    [string]$Senha = "devlocal123"
)

# Native commands (docker, kubectl, kind) escrevem progresso no stderr, o que
# com ErrorActionPreference = Stop viraria erro terminante mesmo com exit 0.
# O controle de falha aqui e feito por $LASTEXITCODE.
$ErrorActionPreference = 'Continue'

$raiz = Split-Path -Parent $PSScriptRoot
$ns = "conexao-solidaria"
$imagemApi = "ghcr.io/carolbabiasi/conexao-solidaria-api:latest"
$imagemWorker = "ghcr.io/carolbabiasi/conexao-solidaria-worker:latest"

function Passo { param($m) Write-Host "`n==> $m" -ForegroundColor Cyan }
function Ok    { param($m) Write-Host "    OK  $m" -ForegroundColor Green }
function Aviso { param($m) Write-Host "    !!  $m" -ForegroundColor Yellow }
function Falha { param($m) Write-Host "    XX  $m" -ForegroundColor Red; exit 1 }

Passo "Verificando pre-requisitos"

foreach ($cmd in 'kubectl', 'docker') {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Falha "$cmd nao encontrado no PATH."
    }
}
Ok "kubectl e docker encontrados"

# A primeira conexao apos um restart do Docker costuma falhar enquanto o
# tunel de porta do kind se restabelece, entao vale uma sequencia de tentativas.
$conectou = $false
foreach ($tentativa in 1..5) {
    kubectl get nodes *> $null
    if ($LASTEXITCODE -eq 0) { $conectou = $true; break }
    Start-Sleep -Seconds 3
}
if (-not $conectou) {
    Falha "Nenhum cluster Kubernetes acessivel. Crie um com: kind create cluster --name conexao-solidaria"
}
$contexto = (kubectl config current-context 2>$null).Trim()
Ok "cluster acessivel: $contexto"

$ehKind = $contexto -like 'kind-*'

if (-not $PularBuild) {
    Passo "Construindo as imagens"

    docker build -q -f "$raiz/src/GestorONG.API/Dockerfile" -t $imagemApi $raiz 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { Falha "falha ao construir a imagem da API" }
    Ok "imagem da API"

    docker build -q -f "$raiz/src/GestorONG.Worker/Dockerfile" -t $imagemWorker $raiz 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { Falha "falha ao construir a imagem do Worker" }
    Ok "imagem do Worker"

    if ($ehKind) {
        if (-not (Get-Command kind -ErrorAction SilentlyContinue)) {
            Falha "contexto kind detectado mas o comando 'kind' nao esta no PATH."
        }

        $nomeDoCluster = $contexto -replace '^kind-', ''
        Passo "Carregando as imagens no cluster kind"

        kind load docker-image $imagemApi --name $nomeDoCluster *> $null
        if ($LASTEXITCODE -ne 0) { Falha "falha ao carregar a imagem da API no kind" }
        Ok "API carregada"

        kind load docker-image $imagemWorker --name $nomeDoCluster *> $null
        if ($LASTEXITCODE -ne 0) { Falha "falha ao carregar a imagem do Worker no kind" }
        Ok "Worker carregado"
    }
}

Passo "Namespace"
kubectl apply -f "$raiz/k8s/base/00-namespace.yaml" *> $null
Ok "namespace $ns"

Passo "ConfigMap e Secret"
kubectl apply -f "$raiz/k8s/base/01-configmap.yaml" *> $null
Ok "ConfigMap"

$alfabeto = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
$chaveJwt = -join ((1..48) | ForEach-Object { $alfabeto[(Get-Random -Maximum $alfabeto.Length)] })

kubectl create secret generic gestorong-secret `
    --namespace $ns `
    --from-literal=POSTGRES_USER=gestorong `
    --from-literal=POSTGRES_PASSWORD=$Senha `
    --from-literal=POSTGRES_DB=conexaosolidaria `
    --from-literal=MONGO_INITDB_ROOT_USERNAME=gestorong `
    --from-literal=MONGO_INITDB_ROOT_PASSWORD=$Senha `
    --from-literal="ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=conexaosolidaria;Username=gestorong;Password=$Senha" `
    --from-literal="Mongo__ConnectionString=mongodb://gestorong:$Senha@mongo:27017/?authSource=admin" `
    --from-literal=RABBITMQ_DEFAULT_USER=gestorong `
    --from-literal=RABBITMQ_DEFAULT_PASS=$Senha `
    --from-literal=RabbitMq__Usuario=gestorong `
    --from-literal=RabbitMq__Senha=$Senha `
    --from-literal=Jwt__ChaveSecreta=$chaveJwt `
    --from-literal=Seed__GestorSenha=$Senha `
    --from-literal=GF_SECURITY_ADMIN_USER=admin `
    --from-literal=GF_SECURITY_ADMIN_PASSWORD=$Senha `
    --dry-run=client -o yaml 2>$null | kubectl apply -f - *> $null
Ok "Secret (chave JWT gerada aleatoriamente)"

Passo "Dependencias: PostgreSQL, MongoDB e RabbitMQ"
kubectl apply -f "$raiz/k8s/dependencias/" *> $null
Ok "manifests aplicados"

Write-Host "    aguardando ficarem prontos (pode levar alguns minutos)..." -ForegroundColor DarkGray
foreach ($dep in 'postgres', 'mongo', 'rabbitmq') {
    kubectl rollout status "deployment/$dep" -n $ns --timeout=300s *> $null
    if ($LASTEXITCODE -eq 0) { Ok $dep } else { Aviso "$dep nao ficou pronto no tempo esperado" }
}

Passo "Observabilidade: Prometheus e Grafana"
kubectl create configmap grafana-dashboards `
    --namespace $ns `
    --from-file="$raiz/k8s/observabilidade/dashboards/" `
    --dry-run=client -o yaml 2>$null | kubectl apply -f - *> $null
Ok "ConfigMap do dashboard"

kubectl apply -f "$raiz/k8s/observabilidade/30-prometheus.yaml" *> $null
kubectl apply -f "$raiz/k8s/observabilidade/31-grafana.yaml" *> $null
foreach ($dep in 'prometheus', 'grafana') {
    kubectl rollout status "deployment/$dep" -n $ns --timeout=300s *> $null
    if ($LASTEXITCODE -eq 0) { Ok $dep } else { Aviso "$dep nao ficou pronto no tempo esperado" }
}

Passo "Aplicacao: API e Worker"
kubectl apply -f "$raiz/k8s/base/20-api.yaml" *> $null
kubectl apply -f "$raiz/k8s/base/21-worker.yaml" *> $null
foreach ($dep in 'gestorong-api', 'gestorong-worker') {
    kubectl rollout status "deployment/$dep" -n $ns --timeout=300s *> $null
    if ($LASTEXITCODE -eq 0) { Ok $dep } else { Aviso "$dep nao ficou pronto no tempo esperado" }
}

Passo "Pronto"
kubectl get pods -n $ns

Write-Host @"

Para acessar as interfaces, abra um terminal para cada:

  kubectl port-forward -n $ns svc/gestorong-api 8080:80
  kubectl port-forward -n $ns svc/rabbitmq 15672:15672
  kubectl port-forward -n $ns svc/grafana 3000:3000
  kubectl port-forward -n $ns svc/prometheus 9090:9090

  API .......... http://localhost:8080/openapi/v1.json
  Painel ....... http://localhost:8080/api/v1/publico/campanhas
  RabbitMQ ..... http://localhost:15672   (gestorong / $Senha)
  Grafana ...... http://localhost:3000    (admin / $Senha)
  Prometheus ... http://localhost:9090

  Gestor ....... gestor@esperancasolidaria.org / $Senha
"@ -ForegroundColor Gray
