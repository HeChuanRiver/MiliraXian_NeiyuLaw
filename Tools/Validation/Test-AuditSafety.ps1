param(
    [string]$ModRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
if (!$AssemblyPath) { $AssemblyPath = Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$properties = dotnet msbuild (Join-Path $ModRoot 'MiliraXian_NeiyuLaw.csproj') -getProperty:RimWorldManagedDir,AriandelLibraryDll,AlienRaceDll
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve local assembly paths.' }
$paths = ($properties | Out-String | ConvertFrom-Json).Properties
$managed = (Resolve-Path -LiteralPath $paths.RimWorldManagedDir).Path
$output = Join-Path $ModRoot 'tmp/validation/audit-safety'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $output 'AuditSafetyRegressionTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$references = @($AssemblyPath, (Join-Path $managed 'Assembly-CSharp.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'))
& $compiler /nologo /target:exe "/out:$exe" @($references | ForEach-Object { "/reference:$_" }) (Join-Path $PSScriptRoot 'AuditSafetyRegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Audit regression test compilation failed.' }
# RimWorld's current SimplePool calls Queue.TryDequeue, which desktop .NET
# Framework lacks. Run the managed fixture on an installed modern .NET runtime.
$runtime = dotnet --list-runtimes | ForEach-Object {
    if ($_ -match '^Microsoft.NETCore.App (\d+\.\d+\.\d+) ') { [version]$Matches[1] }
} | Sort-Object -Descending | Select-Object -First 1
if (!$runtime) { throw 'A modern Microsoft.NETCore.App runtime is required.' }
$runtimeConfig = Join-Path $output 'AuditSafetyRegressionTests.runtimeconfig.json'
@{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.ToString() } } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $runtimeConfig -Encoding UTF8
$assemblyDirectories = @((Split-Path $AssemblyPath), $managed, (Split-Path $paths.AriandelLibraryDll), (Split-Path $paths.AlienRaceDll), (Join-Path $ModRoot 'packages/Lib.Harmony.2.4.2/lib/net48'))
dotnet $exe --snapshots @assemblyDirectories
if ($LASTEXITCODE -ne 0) { throw 'Snapshot regression tests failed.' }
# The shipped net48 Harmony build uses Framework-specific Reflection.Emit APIs.
& $exe --framework @assemblyDirectories
if ($LASTEXITCODE -ne 0) { throw 'Audit regression tests failed.' }
