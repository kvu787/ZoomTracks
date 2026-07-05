# Read-UnityPrefs.ps1
# Reads Unity PlayerPrefs from:
# HKCU:\Software\<CompanyName>\<ProductName>

param(
    [Parameter(Mandatory = $true)]
    [string]$CompanyName,

    [Parameter(Mandatory = $true)]
    [string]$ProductName
)

$regPath = "HKCU:\Software\$CompanyName\$ProductName"

if (-not (Test-Path $regPath)) {
    Write-Host "Registry key not found:"
    Write-Host $regPath
    exit 1
}

Write-Host "Reading Unity registry key:"
Write-Host $regPath
Write-Host ""

$props = Get-ItemProperty -Path $regPath

$props.PSObject.Properties |
    Where-Object {
        $_.Name -notin @(
            "PSPath",
            "PSParentPath",
            "PSChildName",
            "PSDrive",
            "PSProvider"
        )
    } |
    Sort-Object Name |
    Format-Table Name, Value -AutoSize
