param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath,

    [AllowEmptyString()]
    [string]$BasePath = '/beto-s-last-chance/'
)

$ErrorActionPreference = 'Stop'
$indexPath = Join-Path $PublishPath 'index.html'
$html = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $indexPath).Path)
$originalBase = '<base href="/" />'
if (-not $html.Contains($originalBase)) {
    throw 'No se encontró la ruta base esperada en el index.html publicado.'
}

$normalizedPath = '/' + $BasePath.Trim('/')
if ($normalizedPath -ne '/') { $normalizedPath += '/' }
$escapedPath = [System.Net.WebUtility]::HtmlEncode($normalizedPath)
$html = $html.Replace($originalBase, '<base href="' + $escapedPath + '" />')
[System.IO.File]::WriteAllText((Resolve-Path -LiteralPath $indexPath).Path, $html, [System.Text.UTF8Encoding]::new($false))

# El HTML comprimido anterior todavía contiene la ruta de desarrollo.
foreach ($extension in @('.br', '.gz')) {
    $compressedIndex = $indexPath + $extension
    if (Test-Path -LiteralPath $compressedIndex) { Remove-Item -LiteralPath $compressedIndex }
}
New-Item -ItemType File -Path (Join-Path $PublishPath '.nojekyll') -Force | Out-Null
Write-Output "Ruta base publicada: $normalizedPath"
