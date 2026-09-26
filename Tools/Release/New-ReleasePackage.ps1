#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$Revision = 'HEAD',
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Get-Command git, dotnet -ErrorAction Stop | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Always compile the archived commit, never mix working files with an older DLL.
$commit = & git -C $root rev-parse --verify --end-of-options "$Revision^{commit}"
if ($LASTEXITCODE -ne 0) { throw "Cannot resolve revision: $Revision" }
$commit = $commit.Trim()
if (!$Version) { $Version = 'dev-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $commit.Substring(0, 8) }
$name = "MiliraXian_NeiyuLaw-$Version"
$releaseRoot = Join-Path $root '.release'
$zipPath = Join-Path $releaseRoot "$name.zip"
if (Test-Path -LiteralPath $zipPath) { throw "Package already exists: $zipPath" }

# Resolve personal paths in the original checkout before moving into the archive.
$properties = & dotnet msbuild (Join-Path $root 'MiliraXian_NeiyuLaw.csproj') '-getProperty:RimWorldManagedDir,AriandelLibraryDll,AlienRaceDll,ZAnimationModDll'
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve dependency paths. Configure Directory.Build.local.props first.' }
$paths = ($properties | Out-String | ConvertFrom-Json).Properties
$buildArgs = @()
foreach ($key in @('RimWorldManagedDir', 'AriandelLibraryDll', 'AlienRaceDll', 'ZAnimationModDll')) {
    $resolved = (Resolve-Path -LiteralPath $paths.$key).Path
    $buildArgs += "-p:${key}=$resolved"
}

$work = Join-Path $releaseRoot ('work/' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $work 'source'
$package = Join-Path $work 'package/MiliraXian_NeiyuLaw'
New-Item -ItemType Directory -Path $source, $package -Force | Out-Null
$archive = Join-Path $work 'source.zip'
& git -C $root archive --format=zip "--output=$archive" $commit
if ($LASTEXITCODE -ne 0) { throw 'Cannot archive the selected commit.' }
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $source)

# packages.config is not restored by dotnet restore. Reuse the installed Harmony
# package, or fetch that exact package from NuGet when building a fresh checkout.
[xml]$packages = Get-Content -LiteralPath (Join-Path $source 'packages.config') -Raw
$harmony = @($packages.packages.package | Where-Object id -EQ 'Lib.Harmony')[0]
$packageName = "$($harmony.id).$($harmony.version)"
$harmonyDir = Join-Path $source "packages/$packageName"
$cachedHarmony = Join-Path $root "packages/$packageName"
New-Item -ItemType Directory -Path (Split-Path $harmonyDir) -Force | Out-Null
if (Test-Path -LiteralPath (Join-Path $cachedHarmony 'lib/net48/0Harmony.dll')) {
    Copy-Item -LiteralPath $cachedHarmony -Destination $harmonyDir -Recurse
} else {
    $id = $harmony.id.ToLowerInvariant()
    $versionNumber = $harmony.version
    $nupkg = Join-Path $work 'harmony.nupkg'
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$id/$versionNumber/$id.$versionNumber.nupkg" -OutFile $nupkg
    [IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $harmonyDir)
}

# Read the actual project references to fail early and record dependency hashes.
$dependencies = @{}
foreach ($project in @('MiliraXian_NeiyuLaw.csproj', 'MiliraXian_MACompat.csproj')) {
    $referenceJson = & dotnet msbuild (Join-Path $source $project) @buildArgs '-getItem:Reference'
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect references: $project" }
    foreach ($reference in (($referenceJson | Out-String | ConvertFrom-Json).Items.Reference)) {
        if (!$reference.PSObject.Properties['HintPath']) { continue }
        $path = $reference.HintPath
        if (![IO.Path]::IsPathRooted($path)) { $path = Join-Path $source $path }
        $file = Get-Item -LiteralPath $path
        $dependencies[$file.Name] = [ordered]@{
            file = $file.Name
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
}

# Stage only runtime folders from the commit. Never copy an archived binary,
# personal files, source code, or the developer's existing build directory.
foreach ($folder in @('About', '1.6', 'Content')) {
    $from = Join-Path $source $folder
    if (!(Test-Path -LiteralPath $from -PathType Container)) { throw "Missing runtime folder: $folder" }
    foreach ($file in Get-ChildItem -LiteralPath $from -File -Recurse) {
        if ($file.Extension -in @('.dll', '.pdb', '.exe')) { continue }
        $relative = [IO.Path]::GetRelativePath($source, $file.FullName)
        $destination = Join-Path $package $relative
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
Copy-Item -LiteralPath (Join-Path $source 'LoadFolders.xml') -Destination $package

$builds = @(
    @{ project = 'MiliraXian_NeiyuLaw.csproj'; assembly = 'MiliraXian_NeiyuLaw.dll'; destination = '1.6/Assemblies' },
    @{ project = 'MiliraXian_MACompat.csproj'; assembly = 'MiliraXian_MACompat.dll'; destination = '1.6/Mods/co.uk.epicguru.meleeanimation/Assemblies' }
)
foreach ($build in $builds) {
    $output = Join-Path $work ('build/' + $build.assembly)
    & dotnet build (Join-Path $source $build.project) --configuration Release --nologo @buildArgs "-p:OutputPath=$output/" "-p:BaseIntermediateOutputPath=$work/obj/$($build.assembly)/"
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $($build.project). No package was published." }
    $binary = Join-Path $output $build.assembly
    if (!(Test-Path -LiteralPath $binary)) { throw "Missing build output: $binary" }
    $destination = Join-Path $package $build.destination
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath $binary -Destination $destination
}

$binaries = @(Get-ChildItem -LiteralPath $package -Filter '*.dll' -Recurse -File)
if ($binaries.Count -ne 2) { throw 'The package must contain exactly the two mod assemblies.' }
foreach ($file in Get-ChildItem -LiteralPath $package -Filter '*.xml' -Recurse -File) {
    $null = [xml](Get-Content -LiteralPath $file.FullName -Raw)
}
[ordered]@{
    version = $Version
    commit = $commit
    builtAtUtc = [DateTime]::UtcNow.ToString('o')
    dotnetSdk = (& dotnet --version | Out-String).Trim()
    dependencies = @($dependencies.Values | Sort-Object file)
    assemblies = @($binaries | ForEach-Object {
        [ordered]@{
            file = [IO.Path]::GetRelativePath($package, $_.FullName).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $package 'build-info.json') -Encoding utf8

# Finish the ZIP before exposing it under its final filename.
$temporaryZip = Join-Path $work "$name.zip"
[IO.Compression.ZipFile]::CreateFromDirectory((Split-Path $package), $temporaryZip)
Move-Item -LiteralPath $temporaryZip -Destination $zipPath
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $name.zip" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii
Write-Host "Commit: $commit"
Write-Host "Release package: $zipPath"
Write-Host "Checksum: $zipPath.sha256"
Write-Host 'Upload these two files manually to GitHub Releases. Game testing is still required.'
