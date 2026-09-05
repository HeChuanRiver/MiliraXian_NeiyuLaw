param([string]$ModPath = (Join-Path $PSScriptRoot '../..'))

$ErrorActionPreference = 'Stop'
$ModPath = (Resolve-Path -LiteralPath $ModPath).Path
$languages = @('ChineseSimplified (简体中文)', 'ChineseTraditional (繁體中文)', 'English')
$files = @('MX_CharacterPower.xml', 'MX_Mingyuan_Status.xml')
foreach ($file in $files) {
    $reference = $null
    foreach ($language in $languages) {
        [xml]$xml = Get-Content -LiteralPath (Join-Path $ModPath "1.6/Languages/$language/Keyed/$file") -Raw -Encoding UTF8
        $entries = @{}
        foreach ($node in $xml.LanguageData.ChildNodes | Where-Object NodeType -EQ 'Element') {
            if ($entries.ContainsKey($node.Name)) { throw "Duplicate key: $($node.Name)" }
            $text = $node.InnerText
            if ($text.Contains([char]0xFFFD)) { throw "Broken encoding: $($node.Name)" }
            if ($text -match '\\n|\r|\n[ \t]+|\n{3,}') { throw "Unexpected line-break format: $($node.Name)" }
            $entries[$node.Name] = (@([regex]::Matches($text, '\{\d+\}') | ForEach-Object Value | Sort-Object -Unique) -join ',')
            $isSetting = $node.Name -match '^MX_Power_(Zhaoli|Mingyuan)(_(Original|Balanced|Decorative))?$'
            if ($file -eq 'MX_CharacterPower.xml' -and !$isSetting -and $text -match '第[一二三123][档檔]|第一階|第二階|Tier\s*[123]|tier\s*(one|two|three)|原来的|原來的|沿用原|不再|不是.*而是') {
                throw "Balance commentary in gameplay tooltip: $($node.Name)"
            }
        }
        if ($null -ne $reference) {
            if (Compare-Object @($reference.Keys | Sort-Object) @($entries.Keys | Sort-Object)) { throw "Key coverage mismatch: $file" }
            foreach ($key in $entries.Keys) {
                if ($entries[$key] -ne $reference[$key]) { throw "Placeholder mismatch: $key" }
            }
        }
        $reference = $entries
    }
}

[xml]$status = Get-Content -LiteralPath (Join-Path $ModPath '1.6/Languages/English/Keyed/MX_Mingyuan_Status.xml') -Raw -Encoding UTF8
foreach ($entry in @(@('MX_Mingyuan_Bow_ModeDesc', 6), @('MX_Mingyuan_Shield_TipRepair', 2), @('MX_Mingyuan_BurningPillar_Inspect', 6))) {
    $node = $status.LanguageData.SelectSingleNode($entry[0])
    for ($index = 0; $index -lt $entry[1]; $index++) {
        if (!$node.InnerText.Contains("{$index}")) { throw "Missing live-value placeholder: $($entry[0]) {$index}" }
    }
}

Add-Type -AssemblyName System.Drawing
$textures = @{
    'Items/CinderSword.png' = 1024
    'Items/RainbowBow.png' = 1024
    'Projectile/RainbowArrow.png' = 128
    'Apparel/Mingyuan/Naked.png' = 512
}
foreach ($direction in @('east', 'north', 'south', 'west')) { $textures["Apparel/Mingyuan/Naked_Female_$direction.png"] = 512 }
foreach ($entry in $textures.GetEnumerator()) {
    $path = Join-Path $ModPath "Content/Textures/MiliraXianMingyuan/$($entry.Key)"
    $bitmap = [System.Drawing.Bitmap]::new($path)
    try {
        if ($bitmap.Width -ne $entry.Value -or $bitmap.Height -ne $entry.Value -or $bitmap.GetPixel(0, 0).A -ne 0) {
            throw "Unexpected texture dimensions or transparency: $path"
        }
    } finally { $bitmap.Dispose() }
}
[xml]$weapons = Get-Content -LiteralPath (Join-Path $ModPath '1.6/Defs/ThingDefs/MiliraXian_Mingyuan_Weapons.xml') -Raw -Encoding UTF8
if ($weapons.SelectSingleNode("/Defs/ThingDef[defName='MX_Mingyuan_CinderSword']/equippedAngleOffset").InnerText -ne '90') { throw 'Upright sword orientation mismatch' }
if ($weapons.SelectSingleNode("/Defs/ThingDef[defName='MX_Bullet_Mingyuan_RainbowArrow']/graphicData/drawSize").InnerText -ne '(1.8,1.8)') { throw 'Arrow aspect ratio mismatch' }

Write-Output 'PASS: three-language keys/placeholders, gameplay wording, line breaks, live-value templates, eight textures, sword orientation and arrow aspect ratio.'
