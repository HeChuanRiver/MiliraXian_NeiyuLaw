param(
    [string]$ModRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$CecilPath = 'D:\RimWorldModForMe\TempTools\ilspycmd\.store\ilspycmd\9.1.0.7988\ilspycmd\9.1.0.7988\tools\net8.0\any\Mono.Cecil.dll'
)
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
Add-Type -LiteralPath $CecilPath
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $ModRoot '../../RimWorldWin64_Data/Managed'))
$resolver.AddSearchDirectory((Join-Path $ModRoot '1.6/Assemblies'))
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll'), $reader)
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ModRoot '../../RimWorldWin64_Data/Managed/Assembly-CSharp.dll'), $reader)
$types = @{}
foreach ($type in @($assembly.MainModule.Types) + @($game.MainModule.Types)) { $types[$type.Name] = $type }
$defs = @{}
$xmlCount = 0
Get-ChildItem -LiteralPath (Join-Path $ModRoot '1.6') -Filter '*.xml' -Recurse | ForEach-Object {
    [xml]$doc = Get-Content -Raw -LiteralPath $_.FullName
    $xmlCount++
    if ($doc.DocumentElement.Name -eq 'Defs') {
        foreach ($node in $doc.DocumentElement.ChildNodes) {
            if ($node.defName) { $defs[[string]$node.defName] = $node }
        }
    }
}
$fieldCount = 0
$referenceCount = 0
foreach ($character in 'Zhaoli','Mingyuan') {
    $source = Get-Content -Raw -LiteralPath (Join-Path $ModRoot "Source/$character/${character}PowerBalance.cs")
    foreach ($match in [regex]::Matches($source, '(?:Thing|Hediff|AbilityDef|LibraryPassives|Weapon|Armor|Ability|(?:Ability|Thing|Hediff)Comp<[^>]+>)\("([A-Za-z0-9_]+)"')) {
        $name = $match.Groups[1].Value
        if ($name.EndsWith('_')) { continue }
        if (-not $defs.ContainsKey($name)) { throw "Missing Def: $name" }
        $referenceCount++
    }
    $variables = @{}
    $source = ($source -split 'private static StatModifier')[0]
    foreach ($match in [regex]::Matches($source, 'var (\w+) = (?:Ability|Thing|Hediff)Comp<(\w+)>\("(\w+)"\)')) {
        $variables[$match.Groups[1].Value] = $match.Groups[2].Value
        $def = $defs[$match.Groups[3].Value]
        $expected = $match.Groups[2].Value
        if (-not @($def.comps.li | Where-Object { $_.Class -like "*.$expected" }).Count) { throw "Missing comp $expected on $($def.defName)" }
    }
    foreach ($match in [regex]::Matches($source, 'var (\w+) = (Thing|Hediff)\(')) { $variables[$match.Groups[1].Value] = $match.Groups[2].Value + 'Def' }
    foreach ($match in [regex]::Matches($source, 'p\.Field\((.*?), "(\w+)"')) {
        $target = $match.Groups[1].Value
        $fieldName = $match.Groups[2].Value
        $typeName = $variables[$target]
        if ($target -match '(?:Ability|Hediff|Thing)Comp<(\w+)>') { $typeName = $Matches[1] }
        if ($target -match 'statFactors\[0\]') { $typeName = 'StatModifier' }
        if ($target -match '\.Verbs\[0\]|\.verbProperties') { $typeName = 'VerbProperties' }
        if ($target -match '\.projectile$') { $typeName = 'ProjectileProperties' }
        if (-not $typeName) { throw "Unresolved tuning target: $target" }
        $type = $types[$typeName]
        $field = @($type.Fields | Where-Object Name -EQ $fieldName)
        if (-not $field.Count) { throw "Missing field $typeName.$fieldName" }
        $fieldCount++
    }
    $abilityTypes = @($assembly.MainModule.Types | Where-Object { $_.Namespace -eq "MiliraXian.Characters.$character" -and $_.Name -like 'CompAbilityEffect_*' -and $_.Name -notlike '*PowerLimited' })
    foreach ($type in $abilityTypes) {
        if ($type.BaseType.Name -ne "CompAbilityEffect_${character}PowerLimited") { throw "Ungated base: $($type.FullName)" }
        $apply = $type.Methods | Where-Object Name -EQ 'Apply'
        if ($apply -and -not ($apply.Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -like '*PowerBalance::get_Sealed*' })) {
            throw "Missing Apply seal guard: $($type.Name)"
        }
    }
}
$keySets = @()
foreach ($language in 'ChineseSimplified (简体中文)','ChineseTraditional (繁體中文)','English') {
    [xml]$lang = Get-Content -Raw -LiteralPath (Join-Path $ModRoot "1.6/Languages/$language/Keyed/MX_CharacterPower.xml")
    $keys = @($lang.LanguageData.ChildNodes | Where-Object NodeType -EQ 'Element' | ForEach-Object Name | Sort-Object)
    if (@($keys | Group-Object | Where-Object Count -GT 1).Count) { throw 'Duplicate language keys' }
    $keySets += ,$keys
}
if ((Compare-Object $keySets[0] $keySets[1]) -or (Compare-Object $keySets[0] $keySets[2])) { throw 'Translation coverage mismatch' }
Write-Output "PASS: $xmlCount XML files; $referenceCount Def references; $fieldCount reflected tuning fields; all 11 ability Apply guards; $($keySets[0].Count) keys in each of 3 languages."
$assembly.Dispose()
$game.Dispose()
$resolver.Dispose()
