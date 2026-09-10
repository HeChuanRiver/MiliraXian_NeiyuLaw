param([string]$ModRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
$assemblyPath = Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll'
$properties = dotnet msbuild (Join-Path $ModRoot 'MiliraXian_NeiyuLaw.csproj') -getProperty:RimWorldManagedDir,AriandelLibraryDll,AlienRaceDll
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve local assembly paths.' }
$paths = ($properties | Out-String | ConvertFrom-Json).Properties
$managed = (Resolve-Path -LiteralPath $paths.RimWorldManagedDir).Path
$output = Join-Path $ModRoot 'tmp/validation/qinghe-experience'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $output 'QingheExperienceRegressionTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$references = @($assemblyPath, (Join-Path $managed 'Assembly-CSharp.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'))
& $compiler /nologo /target:exe "/out:$exe" @($references | ForEach-Object { "/reference:$_" }) (Join-Path $PSScriptRoot 'QingheExperienceRegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Experience regression test compilation failed.' }
$runtime = dotnet --list-runtimes | ForEach-Object {
    if ($_ -match '^Microsoft.NETCore.App (\d+\.\d+\.\d+) ') { [version]$Matches[1] }
} | Sort-Object -Descending | Select-Object -First 1
if (!$runtime) { throw 'A modern Microsoft.NETCore.App runtime is required.' }
@{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.ToString() } } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'QingheExperienceRegressionTests.runtimeconfig.json') -Encoding UTF8
$assemblyDirectories = @((Split-Path $assemblyPath), $managed, (Split-Path $paths.AriandelLibraryDll), (Split-Path $paths.AlienRaceDll), (Join-Path $ModRoot 'packages/Lib.Harmony.2.4.2/lib/net48'))
dotnet $exe @assemblyDirectories
if ($LASTEXITCODE -ne 0) { throw 'Experience regression tests failed.' }
