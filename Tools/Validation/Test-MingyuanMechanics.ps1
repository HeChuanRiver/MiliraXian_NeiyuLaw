param([string]$ModRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$ModRoot = (Resolve-Path -LiteralPath $ModRoot).Path
$culture = [Globalization.CultureInfo]::InvariantCulture
function Number($value) { [double]::Parse([string]$value, $culture) }
function Check($condition, $message) { if (!$condition) { throw $message } }
[xml]$resource = Get-Content -LiteralPath (Join-Path $ModRoot '1.6/Defs/HediffDefs/MiliraXian_Mingyuan_PawnResource.xml') -Raw -Encoding UTF8
$self = $resource.SelectSingleNode("/Defs/HediffDef[defName='MX_Mingyuan_SelfBurn']")
$stages = @($self.stages.li)
Check ($stages.Count -eq 32) 'Expected 31 ten-layer stages and one Overburn stage.'
Check (!$self.SelectSingleNode('.//statFactorsBySeverity')) 'Self Burn must not interpolate bonuses between ten-layer boundaries.'
$cases = @(
    @(0, 1, 1, 1), @(9.99, 1, 1, 1), @(10, 1.04, 1.02, 1.04),
    @(19.99, 1.04, 1.02, 1.04), @(20, 1.08, 1.04, 1.08),
    @(299.99, 2.16, 1.58, 2.16), @(300, 2.2, 1.6, 2.2), @(301, 4.4, 1.6, 2.2), @(500, 4.4, 1.6, 2.2)
)
foreach ($case in $cases) {
    $stage = $stages | Where-Object { (Number $_.minSeverity) -le $case[0] } | Select-Object -Last 1
    Check ([Math]::Abs((Number $stage.statFactors.MeleeDamageFactor) - $case[1]) -lt 0.00001) "Melee bonus at $($case[0]) layers"
    Check ([Math]::Abs((Number $stage.statFactors.MoveSpeed) - $case[2]) -lt 0.00001) "Movement bonus at $($case[0]) layers"
    Check ([Math]::Abs(1 / (Number $stage.statFactors.MeleeCooldownFactor) - $case[3]) -lt 0.00001) "Attack speed at $($case[0]) layers"
}
Check ((Number $self.comps.li.overburnDecayLayers) -eq 0) 'Excess layers must remain available for timed release.'
[xml]$apparel = Get-Content -LiteralPath (Join-Path $ModRoot '1.6/Defs/ApparelDefs/MiliraXian_Mingyuan_Apparel.xml') -Raw -Encoding UTF8
$body = $resource.SelectSingleNode("/Defs/HediffDef[defName='MX_Mingyuan_BurningBody']/stages/li/statFactors/IncomingDamageFactor")
$armor = $apparel.SelectSingleNode("/Defs/ThingDef[defName='MX_Mingyuan_InfernoArmor']/equippedStatOffsets/IncomingDamageFactor")
$crown = $apparel.SelectSingleNode("/Defs/ThingDef[defName='MX_Mingyuan_BurningFeatherCrown']/equippedStatOffsets/IncomingDamageFactor")
$incoming = (1 + (Number $armor.InnerText) + (Number $crown.InnerText)) * (Number $body.InnerText)
Check ([Math]::Abs($incoming - 0.65) -lt 0.000001) 'Exclusive loadout incoming damage should be 65%, before external effects/armor.'
[xml]$skills = Get-Content -LiteralPath (Join-Path $ModRoot '1.6/Defs/AbilityDefs/MiliraXian_Mingyuan_Absorb.xml') -Raw -Encoding UTF8
$absorb = $skills.Defs.AbilityDef
Check ($absorb.cooldownTicksRange -eq '900') 'Absorb cooldown must be 15 seconds.'
Check ($absorb.comps.li.radius -eq '10' -and $absorb.comps.li.arcDegrees -eq '108' -and $absorb.comps.li.damagePerLayer -eq '0.3') 'Absorb cone/layer damage mismatch.'
[xml]$effects = Get-Content -LiteralPath (Join-Path $ModRoot '1.6/Defs/Effects/MiliraXian_Effect_Mingyuan_Skills.xml') -Raw -Encoding UTF8
Check ($effects.Defs.DamageDef.scaleDamageToBuildingsBasedOnFlammability -eq 'false') 'Structural damage must work on nonflammable buildings.'
foreach ($factor in 'buildingDamageFactor','buildingDamageFactorPassable','buildingDamageFactorImpassable') {
    Check ($effects.Defs.DamageDef.$factor -eq '1') "Unexpected structural multiplier: $factor"
}
$concrete = @{}
$xmlCount = 0
Get-ChildItem -LiteralPath (Join-Path $ModRoot '1.6') -Filter '*.xml' -Recurse | ForEach-Object {
    [xml]$doc = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
    $xmlCount++
    if ($doc.DocumentElement.Name -eq 'Defs') {
        foreach ($node in $doc.DocumentElement.ChildNodes) {
            if (!$node.defName) { continue }
            $key = $node.Name + ':' + [string]$node.defName
            Check (!$concrete.ContainsKey($key)) "Duplicate concrete Def: $key"
            $concrete[$key] = $true
        }
    }
}
foreach ($language in 'English','ChineseSimplified (简体中文)','ChineseTraditional (繁體中文)') {
    [xml]$translation = Get-Content -LiteralPath (Join-Path $ModRoot "1.6/Languages/$language/DefInjected/AbilityDef/MiliraXian_Mingyuan_Absorb.xml") -Raw -Encoding UTF8
    Check ($null -ne $translation.LanguageData.'MX_Mingyuan_Absorb.label' -and $null -ne $translation.LanguageData.'MX_Mingyuan_Absorb.description') "Missing Absorb translation: $language"
}
Write-Output "PASS: $xmlCount XML files and unique concrete Defs; nine Self Burn boundaries; 65% loadout damage factor; Absorb parameters/translations; nonflammable structural damage."
