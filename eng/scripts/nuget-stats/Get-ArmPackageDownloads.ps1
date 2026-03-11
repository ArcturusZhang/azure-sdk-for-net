<#
.SYNOPSIS
    Fetches all Azure.ResourceManager.* packages from NuGet and generates a markdown
    table sorted by total downloads, with corresponding provisioning libraries.

.DESCRIPTION
    Queries the NuGet V3 Search API to retrieve all packages matching the
    "Azure.ResourceManager." prefix, collects download counts, resolves the
    corresponding Azure.Provisioning.* package(s) for each, and writes a
    markdown report to the specified output path.

    Mapping rules (derived from Library_Inventory.md):
      - Azure.ResourceManager                       -> Azure.Provisioning
      - Azure.ResourceManager.Resources             -> Azure.Provisioning
      - Azure.ResourceManager.Authorization         -> Azure.Provisioning
      - Azure.ResourceManager.ManagedServiceIdentities -> Azure.Provisioning
      - Azure.ResourceManager.XXX                   -> Azure.Provisioning.XXX  (default convention)

.PARAMETER OutputPath
    Path for the generated markdown file. Defaults to "ArmPackageDownloads.md"
    in the same directory as this script.
#>

[CmdletBinding()]
param(
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $OutputPath) {
    $OutputPath = Join-Path $PSScriptRoot "ArmPackageDownloads.md"
}

$baseUrl = "https://azuresearch-usnc.nuget.org/query"
$packagePrefix = "Azure.ResourceManager."
$pageSize = 100
$skip = 0
$allPackages = [System.Collections.Generic.List[PSObject]]::new()

Write-Host "Fetching Azure.ResourceManager.* packages from NuGet..." -ForegroundColor Cyan

# Fetch with prerelease=true to get all packages and their latest version (incl. beta)
do {
    $url = "$baseUrl`?q=$packagePrefix&take=$pageSize&skip=$skip&prerelease=true&semVerLevel=2.0.0"
    Write-Host "  Requesting skip=$skip (incl. prerelease) ..." -NoNewline

    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
    $data = $response.data

    foreach ($pkg in $data) {
        $id = $pkg.id
        if ($id -like "Azure.ResourceManager.*" -or $id -eq "Azure.ResourceManager") {
            $allPackages.Add([PSCustomObject]@{
                Name           = $id
                TotalDownloads = [long]$pkg.totalDownloads
                LatestVersion  = $pkg.version
                StableVersion  = $null
                Description    = ($pkg.description -replace '\r?\n', ' ').Trim()
            })
        }
    }

    Write-Host " got $($data.Count) results (matched $($allPackages.Count) so far)"
    $skip += $pageSize
} while ($data.Count -eq $pageSize)

# Fetch with prerelease=false to get the latest stable version for each package
Write-Host "  Fetching stable versions..." -NoNewline
$armStable = @{}
$skip = 0
do {
    $url = "$baseUrl`?q=$packagePrefix&take=$pageSize&skip=$skip&prerelease=false&semVerLevel=2.0.0"
    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
    foreach ($pkg in $response.data) {
        if ($pkg.id -like "Azure.ResourceManager.*" -or $pkg.id -eq "Azure.ResourceManager") {
            $armStable[$pkg.id] = $pkg.version
        }
    }
    $skip += $pageSize
} while ($response.data.Count -eq $pageSize)
Write-Host " found $($armStable.Count) with stable releases"

foreach ($pkg in $allPackages) {
    if ($armStable.ContainsKey($pkg.Name)) {
        $pkg.StableVersion = $armStable[$pkg.Name]
    }
}

Write-Host "`nTotal packages found: $($allPackages.Count)" -ForegroundColor Green

# Mapping from ARM package to its provisioning library/libraries.
# Exceptions sourced from doc/GeneratorMigration/Library_Inventory.md.
# Default rule: Azure.ResourceManager.XXX -> Azure.Provisioning.XXX
$provisioningOverrides = @{
    "Azure.ResourceManager"                        = @("Azure.Provisioning")
    "Azure.ResourceManager.Resources"              = @("Azure.Provisioning")
    "Azure.ResourceManager.Authorization"          = @("Azure.Provisioning")
    "Azure.ResourceManager.ManagedServiceIdentities" = @("Azure.Provisioning")
}

function Get-ProvisioningNames([string]$armName) {
    if ($provisioningOverrides.ContainsKey($armName)) {
        return $provisioningOverrides[$armName]
    }
    if ($armName -like "Azure.ResourceManager.*") {
        $suffix = $armName.Substring("Azure.ResourceManager.".Length)
        return @("Azure.Provisioning.$suffix")
    }
    return @()
}

# Fetch all Azure.Provisioning.* packages from NuGet (including prerelease) to get version info.
# We query twice: once with prerelease=true (latest version incl. beta) and once with
# prerelease=false (latest stable version). The prerelease query is the superset for existence.
Write-Host "`nFetching Azure.Provisioning.* packages from NuGet (incl. prerelease)..." -ForegroundColor Cyan

# Latest version (including prerelease/beta)
$provLatest = @{}
$provSkip = 0
do {
    $url = "$baseUrl`?q=Azure.Provisioning&take=$pageSize&skip=$provSkip&prerelease=true&semVerLevel=2.0.0"
    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
    foreach ($pkg in $response.data) {
        if ($pkg.id -like "Azure.Provisioning*") {
            $provLatest[$pkg.id] = $pkg.version
        }
    }
    $provSkip += $pageSize
} while ($response.data.Count -eq $pageSize)

# Latest stable version
$provStable = @{}
$provSkip = 0
do {
    $url = "$baseUrl`?q=Azure.Provisioning&take=$pageSize&skip=$provSkip&prerelease=false&semVerLevel=2.0.0"
    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
    foreach ($pkg in $response.data) {
        if ($pkg.id -like "Azure.Provisioning*") {
            $provStable[$pkg.id] = $pkg.version
        }
    }
    $provSkip += $pageSize
} while ($response.data.Count -eq $pageSize)

Write-Host "  Found $($provLatest.Count) provisioning packages ($($provStable.Count) with stable releases)" -ForegroundColor Green

# Sort by total downloads descending
$sorted = $allPackages | Sort-Object -Property TotalDownloads -Descending

# Build markdown
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Azure.ResourceManager.* NuGet Package Downloads")
[void]$sb.AppendLine()
[void]$sb.AppendLine("> Auto-generated on **$(Get-Date -Format 'yyyy-MM-dd HH:mm UTC' -AsUTC)**")
[void]$sb.AppendLine("> Source: [NuGet.org](https://www.nuget.org/)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| # | Package | Version (Stable / Latest) | Total Downloads | Provisioning Library | Prov. Version (Stable / Latest) |")
[void]$sb.AppendLine("|--:|---------|:-------------------------:|----------------:|----------------------|:-------------------------------:|")

# Helper to format a combined stable/latest version cell
function Format-VersionCell([string]$stable, [string]$latest) {
    if (-not $stable -and -not $latest) { return "—" }
    if (-not $stable) { return "— / $latest" }
    if (-not $latest -or $stable -eq $latest) { return $stable }
    return "$stable / $latest"
}

$rank = 0
foreach ($pkg in $sorted) {
    $rank++
    $downloads = $pkg.TotalDownloads.ToString("N0")
    $nugetLink = "[$($pkg.Name)](https://www.nuget.org/packages/$($pkg.Name))"

    # ARM version cell
    $armVersionCell = Format-VersionCell $pkg.StableVersion $pkg.LatestVersion

    $provNames = Get-ProvisioningNames $pkg.Name
    $verifiedLinks = @()
    $provVersionCells = @()
    foreach ($pn in $provNames) {
        if ($provLatest.ContainsKey($pn)) {
            $verifiedLinks += "[$pn](https://www.nuget.org/packages/$pn)"
            $ps = if ($provStable.ContainsKey($pn)) { $provStable[$pn] } else { $null }
            $pl = $provLatest[$pn]
            $provVersionCells += Format-VersionCell $ps $pl
        }
    }
    $provCell = if ($verifiedLinks.Count -gt 0) { $verifiedLinks -join ", " } else { "—" }
    $provVerCell = if ($provVersionCells.Count -gt 0) { $provVersionCells -join ", " } else { "—" }

    [void]$sb.AppendLine("| $rank | $nugetLink | $armVersionCell | $downloads | $provCell | $provVerCell |")
}

[void]$sb.AppendLine()
[void]$sb.AppendLine("**Total packages: $($allPackages.Count)**")
[void]$sb.AppendLine()

$sb.ToString() | Out-File -FilePath $OutputPath -Encoding utf8NoBOM
Write-Host "Markdown report written to: $OutputPath" -ForegroundColor Green
