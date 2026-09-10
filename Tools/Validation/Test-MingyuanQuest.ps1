param(
    [string]$ModRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
if (!$AssemblyPath) { $AssemblyPath = Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$properties = dotnet msbuild (Join-Path $ModRoot 'MiliraXian_NeiyuLaw.csproj') -getProperty:RimWorldManagedDir,AriandelLibraryDll,AlienRaceDll
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve game dependency paths.' }
$paths = ($properties | Out-String | ConvertFrom-Json).Properties
$managed = (Resolve-Path -LiteralPath $paths.RimWorldManagedDir).Path
$harmony = Join-Path $ModRoot 'packages/Lib.Harmony.2.4.2/lib/net48/0Harmony.dll'
$output = Join-Path $ModRoot 'tmp/validation/mingyuan-quest'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $output 'MingyuanQuestRegressionTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$references = @($AssemblyPath, $harmony, (Join-Path $managed 'Assembly-CSharp.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'))
& $compiler /nologo /target:exe "/out:$exe" @($references | ForEach-Object { "/reference:$_" }) (Join-Path $PSScriptRoot 'MingyuanQuestRegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Quest test compilation failed.' }
& $exe (Split-Path $AssemblyPath) $managed (Split-Path $paths.AriandelLibraryDll) (Split-Path $paths.AlienRaceDll) (Split-Path $harmony)
if ($LASTEXITCODE -ne 0) { throw 'Mingyuan quest regression tests failed.' }
