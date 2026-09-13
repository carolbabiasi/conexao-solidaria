<#
.SYNOPSIS
    Sobe a Conexao Solidaria inteira em um cluster Kubernetes local.

.DESCRIPTION
    Aplica os manifests na ordem correta, aguardando cada camada ficar pronta
    antes de seguir. Constroi as imagens localmente por padrao, o que evita
    depender do GHCR.

.PARAMETER PularBuild
    Nao constroi as imagens; usa as que ja existirem no cluster.

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

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$ns = "conexao-solidaria"
$imagemApi = "ghcr.io/carolbabiasi/conexao-solidaria-api:latest"
$imagemWorker = "ghcr.io/carolbabiasi/conexao-solidaria-worker:latest"

function Passo { param($m) Write-Host "`n==> $m" -ForegroundColor Cyan }
function Ok    { param($m) Write-Host "    OK  $m" -ForegroundColor Green }
function Aviso { param($m) Write-Host "    !!  $m" -ForegroundColor Yellow }

Passo "Verificando pre-requisitos"

foreach ($cmd in 'kubectl', 'docker') {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        throw "$cmd nao encontrado no PATH."
    }
}
Ok "kubectl e docker encontrados"

kubectl cluster-info 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Nenhum cluster Kubernetes acessivel. Habilite o Kubernetes no Docker Desktop (Settings > Kubernetes > Enable Kubernetes)."
}
Ok "cluster acessivel: $(kubectl config current-context)"

if (-not $PularBuild) {
    Passo "Construindo as imagens"
    docker build -q -f "$raiz/src/GestorONG.API/Dockerfile" -t $imagemApi $raiz | Out-Null
    Ok "imagem da API"
    docker build -q -f "$raiz/src/GestorONG.Worker/Dockerfile" -t $imagemWorker $raiz | Out-Null
    Ok "imagem do Worker"
}

Passo "Namespace"
kubectl apply -f "$raiz/k8s/base/00-namespace.yaml" | Out-Null
Ok "namespace $ns"

Passo "ConfigMap e Secret"
kubectl apply -f "$raiz/k8s/base/01-configmap.yaml" | Out-Null
Ok "ConfigMap"

$chaveJwt = -join ((1..48) | ForEach-Object { "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[(Get-Random -Maximum 62)] })

kubectl create secret generic gestorong-secret `
    --namespace $ns `
    --from-literal=POSTGRES_USER=gestorong `
    --from-literal=POSTGRES_PASSWORD=$Senha `
    --from-literal=POSTGRES_DB=conexaosolidaria `
    --from-literal=MONGO_INITDB_ROOT_USERNAME=gestorong `
    --from-literal=MONGO_INITDB_ROOT_PASSWORD=$Senha `
    --from-literal=RABBITMQ_DEFAULT_USER=gestorong `
    --from-literal=RABBITMQ_DEFAULT_PASS=$Senha `
    --from-literal="ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=conexaosolidaria;Username=gestorong;Password=$Senha" `
    --from-literal="Mongo__ConnectionString=mongodb://gestorong:$Senha@mongo:27017/?authSource=admin" `
    --from-literal=RabbitMq__Usuario=gestorong `
    --from-literal=RabbitMq__Senha=$Senha `
    --from-literal=Jwt__ChaveSecreta=$chaveJwt `
    --from-literal=Seed__GestorSenha=$Senha `
    --from-literal=GF_SECURITY_ADMIN_USER=admin `
    --from-literal=GF_SECURITY_ADMIN_PASSWORD=$Senha `
    --dry-run=client -o yaml | kubectl apply -f - | Out-Null
Ok "Secret (chave JWT gerada aleatoriamente)"

Passo "Dependencias: PostgreSQL, MongoDB e RabbitMQ"
kubectl apply -f "$raiz/k8s/dependencias/" | Out-Null
Ok "manifests aplicados"

Write-Host "    aguardando ficarem prontos (pode levar alguns minutos)..." -ForegroundColor DarkGray
foreach ($dep in 'postgres', 'mongo', 'rabbitmq') {
    kubectl rollout status "deployment/$dep" -n $ns --timeout=300s | Out-Null
    if ($LASTEXITCODE -eq 0) { Ok $dep } else { Aviso "$dep nao ficou pronto" }
}

Passo "Observabilidade: Prometheus e Grafana"
kubectl create configmap grafana-dashboards `
    --namespace $ns `
    --from-file="$raiz/k8s/observabilidade/dashboards/" `
    --dry-run=client -o yaml | kubectl apply -f - | Out-Null
Ok "ConfigMap do dashboard"

kubectl apply -f "$raiz/k8s/observabilidade/30-prometheus.yaml" | Out-Null
kubectl apply -f "$raiz/k8s/observabilidade/31-grafana.yaml" | Out-Null
foreach ($dep in 'prometheus', 'grafana') {
    kubectl rollout status "deployment/$dep" -n $ns --timeout=300s | Out-Null
    if ($LASTEXITCODE -eq 0) { Ok $dep } else { Aviso "$dep nao ficou pronto" }
}

Passo "Aplicacao: API e Worker"
kubectl apply -f "$raiz/k8s/base/20-api.yaml" | Out-Null
kubectl apply -f "$raiz/k8s/base/21-worker.yaml" | Out-Null
foreach ($dep in 'gestorong-api', 'gestorong-worker') {
    kubectl rollout status "deployment/$dep" -n $ns --timeout=300s | Out-Null
    if ($LASTEXITCODE -eq 0) { Ok $dep } else { Aviso "$dep nao ficou pronto" }
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
