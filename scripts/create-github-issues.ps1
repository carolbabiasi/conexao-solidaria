<#
.SYNOPSIS
    Cria labels, milestones e issues do backlog do hackathon no GitHub.

.DESCRIPTION
    Lê docs/issues/labels.json e docs/issues/issues.json e cria tudo via gh CLI.

    É idempotente: grava docs/issues/.created-map.json com o mapa ID -> número
    da issue. Rodar de novo pula o que já foi criado, então dá para retomar
    depois de uma falha no meio.

    Depois de criar todas as issues, faz um segundo passe substituindo os
    placeholders {{INFRA-01}} pelos números reais (#12), para as dependências
    virarem links clicáveis no GitHub.

.PARAMETER Repo
    Repositório no formato owner/nome. Se omitido, usa o remote do diretório atual.

.PARAMETER DryRun
    Mostra o que seria criado sem chamar a API do GitHub.

.PARAMETER DelayMs
    Pausa entre chamadas de criação, para não bater no rate limit secundário
    do GitHub. Padrão 1500ms.

.EXAMPLE
    .\scripts\create-github-issues.ps1 -Repo minha-org/conexao-solidaria -DryRun

.EXAMPLE
    .\scripts\create-github-issues.ps1 -Repo minha-org/conexao-solidaria
#>
[CmdletBinding()]
param(
    [string]$Repo,
    [switch]$DryRun,
    [int]$DelayMs = 1500
)

$ErrorActionPreference = 'Stop'

$root      = Split-Path -Parent $PSScriptRoot
$issuesDir = Join-Path $root 'docs\issues'
$labelsFile = Join-Path $issuesDir 'labels.json'
$issuesFile = Join-Path $issuesDir 'issues.json'
$mapFile    = Join-Path $issuesDir '.created-map.json'

function Write-Step  { param($m) Write-Host "`n==> $m" -ForegroundColor Cyan }
function Write-Ok    { param($m) Write-Host "    OK  $m" -ForegroundColor Green }
function Write-Skip  { param($m) Write-Host "    --  $m" -ForegroundColor DarkGray }
function Write-Warn2 { param($m) Write-Host "    !!  $m" -ForegroundColor Yellow }

# ---------------------------------------------------------------- pré-requisitos

Write-Step 'Verificando pré-requisitos'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh CLI não encontrado. Instale com: winget install --id GitHub.cli"
}
Write-Ok "gh CLI encontrado"

if (-not $DryRun) {
    gh auth status 2>&1 | Out-Null
    if (-not $?) { throw "gh não autenticado. Rode: gh auth login" }
    Write-Ok "gh autenticado"
}

if (-not (Test-Path $labelsFile)) { throw "Arquivo não encontrado: $labelsFile" }
if (-not (Test-Path $issuesFile)) { throw "Arquivo não encontrado: $issuesFile" }

if (-not $Repo) {
    $Repo = (gh repo view --json nameWithOwner --jq .nameWithOwner 2>$null)
    if (-not $Repo) {
        throw "Não foi possível detectar o repositório. Passe -Repo owner/nome."
    }
}
Write-Ok "Repositório alvo: $Repo"

$labels = Get-Content $labelsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$data   = Get-Content $issuesFile -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Ok "$($labels.Count) labels, $($data.milestones.Count) milestones, $($data.issues.Count) issues"

# mapa de issues já criadas (para retomar execução interrompida)
$created = @{}
if (Test-Path $mapFile) {
    $existing = Get-Content $mapFile -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($p in $existing.PSObject.Properties) { $created[$p.Name] = $p.Value }
    Write-Ok "$($created.Count) issues já criadas em execução anterior"
}

function Save-Map {
    if ($DryRun) { return }
    $obj = [PSCustomObject]$created
    $json = $obj | ConvertTo-Json -Depth 3
    [System.IO.File]::WriteAllText($mapFile, $json, (New-Object System.Text.UTF8Encoding($false)))
}

# ---------------------------------------------------------------------- labels

Write-Step 'Criando labels'

foreach ($l in $labels) {
    if ($DryRun) { Write-Skip "[dry-run] label $($l.name)"; continue }
    # --force torna a operação idempotente (cria ou atualiza)
    gh label create $l.name --repo $Repo --color $l.color --description $l.description --force 2>&1 | Out-Null
    if ($?) { Write-Ok "label $($l.name)" } else { Write-Warn2 "falhou: label $($l.name)" }
}

# ------------------------------------------------------------------ milestones

Write-Step 'Criando milestones'

$existingMilestones = @()
if (-not $DryRun) {
    $raw = gh api "repos/$Repo/milestones?state=all&per_page=100" 2>$null
    if ($?) { $existingMilestones = ($raw | ConvertFrom-Json) | ForEach-Object { $_.title } }
}

foreach ($m in $data.milestones) {
    if ($DryRun) { Write-Skip "[dry-run] milestone $($m.title)"; continue }
    if ($existingMilestones -contains $m.title) { Write-Skip "milestone já existe: $($m.title)"; continue }

    gh api "repos/$Repo/milestones" -f title="$($m.title)" -f description="$($m.description)" 2>&1 | Out-Null
    if ($?) { Write-Ok "milestone $($m.title)" } else { Write-Warn2 "falhou: milestone $($m.title)" }
}

# ---------------------------------------------------------------------- issues

Write-Step "Criando issues ($($data.issues.Count) no total)"

$tmpDir = Join-Path $env:TEMP "gestorong-issues"
if (-not (Test-Path $tmpDir)) { New-Item -ItemType Directory -Path $tmpDir | Out-Null }
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

$i = 0
foreach ($issue in $data.issues) {
    $i++
    $prefix = "[$i/$($data.issues.Count)]"

    if ($created.ContainsKey($issue.id)) {
        Write-Skip "$prefix $($issue.id) já criada (#$($created[$issue.id]))"
        continue
    }

    if ($DryRun) {
        Write-Skip "$prefix [dry-run] $($issue.title)  {$($issue.labels -join ', ')}  [$($issue.milestone)]"
        continue
    }

    $bodyFile = Join-Path $tmpDir "$($issue.id).md"
    [System.IO.File]::WriteAllText($bodyFile, $issue.body, $utf8NoBom)

    $ghArgs = @(
        'issue', 'create',
        '--repo', $Repo,
        '--title', $issue.title,
        '--body-file', $bodyFile,
        '--milestone', $issue.milestone
    )
    foreach ($lab in $issue.labels) { $ghArgs += @('--label', $lab) }

    $out = & gh @ghArgs 2>&1
    $url = ($out | Select-String -Pattern 'https://github\.com/\S+/issues/(\d+)' | Select-Object -Last 1)

    if ($url) {
        $number = [int]$url.Matches[0].Groups[1].Value
        $created[$issue.id] = $number
        Save-Map
        Write-Ok "$prefix $($issue.id) -> #$number"
    }
    else {
        Write-Warn2 "$prefix falhou: $($issue.id)"
        Write-Warn2 "    $out"
    }

    Start-Sleep -Milliseconds $DelayMs
}

# ------------------------------------- segundo passe: resolver as dependências

Write-Step 'Resolvendo links de dependência entre as issues'

if ($DryRun) {
    Write-Skip '[dry-run] segundo passe não executado'
}
else {
    foreach ($issue in $data.issues) {
        if (-not $created.ContainsKey($issue.id)) { continue }
        if ($issue.body -notmatch '\{\{[A-Z]+-\d+\}\}') { continue }

        $body = $issue.body
        foreach ($key in $created.Keys) {
            $body = $body.Replace("{{$key}}", "#$($created[$key])")
        }

        # se sobrou algum placeholder sem correspondência, deixa o ID legível
        $body = [regex]::Replace($body, '\{\{([A-Z]+-\d+)\}\}', '`$1`')

        $bodyFile = Join-Path $tmpDir "$($issue.id).resolved.md"
        [System.IO.File]::WriteAllText($bodyFile, $body, $utf8NoBom)

        gh issue edit $created[$issue.id] --repo $Repo --body-file $bodyFile 2>&1 | Out-Null
        if ($?) { Write-Ok "$($issue.id) (#$($created[$issue.id])) atualizada" }
        else { Write-Warn2 "falhou ao atualizar $($issue.id)" }

        Start-Sleep -Milliseconds $DelayMs
    }
}

# ----------------------------------------------------------------------- fim

Write-Step 'Concluído'
Write-Host "    Issues criadas: $($created.Count)/$($data.issues.Count)"
if (-not $DryRun) {
    Write-Host "    Mapa salvo em: $mapFile"
    Write-Host "    Ver em: https://github.com/$Repo/issues"
}
