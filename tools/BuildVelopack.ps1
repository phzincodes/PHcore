param(
    [string]$Dotnet = 'dotnet',
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.3.0-preview.2'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$publish = Join-Path $root "artifacts/desktop-velopack/$Version"
$output = Join-Path $root "artifacts/velopack/$Version"

Push-Location $root
try {
    & $Dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao restaurar o CLI vpk.' }

    & $Dotnet publish src/PHsystem.Desktop -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'Falha no publish do aplicativo.' }

    $target = @()
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { $target = @('[win]') }
    & $Dotnet tool run vpk -- @target pack --runtime win-x64 --packId PHsystem.Next --packTitle 'PHsystem Next' --packVersion $Version --packDir $publish --mainExe PHsystem.Desktop.exe --outputDir $output
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao empacotar com o Velopack.' }

    $installer = Get-ChildItem -LiteralPath $output -Filter '*Setup.exe' -File | Select-Object -First 1
    if ($null -eq $installer) { throw 'O CLI terminou sem gerar o instalador esperado.' }
    $hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($installer.Name)" | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
    Write-Output $installer.FullName
}
finally { Pop-Location }
