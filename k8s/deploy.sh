#!/usr/bin/env bash
#
# Sobe a Conexao Solidaria inteira em um cluster Kubernetes local.
#
# Aplica os manifests na ordem correta, aguardando cada camada ficar pronta
# antes de seguir. Constroi as imagens localmente por padrao, o que evita
# depender do GHCR. Em cluster kind, carrega as imagens com kind load.
#
# Uso:
#   ./k8s/deploy.sh
#   ./k8s/deploy.sh --pular-build
#   ./k8s/deploy.sh --senha "outra-senha"
#
# Equivalente ao deploy.ps1, para quem nao esta no Windows.

set -uo pipefail

pular_build=0
senha="devlocal123"

while [ $# -gt 0 ]; do
    case "$1" in
        --pular-build) pular_build=1; shift ;;
        --senha)       senha="${2:?--senha exige um valor}"; shift 2 ;;
        -h|--help)     sed -n '3,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *)             echo "opcao desconhecida: $1" >&2; exit 1 ;;
    esac
done

raiz="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ns="conexao-solidaria"
imagem_api="ghcr.io/carolbabiasi/conexao-solidaria-api:latest"
imagem_worker="ghcr.io/carolbabiasi/conexao-solidaria-worker:latest"

ciano=$'\033[36m'; verde=$'\033[32m'; amarelo=$'\033[33m'; vermelho=$'\033[31m'; fim=$'\033[0m'

passo() { printf '\n%s==> %s%s\n' "$ciano" "$1" "$fim"; }
ok()    { printf '    %sOK  %s%s\n' "$verde" "$1" "$fim"; }
aviso() { printf '    %s!!  %s%s\n' "$amarelo" "$1" "$fim"; }
falha() { printf '    %sXX  %s%s\n' "$vermelho" "$1" "$fim"; exit 1; }

passo "Verificando pre-requisitos"

for cmd in kubectl docker; do
    command -v "$cmd" > /dev/null 2>&1 || falha "$cmd nao encontrado no PATH."
done
ok "kubectl e docker encontrados"

conectou=0
for _ in 1 2 3 4 5; do
    if kubectl get nodes > /dev/null 2>&1; then conectou=1; break; fi
    sleep 3
done
[ "$conectou" -eq 1 ] || falha "Nenhum cluster Kubernetes acessivel. Crie um com: kind create cluster --name conexao-solidaria"

contexto="$(kubectl config current-context 2>/dev/null | tr -d '[:space:]')"
ok "cluster acessivel: $contexto"

if [ "$pular_build" -eq 0 ]; then
    passo "Construindo as imagens"

    docker build -q -f "$raiz/src/GestorONG.API/Dockerfile" -t "$imagem_api" "$raiz" > /dev/null 2>&1 \
        || falha "falha ao construir a imagem da API"
    ok "imagem da API"

    docker build -q -f "$raiz/src/GestorONG.Worker/Dockerfile" -t "$imagem_worker" "$raiz" > /dev/null 2>&1 \
        || falha "falha ao construir a imagem do Worker"
    ok "imagem do Worker"

    case "$contexto" in
        kind-*)
            command -v kind > /dev/null 2>&1 \
                || falha "contexto kind detectado mas o comando 'kind' nao esta no PATH."

            nome_do_cluster="${contexto#kind-}"
            passo "Carregando as imagens no cluster kind"

            kind load docker-image "$imagem_api" --name "$nome_do_cluster" > /dev/null 2>&1 \
                || falha "falha ao carregar a imagem da API no kind"
            ok "API carregada"

            kind load docker-image "$imagem_worker" --name "$nome_do_cluster" > /dev/null 2>&1 \
                || falha "falha ao carregar a imagem do Worker no kind"
            ok "Worker carregado"
            ;;
    esac
fi

passo "Namespace"
kubectl apply -f "$raiz/k8s/base/00-namespace.yaml" > /dev/null 2>&1
ok "namespace $ns"

passo "ConfigMap e Secret"
kubectl apply -f "$raiz/k8s/base/01-configmap.yaml" > /dev/null 2>&1
ok "ConfigMap"

chave_jwt="$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 48)"

kubectl create secret generic gestorong-secret \
    --namespace "$ns" \
    --from-literal=POSTGRES_USER=gestorong \
    --from-literal=POSTGRES_PASSWORD="$senha" \
    --from-literal=POSTGRES_DB=conexaosolidaria \
    --from-literal=MONGO_INITDB_ROOT_USERNAME=gestorong \
    --from-literal=MONGO_INITDB_ROOT_PASSWORD="$senha" \
    --from-literal="ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=conexaosolidaria;Username=gestorong;Password=$senha" \
    --from-literal="Mongo__ConnectionString=mongodb://gestorong:$senha@mongo:27017/?authSource=admin" \
    --from-literal=RABBITMQ_DEFAULT_USER=gestorong \
    --from-literal=RABBITMQ_DEFAULT_PASS="$senha" \
    --from-literal=RabbitMq__Usuario=gestorong \
    --from-literal=RabbitMq__Senha="$senha" \
    --from-literal=Jwt__ChaveSecreta="$chave_jwt" \
    --from-literal=Seed__GestorSenha="$senha" \
    --from-literal=GF_SECURITY_ADMIN_USER=admin \
    --from-literal=GF_SECURITY_ADMIN_PASSWORD="$senha" \
    --dry-run=client -o yaml 2>/dev/null | kubectl apply -f - > /dev/null 2>&1
ok "Secret (chave JWT gerada aleatoriamente)"

passo "Dependencias: PostgreSQL, MongoDB e RabbitMQ"
kubectl apply -f "$raiz/k8s/dependencias/" > /dev/null 2>&1
ok "manifests aplicados"

printf '    aguardando ficarem prontos (pode levar alguns minutos)...\n'
for dep in postgres mongo rabbitmq; do
    if kubectl rollout status "deployment/$dep" -n "$ns" --timeout=300s > /dev/null 2>&1; then
        ok "$dep"
    else
        aviso "$dep nao ficou pronto no tempo esperado"
    fi
done

passo "Observabilidade: Prometheus e Grafana"
kubectl create configmap grafana-dashboards \
    --namespace "$ns" \
    --from-file="$raiz/k8s/observabilidade/dashboards/" \
    --dry-run=client -o yaml 2>/dev/null | kubectl apply -f - > /dev/null 2>&1
ok "ConfigMap do dashboard"

kubectl apply -f "$raiz/k8s/observabilidade/30-prometheus.yaml" > /dev/null 2>&1
kubectl apply -f "$raiz/k8s/observabilidade/31-grafana.yaml" > /dev/null 2>&1
for dep in prometheus grafana; do
    if kubectl rollout status "deployment/$dep" -n "$ns" --timeout=300s > /dev/null 2>&1; then
        ok "$dep"
    else
        aviso "$dep nao ficou pronto no tempo esperado"
    fi
done

passo "Aplicacao: API e Worker"
kubectl apply -f "$raiz/k8s/base/20-api.yaml" > /dev/null 2>&1
kubectl apply -f "$raiz/k8s/base/21-worker.yaml" > /dev/null 2>&1
for dep in gestorong-api gestorong-worker; do
    if kubectl rollout status "deployment/$dep" -n "$ns" --timeout=300s > /dev/null 2>&1; then
        ok "$dep"
    else
        aviso "$dep nao ficou pronto no tempo esperado"
    fi
done

passo "Pronto"
kubectl get pods -n "$ns"

cat <<FIM

Para acessar as interfaces, abra um terminal para cada:

  kubectl port-forward -n $ns svc/gestorong-api 8080:80
  kubectl port-forward -n $ns svc/rabbitmq 15672:15672
  kubectl port-forward -n $ns svc/grafana 3000:3000
  kubectl port-forward -n $ns svc/prometheus 9090:9090

  API .......... http://localhost:8080/swagger
  Painel ....... http://localhost:8080/api/v1/publico/campanhas
  RabbitMQ ..... http://localhost:15672   (gestorong / $senha)
  Grafana ...... http://localhost:3000    (admin / $senha)
  Prometheus ... http://localhost:9090

  Gestor ....... gestor@esperancasolidaria.org / $senha
FIM
