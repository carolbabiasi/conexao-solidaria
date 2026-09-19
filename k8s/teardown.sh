#!/usr/bin/env bash
#
# Remove a Conexao Solidaria do cluster.
#
# Uso:
#   ./k8s/teardown.sh                # remove o namespace inteiro, incluindo os dados
#   ./k8s/teardown.sh --manter-dados # remove os workloads, preserva os PVCs
#
# Equivalente ao teardown.ps1, para quem nao esta no Windows.

set -uo pipefail

manter_dados=0

while [ $# -gt 0 ]; do
    case "$1" in
        --manter-dados) manter_dados=1; shift ;;
        -h|--help)      sed -n '3,9p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *)              echo "opcao desconhecida: $1" >&2; exit 1 ;;
    esac
done

ns="conexao-solidaria"
ciano=$'\033[36m'; verde=$'\033[32m'; fim=$'\033[0m'

if [ "$manter_dados" -eq 1 ]; then
    printf '%s==> Removendo workloads, preservando os volumes%s\n' "$ciano" "$fim"
    kubectl delete deployment --all -n "$ns" --ignore-not-found
    kubectl delete service --all -n "$ns" --ignore-not-found
    kubectl delete configmap --all -n "$ns" --ignore-not-found
    kubectl delete secret gestorong-secret -n "$ns" --ignore-not-found
else
    printf '%s==> Removendo o namespace inteiro, incluindo os dados%s\n' "$ciano" "$fim"
    kubectl delete namespace "$ns" --ignore-not-found
    kubectl delete clusterrole prometheus-conexao-solidaria --ignore-not-found
    kubectl delete clusterrolebinding prometheus-conexao-solidaria --ignore-not-found
fi

printf '    %sconcluido%s\n' "$verde" "$fim"
