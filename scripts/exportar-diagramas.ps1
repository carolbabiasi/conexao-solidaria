<#
.SYNOPSIS
    Reexporta os diagramas de docs/diagramas de SVG para PNG.

.DESCRIPTION
    O SVG e a fonte versionada; o PNG existe para o relatorio de entrega e para
    os slides, onde SVG nem sempre e aceito. Rode este script sempre que editar
    um .svg, para os dois nao divergirem.

    A conversao roda em container: nao exige instalar librsvg nem fontes na
    maquina. As fontes vao dentro do container de proposito - sem elas o
    rsvg-convert desenha caixas vazias no lugar do texto.

.PARAMETER Largura
    Largura do PNG em pixels. O padrao de 2560 imprime bem em A4 paisagem.

.EXAMPLE
    .\scripts\exportar-diagramas.ps1
#>
[CmdletBinding()]
param([int]$Largura = 2560)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$pasta = Join-Path $raiz 'docs/diagramas'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "docker nao encontrado no PATH." -ForegroundColor Red
    exit 1
}

$svgs = Get-ChildItem -Path $pasta -Filter *.svg
if ($svgs.Count -eq 0) {
    Write-Host "Nenhum .svg em $pasta" -ForegroundColor Yellow
    exit 0
}

$comandos = $svgs | ForEach-Object {
    $png = [IO.Path]::ChangeExtension($_.Name, '.png')
    "rsvg-convert -w $Largura -b white '$($_.Name)' -o '$png' && echo '  OK  $png'"
}

$script = @(
    'apk add --no-cache rsvg-convert ttf-liberation font-dejavu fontconfig > /dev/null 2>&1'
    ($comandos -join '; ')
) -join '; '

Write-Host "==> Exportando $($svgs.Count) diagrama(s) a $Largura px" -ForegroundColor Cyan

docker run --rm -v "${pasta}:/work" -w /work alpine:3.21 sh -c $script

if ($LASTEXITCODE -ne 0) {
    Write-Host "Falha na exportacao." -ForegroundColor Red
    exit 1
}
