<#
.SYNOPSIS
    Reexporta os documentos de docs/ de Markdown para PDF.

.DESCRIPTION
    O .md e a fonte versionada: renderiza no GitHub e diffa em texto. O PDF
    existe porque o edital pede os entregaveis em PDF. Rode este script sempre
    que editar um .md exportavel, para os dois nao divergirem.

    A conversao roda em container, com pandoc e LaTeX: nao exige instalar
    nenhum dos dois na maquina.

.PARAMETER Documento
    Nome do arquivo em docs/ a converter. O padrao cobre os entregaveis atuais.

.EXAMPLE
    .\scripts\exportar-documentos.ps1
#>
[CmdletBinding()]
param([string[]]$Documento = @('escolha-dos-bancos.md'))

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$pasta = Join-Path $raiz 'docs'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "docker nao encontrado no PATH." -ForegroundColor Red
    exit 1
}

foreach ($nome in $Documento) {
    $origem = Join-Path $pasta $nome

    if (-not (Test-Path $origem)) {
        Write-Host "    !!  $nome nao encontrado em docs/" -ForegroundColor Yellow
        continue
    }

    $pdf = [IO.Path]::ChangeExtension($nome, '.pdf')
    Write-Host "==> $nome -> $pdf" -ForegroundColor Cyan

    docker run --rm -v "${pasta}:/data" pandoc/latex:latest `
        $nome -o $pdf `
        --toc `
        -V geometry:margin=2.5cm `
        -V linkcolor=blue `
        -V fontsize=11pt

    if ($LASTEXITCODE -ne 0) {
        Write-Host "    XX  falha ao converter $nome" -ForegroundColor Red
        exit 1
    }

    Write-Host "    OK  $pdf" -ForegroundColor Green
}
