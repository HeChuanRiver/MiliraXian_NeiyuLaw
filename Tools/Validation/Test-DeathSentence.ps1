param(
    [string]$ModRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
if (!$AssemblyPath) { $AssemblyPath = Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$properties = dotnet msbuild (Join-Path $ModRoot 'MiliraXian_NeiyuLaw.csproj') -getProperty:RimWorldManagedDir,AriandelLibraryDll
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve local game and AL paths.' }
$paths = ($properties | Out-String | ConvertFrom-Json).Properties
$managed = (Resolve-Path -LiteralPath $paths.RimWorldManagedDir).Path
$al = (Resolve-Path -LiteralPath $paths.AriandelLibraryDll).Path

# Verify the data path connecting the field, accumulation stat and production part.
[xml]$stats = Get-Content -Raw -LiteralPath (Join-Path $ModRoot '1.6/Defs/StatDefs/MiliraXian_AbnormalStats.xml')
$part = $stats.SelectSingleNode("/Defs/StatDef[defName='MX_AbnormalDeathSentenceLimitFactor']/parts/li")
if ($part.Class -ne 'MiliraXian.Characters.Zhaoli.StatPart_ZhaoliDeathSentenceImmunity') { throw 'Death sentence immunity stat part is not wired.' }
[xml]$defs = Get-Content -Raw -LiteralPath (Join-Path $ModRoot '1.6/Defs/HediffDefs/MiliraXian_Zhaoli_DeathField.xml')
$abnormal = $defs.SelectSingleNode("/Defs/*[defName='MX_AbnormalDeathSentence']")
if ($abnormal.accumulationLimitFactorStat -ne 'MX_AbnormalDeathSentenceLimitFactor') { throw 'Death sentence does not use the immunity stat.' }
if ($abnormal.baseAccumulationLimit -ne '9') { throw 'Ordinary target threshold changed.' }
if (!$abnormal.SelectSingleNode("comps/li[@Class='MiliraXian.Characters.Zhaoli.HediffCompProperties_ZhaoliDeathSentence']")) { throw 'Legacy accumulation cleanup comp missing.' }

$output = Join-Path $ModRoot 'tmp/validation/death-sentence'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $output 'DeathSentenceRegressionTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$references = @($AssemblyPath, (Join-Path $managed 'Assembly-CSharp.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'))
& $compiler /nologo /target:exe "/out:$exe" @($references | ForEach-Object { "/reference:$_" }) (Join-Path $PSScriptRoot 'DeathSentenceRegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Death sentence test compilation failed.' }
& $exe (Split-Path $AssemblyPath) $managed (Split-Path $al) (Join-Path $ModRoot 'packages/Lib.Harmony.2.4.2/lib/net48')
if ($LASTEXITCODE -ne 0) { throw 'Death sentence regression tests failed.' }
Write-Output "AL compatibility baseline: $al"
Get-FileHash -LiteralPath $al -Algorithm SHA256 | Select-Object Algorithm, Hash
