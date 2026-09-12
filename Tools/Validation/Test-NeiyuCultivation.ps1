param([string]$ModRoot = (Join-Path $PSScriptRoot '../..'), [string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
if (!$AssemblyPath) { $AssemblyPath = Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$properties = dotnet msbuild (Join-Path $ModRoot 'MiliraXian_NeiyuLaw.csproj') -getProperty:RimWorldManagedDir,AriandelLibraryDll,AlienRaceDll
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve dependencies.' }
$paths = ($properties | Out-String | ConvertFrom-Json).Properties
$managed = (Resolve-Path -LiteralPath $paths.RimWorldManagedDir).Path
$output = Join-Path $ModRoot 'tmp/validation/neiyu-cultivation'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$exe = Join-Path $output 'NeiyuCultivationRegressionTests.exe'
$sdk = dotnet --version
$compiler = Join-Path (Split-Path (Get-Command dotnet).Source) "sdk/$sdk/Roslyn/bincore/csc.dll"
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$refs = @($AssemblyPath, (Join-Path $managed 'Assembly-CSharp.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $framework 'mscorlib.dll'), (Join-Path $framework 'System.dll'), (Join-Path $framework 'System.Core.dll'), (Join-Path $framework 'System.Xml.dll'),
    (Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8.1/Facades/netstandard.dll'))
dotnet $compiler /nologo /nostdlib+ /target:exe "/out:$exe" @($refs | ForEach-Object { "/reference:$_" }) (Join-Path $PSScriptRoot 'NeiyuCultivationRegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Cultivation test compilation failed.' }
$assemblyDirectories = @((Split-Path $AssemblyPath), $managed, (Split-Path $paths.AriandelLibraryDll), (Split-Path $paths.AlienRaceDll), (Join-Path $ModRoot 'packages/Lib.Harmony.2.4.2/lib/net48'))
& $exe @assemblyDirectories
if ($LASTEXITCODE -ne 0) { throw 'Cultivation regression tests failed.' }
$runtime = dotnet --list-runtimes | ForEach-Object {
    if ($_ -match '^Microsoft.NETCore.App (\d+\.\d+\.\d+) ') { [version]$Matches[1] }
} | Sort-Object -Descending | Select-Object -First 1
if (!$runtime) { throw 'A modern .NET runtime is required for the game XML reader.' }
@{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.ToString() } } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'NeiyuCultivationRegressionTests.runtimeconfig.json') -Encoding UTF8
dotnet $exe --xml (Join-Path $ModRoot '1.6/Defs/CultivationDefs/MiliraXian_Neiyu_Cultivation.xml') @assemblyDirectories
if ($LASTEXITCODE -ne 0) { throw 'Native XML reader tests failed.' }

[xml]$xml = Get-Content -Raw -LiteralPath (Join-Path $ModRoot '1.6/Defs/CultivationDefs/MiliraXian_Neiyu_Cultivation.xml')
$nodes = @($xml.Defs.ChildNodes | Where-Object NodeType -EQ Element)
if ($nodes.Count -ne 12) { throw 'Expected twelve cultivation nodes.' }
$byName = @{}
foreach ($node in $nodes) {
    if ($byName.ContainsKey([string]$node.defName)) { throw 'Duplicate cultivation node.' }
    $byName[[string]$node.defName] = $node
    if (!$node.story -or !$node.description -or !$node.label) { throw 'Missing source text.' }
    $seen = @{}
    foreach ($cost in $node.costs.ChildNodes) {
        if ($cost.Name -eq 'li' -or [int]$cost.InnerText -le 0 -or $seen.ContainsKey($cost.Name)) { throw 'Invalid material cost.' }
        $seen[$cost.Name] = $true
    }
}
foreach ($branch in 'Wing','Arrow','Halo','Law') {
    $ranks = @($nodes | Where-Object branch -EQ $branch | Sort-Object { [int]$_.rank })
    if ((($ranks | ForEach-Object { [string]$_.rank }) -join ',') -ne '1,2,3') { throw "Invalid branch ranks: $branch" }
    for ($i = 1; $i -lt 3; $i++) {
        if ([string]$ranks[$i].prerequisite -ne [string]$ranks[$i-1].defName) { throw "Invalid prerequisite: $branch" }
    }
}
Write-Output 'PASS: twelve Def nodes; four complete acyclic branches; positive unique costs; source text present.'
