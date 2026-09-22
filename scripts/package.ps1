param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0-preview.3"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
$publish = Join-Path $root "artifacts\publish\win-x64"
$artifactDir = Join-Path $root "artifacts"
$zip = Join-Path $artifactDir "TextEntryAssistant-$Version-win-x64.zip"

Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zip -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publish | Out-Null
New-Item -ItemType Directory -Force $artifactDir | Out-Null

& $dotnet publish (Join-Path $root "src\TextEntryAssistant.App\TextEntryAssistant.App.csproj") `
    --configuration $Configuration --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version -o $publish --nologo

# PDB files can contain developer machine paths and are not needed by end users.
Remove-Item (Join-Path $publish "*.pdb") -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$zip.sha256" -Value "$hash  $(Split-Path $zip -Leaf)" -NoNewline
Write-Host "Package: $zip"
Write-Host "SHA-256: $hash"
