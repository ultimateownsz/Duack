param(
    [string]$SampleRoot = "samples/Duack.Sample.Desktop",
    [switch]$InPlace
)

$masters = Join-Path $SampleRoot "_masters"
if (-not (Test-Path $masters)) {
    Write-Error "masters directory not found: $masters"
    exit 1
}

if ($InPlace) {
    $target = Join-Path $env:TEMP ("duack-normalized-" + [Guid]::NewGuid().ToString("N"))
    & dotnet run --project tools/DuackCli -- normalize $masters $target
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Get-ChildItem $target -Filter *.wav | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $masters $_.Name) -Force
    }
    Remove-Item $target -Recurse -Force
    Write-Host "Normalized masters in place: $masters"
} else {
    $out = Join-Path $SampleRoot "_masters_normalized"
    & dotnet run --project tools/DuackCli -- normalize $masters $out
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host ""
    Write-Host "Next: convert the normalized masters:"
    Write-Host "  dotnet run --project tools/DuackCli -- convert $out $SampleRoot/Content/Audio/Tracks --all"
}

