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
      - Azure.ResourceManager                      -> Azure.Provisioning, Azure.Provisioning.Deployment
      - Azure.ResourceManager.Resources             -> Azure.Provisioning, Azure.Provisioning.Deployment
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

do {
    $url = "$baseUrl`?q=$packagePrefix&take=$pageSize&skip=$skip&prerelease=false&semVerLevel=2.0.0"
    Write-Host "  Requesting skip=$skip ..." -NoNewline

    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
    $data = $response.data

    foreach ($pkg in $data) {
        $id = $pkg.id
        if ($id -like "Azure.ResourceManager.*" -or $id -eq "Azure.ResourceManager") {
            $allPackages.Add([PSCustomObject]@{
                Name           = $id
                TotalDownloads = [long]$pkg.totalDownloads
                Version        = $pkg.version
                Description    = ($pkg.description -replace '\r?\n', ' ').Trim()
            })
        }
    }

    Write-Host " got $($data.Count) results (matched $($allPackages.Count) so far)"
    $skip += $pageSize
} while ($data.Count -eq $pageSize)

Write-Host "`nTotal packages found: $($allPackages.Count)" -ForegroundColor Green

# Mapping from ARM package to its provisioning library/libraries.
# Exceptions sourced from doc/GeneratorMigration/Library_Inventory.md.
# Default rule: Azure.ResourceManager.XXX -> Azure.Provisioning.XXX
$provisioningOverrides = @{
    "Azure.ResourceManager"                        = @("Azure.Provisioning", "Azure.Provisioning.Deployment")
    "Azure.ResourceManager.Resources"              = @("Azure.Provisioning", "Azure.Provisioning.Deployment")
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

# Fetch all Azure.Provisioning.* packages from NuGet to verify existence
Write-Host "`nVerifying Azure.Provisioning.* packages on NuGet..." -ForegroundColor Cyan
$provisioningExists = @{}
$provSkip = 0
do {
    $url = "$baseUrl`?q=Azure.Provisioning&take=$pageSize&skip=$provSkip&prerelease=false&semVerLevel=2.0.0"
    $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
    foreach ($pkg in $response.data) {
        if ($pkg.id -like "Azure.Provisioning*") {
            $provisioningExists[$pkg.id] = $true
        }
    }
    $provSkip += $pageSize
} while ($response.data.Count -eq $pageSize)
Write-Host "  Found $($provisioningExists.Count) provisioning packages on NuGet" -ForegroundColor Green

# Sort by total downloads descending
$sorted = $allPackages | Sort-Object -Property TotalDownloads -Descending

# Build markdown
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# Azure.ResourceManager.* NuGet Package Downloads")
[void]$sb.AppendLine()
[void]$sb.AppendLine("> Auto-generated on **$(Get-Date -Format 'yyyy-MM-dd HH:mm UTC' -AsUTC)**")
[void]$sb.AppendLine("> Source: [NuGet.org](https://www.nuget.org/)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| # | Package | Latest Version | Total Downloads | Provisioning Library |")
[void]$sb.AppendLine("|--:|---------|---------------|----------------:|----------------------|")

$rank = 0
foreach ($pkg in $sorted) {
    $rank++
    $downloads = $pkg.TotalDownloads.ToString("N0")
    $nugetLink = "[$($pkg.Name)](https://www.nuget.org/packages/$($pkg.Name))"

    $provNames = Get-ProvisioningNames $pkg.Name
    $verifiedLinks = @()
    foreach ($pn in $provNames) {
        if ($provisioningExists.ContainsKey($pn)) {
            $verifiedLinks += "[$pn](https://www.nuget.org/packages/$pn)"
        }
    }
    $provCell = if ($verifiedLinks.Count -gt 0) { $verifiedLinks -join ", " } else { "—" }

    [void]$sb.AppendLine("| $rank | $nugetLink | $($pkg.Version) | $downloads | $provCell |")
}

[void]$sb.AppendLine()
[void]$sb.AppendLine("**Total packages: $($allPackages.Count)**")
[void]$sb.AppendLine()

$sb.ToString() | Out-File -FilePath $OutputPath -Encoding utf8NoBOM
Write-Host "Markdown report written to: $OutputPath" -ForegroundColor Green
