param(
    [string]$ModRoot = (Join-Path $PSScriptRoot '../..'),
    [string]$CecilPath = 'D:\RimWorldModForMe\TempTools\ilspycmd\.store\ilspycmd\9.1.0.7988\ilspycmd\9.1.0.7988\tools\net8.0\any\Mono.Cecil.dll',
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
Add-Type -LiteralPath $CecilPath
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $ModRoot '../../RimWorldWin64_Data/Managed'))
$resolver.AddSearchDirectory((Join-Path $ModRoot '1.6/Assemblies'))
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
if (-not $AssemblyPath) { $AssemblyPath = Join-Path $ModRoot '1.6/Assemblies/MiliraXian_NeiyuLaw.dll' }
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $AssemblyPath).Path, $reader)
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
    foreach ($match in [regex]::Matches($source, 'p\.(?:Field|ScaleField|KeepField)\((.*?), "(\w+)"')) {
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

# Tier two must reach the signature mechanics, not an alternate ordinary-damage branch.
$mechanicMethods = @(
    @('CompAbilityEffect_NeiyuSwordExecution', 'DecapitateTarget'),
    @('HediffComp_MXNeiyuCountShield', 'CalculatePhase2Cost'),
    @('Hediff_ZhaoliDeathSentenceResult', 'Resolve'),
    @('CompAbilityEffect_ZhaoliGuiyi', 'Apply'),
    @('HediffComp_ZhaoliKarmaLinks', 'TryUseBalancedSubstitute'),
    @('MingyuanUtility', 'ApplyTrueDamage'),
    @('MingyuanUtility', 'RestorePawnToBestCondition'),
    @('HediffComp_MingyuanLifeBurn', 'TryTriggerBurstNow'),
    @('HediffComp_MingyuanProtectiveFlameShield', 'TryRepairOneBodyPart'),
    @('MingyuanTimeBurnUtility', 'DissolveBuilding')
)
foreach ($check in $mechanicMethods) {
    $method = @($types[$check[0]].Methods | Where-Object Name -EQ $check[1])
    if ($method.Count -ne 1) { throw "Missing/ambiguous mechanic method: $($check -join '.')" }
    if ($method[0].Body.Instructions | Where-Object { $_.Operand -and $_.Operand.ToString() -match 'PowerBalance::get_Is(Balanced|Original)' }) {
        throw "Tier two replaces a signature mechanic: $($check -join '.')"
    }
}
$reset = $types['MingyuanTimeBurnRecord'].Methods | Where-Object Name -EQ 'Reset'
$castFlag = $reset.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'reducedCast' }
if (-not $castFlag -or $castFlag.Previous.OpCode.Name -ne 'ldc.i4.0') { throw 'New Time Burn casts still use the legacy damage-only mode' }
foreach ($typeName in 'HediffComp_ZhaoliShieldLayers','HediffComp_MingyuanLifeBurn','HediffComp_MingyuanSelfBurn','HediffComp_MingyuanProtectiveFlameShield') {
    foreach ($method in ($types[$typeName].Methods | Where-Object { $_.Name -in 'get_CompDescriptionExtra','get_CompTipStringExtra' })) {
        if ($method.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldfld' -and $_.Operand.ToString() -like '*Verse.Def::description' }) {
            throw "Duplicate base description in extra tooltip: $typeName"
        }
    }
}
[xml]$neiyuAbilities = Get-Content -Raw -LiteralPath (Join-Path $ModRoot '1.6/Defs/AbilityDefs/MiliraXian_Ability_Neiyu.xml')
foreach ($def in $neiyuAbilities.Defs.AbilityDef) {
    if ('MX_Power_' + $def.defName -notin $keySets[0]) { throw "Missing current-tier Neiyu description: $($def.defName)" }
}
Write-Output "PASS: $xmlCount XML files; $referenceCount Def references; $fieldCount reflected tuning fields; all 11 ability Apply guards; $($keySets[0].Count) keys in each of 3 languages; $($mechanicMethods.Count) intact mechanic paths; Time Burn legacy migration; no duplicated base tooltips."
$assembly.Dispose()
$game.Dispose()
$resolver.Dispose()
