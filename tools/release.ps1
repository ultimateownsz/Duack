# Duack release helper. Run from the Duack repo root.
# Usage:  .\tools\release.ps1 -Version 0.1.0-preview.4
#         .\tools\release.ps1 -Version 0.1.0-preview.4 -DryRun
#
# What it does:
#   1. Verifies the working tree is clean.
#   2. Verifies CHANGELOG.md has a non-empty "## [Unreleased]" section.
#   3. Bumps <Version> in Directory.Build.props to match the requested version.
#   4. Runs dotnet build + dotnet test Duack.slnx (full suite).
#   5. Commits the version bump.
#   6. Creates an annotated tag vX.Y.Z.
#   7. Pushes master + tag to origin (triggers the GitHub Actions release workflow).
#
# The workflow (release.yml) then builds, tests, packs, and publishes to NuGet.
# The guard step verifies the packed .nupkg version matches the tag.

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

# 1. Clean working tree
$status = git status --porcelain
if ($status) {
    Write-Error "Working tree is not clean. Commit or stash first."
    exit 1
}

# 2. Changelog check
$cl = Get-Content CHANGELOG.md -Raw
if ($cl -notmatch '## \[Unreleased\]\s*\r?\n') {
    Write-Error "CHANGELOG.md missing '## [Unreleased]' section."
    exit 1
}
# Check that at least one subsection under [Unreleased] has content
$unreleased = ($cl -split '## \[Unreleased\]')[1] -split '## \[' | Select-Object -First 1
if ($unreleased -notmatch '### (Added|Changed|Fixed|Deprecated|Removed|Security)') {
    Write-Error "CHANGELOG.md [Unreleased] has no categorized entries."
    exit 1
}

# 3. Bump version
$props = "Directory.Build.props"
$propsContent = Get-Content $props -Raw
$newProps = $propsContent -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
if ($newProps -eq $propsContent) {
    Write-Error "Could not find <Version> in $props"
    exit 1
}
Set-Content $props $newProps -NoNewline
Write-Host "Bumped Directory.Build.props -> $Version"

# 4. Build + test
Write-Host "Building..."
dotnet build Duack.slnx
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed."; exit 1 }

Write-Host "Testing..."
dotnet test Duack.slnx
if ($LASTEXITCODE -ne 0) { Write-Error "Tests failed."; exit 1 }

# 5. Commit
if (-not $DryRun) {
    git add Directory.Build.props CHANGELOG.md
    git commit -m "chore: bump version to $Version"
    Write-Host "Committed version bump."

    # 6. Tag
    $tag = "v$Version"
    git tag -a $tag -m "Release $tag"
    Write-Host "Tagged $tag."

    # 7. Push
    git push origin master
    git push origin $tag
    Write-Host "Pushed. GitHub Actions will build/test/pack/publish."
    Write-Host "Monitor: gh run list --limit 3"
    Write-Host "Verify:  https://www.nuget.org/packages/Duack"
} else {
    Write-Host "[DryRun] Skipping commit/tag/push."
    git checkout -- Directory.Build.props
}