<#
.SYNOPSIS
    Remove a Conexao Solidaria do cluster.

.PARAMETER ManterDados
    Preserva os PersistentVolumeClaims.

.EXAMPLE
    .\k8s\teardown.ps1
#>
[CmdletBinding()]
param([switch]$ManterDados)

$ErrorActionPreference = 'Stop'
$ns = "conexao-solidaria"

if ($ManterDados) {
    Write-Host "==> Removendo workloads, preservando os volumes" -ForegroundColor Cyan
    kubectl delete deployment --all -n $ns --ignore-not-found
    kubectl delete service --all -n $ns --ignore-not-found
    kubectl delete configmap --all -n $ns --ignore-not-found
    kubectl delete secret gestorong-secret -n $ns --ignore-not-found
}
else {
    Write-Host "==> Removendo o namespace inteiro, incluindo os dados" -ForegroundColor Cyan
    kubectl delete namespace $ns --ignore-not-found
    kubectl delete clusterrole prometheus-conexao-solidaria --ignore-not-found
    kubectl delete clusterrolebinding prometheus-conexao-solidaria --ignore-not-found
}

Write-Host "    concluido" -ForegroundColor Green
