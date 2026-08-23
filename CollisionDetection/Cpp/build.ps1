[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$buildDirectory = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer vswhere.exe was not found.'
}
$visualStudio = & $vswhere -latest -products * `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath
if (-not $visualStudio) {
    throw 'A Visual Studio installation with the x64 C++ tools was not found.'
}
$developerShell = Join-Path $visualStudio 'Common7\Tools\VsDevCmd.bat'

$optimization = if ($Configuration -eq 'Release') { '/O2 /DNDEBUG' } else { '/Od /Zi' }
$common = "/nologo /std:c++20 /EHsc /W4 /permissive- /fp:strict $optimization " +
          "/I`"$projectRoot\include`""
$sources = @(
    "$projectRoot\src\exact_predicates.cpp",
    "$projectRoot\src\linear_index.cpp",
    "$projectRoot\src\bvh_index.cpp",
    "$projectRoot\src\grid_index.cpp"
)
$quotedSources = ($sources | ForEach-Object { "`"$_`"" }) -join ' '

Push-Location $buildDirectory
try {
    foreach ($target in @(
        @{ Name = 'collision_tests'; Source = "$projectRoot\tests\test_collision.cpp" },
        @{ Name = 'collision_benchmark'; Source = "$projectRoot\bench\benchmark.cpp" }
    )) {
        if (-not (Test-Path -LiteralPath $target.Source)) {
            Write-Verbose "Skipping $($target.Name); source is not present yet."
            continue
        }
        $output = Join-Path $buildDirectory ($target.Name + '.exe')
        $pdb = Join-Path $buildDirectory ($target.Name + '.pdb')
        $command = "`"$developerShell`" -arch=x64 -host_arch=x64 >nul && " +
                   "cl.exe $common $quotedSources `"$($target.Source)`" " +
                   "/Fe:`"$output`" /Fd:`"$pdb`""
        & $env:ComSpec /d /s /c $command
        if ($LASTEXITCODE -ne 0) {
            throw "Compilation failed for $($target.Name) with exit code $LASTEXITCODE."
        }
    }
} finally {
    Pop-Location
}
