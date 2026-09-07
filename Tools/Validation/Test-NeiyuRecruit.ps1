param(
    [string]$ModRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
if (!$AssemblyPath) { $AssemblyPath = Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$properties = dotnet msbuild (Join-Path $ModRoot 'MiliraXian_NeiyuLaw.csproj') -getProperty:RimWorldManagedDir,AriandelLibraryDll,AlienRaceDll
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve local game and dependency paths.' }
$paths = ($properties | Out-String | ConvertFrom-Json).Properties
$managed = (Resolve-Path -LiteralPath $paths.RimWorldManagedDir).Path

# Check that the persisted scenario data really declares the required starting kind.
[xml]$scenario = Get-Content -Raw -LiteralPath (Join-Path $ModRoot '1.6/Defs/ScenarioDefs/MiliraXian_Neiyu_Scenarios.xml')
$required = $scenario.SelectSingleNode("/Defs/ScenarioDef[defName='MXNL_NeiyuProjectionStart']/scenario/parts/li[@Class='ScenPart_ConfigPage_ConfigureStartingPawns_KindDefs']/kindCounts/li[kindDef='MiliraXian_Neiyu' and requiredAtStart='true']")
if (!$required -or [int]$required.count -le 0) { throw 'Neiyu scenario must require a starting Neiyu pawn.' }
[xml]$quest = Get-Content -Raw -LiteralPath (Join-Path $ModRoot '1.6/Defs/QuestScriptDefs/MiliraXian_Neiyu_QuestScripts.xml')
if ($quest.Defs.QuestScriptDef.root.Class -ne 'MiliraXian.Characters.Neiyu.QuestNode_Root_NeiyuProjectionRecruit_AvailableQuest') { throw 'Recruit quest root changed; update regression coverage.' }

$output = Join-Path $ModRoot 'tmp/validation/neiyu-recruit'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $output 'NeiyuRecruitRegressionTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$references = @($AssemblyPath, (Join-Path $managed 'Assembly-CSharp.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'))
& $compiler /nologo /target:exe "/out:$exe" @($references | ForEach-Object { "/reference:$_" }) (Join-Path $PSScriptRoot 'NeiyuRecruitRegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Recruitment test compilation failed.' }
& $exe (Split-Path $AssemblyPath) $managed (Split-Path $paths.AriandelLibraryDll) (Split-Path $paths.AlienRaceDll) (Join-Path $ModRoot 'packages/Lib.Harmony.2.4.2/lib/net48')
if ($LASTEXITCODE -ne 0) { throw 'Neiyu recruitment regression tests failed.' }
