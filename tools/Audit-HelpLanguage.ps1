[CmdletBinding()]
param(
    [ValidatePattern('\A[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}\z')]
    [string]$EvidenceTag
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$contentRoot = Join-Path $repoRoot 'docs/help/content'
$evidence = Join-Path $repoRoot 'docs/help/screenshots/validation/stage11'
if ($EvidenceTag) { $evidence = Join-Path $evidence $EvidenceTag }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$catalog = Get-Content -LiteralPath (Join-Path $contentRoot 'catalog.json') -Raw | ConvertFrom-Json
$rows = [Collections.Generic.List[object]]::new()
$issues = [Collections.Generic.List[object]]::new()
$keys = @()
foreach ($culture in $catalog.cultures) {
    $suffix = switch ($culture) { 'ru-RU' { '.ru' }; 'uk-UA' { '.uk' }; 'de-DE' { '.de' }; default { '' } }
    [xml]$resources = Get-Content -LiteralPath (Join-Path $repoRoot "CryptoBook/Properties/Resources$suffix.resx") -Raw
    $keys += ,@($resources.root.data | Where-Object { -not [string]::IsNullOrWhiteSpace($_.value) } | ForEach-Object name | Sort-Object)
    $labels = @($resources.root.data | ForEach-Object { ([string]$_.value).Replace('_','') })
    foreach ($topic in $catalog.topics) {
        $path = Join-Path $contentRoot "$culture/$($topic.id).md"
        $body = [IO.File]::ReadAllText($path)
        # Fenced syntax, inline code and the explicit language selector names are
        # allowed to contain another alphabet. This scan is a triage aid only.
        $plain = [regex]::Replace($body, '(?ms)^(`{3,}).*?^\1\s*$', '')
        $plain = [regex]::Replace($plain, '`[^`\r\n]+`', '')
        $plain = $plain.Replace('Русский','').Replace('Українська','')
        $foreign = if ($culture -in @('en-US','de-DE')) { '[\p{IsCyrillic}]+' } elseif ($culture -eq 'uk-UA') { '[ыэёъЫЭЁЪ]+' } else { '[ґєіїҐЄІЇ]+' }
        $found = @([regex]::Matches($plain, $foreign) | ForEach-Object Value | Sort-Object -Unique)
        $bold = @([regex]::Matches($body, '\*\*([^*\r\n]+)\*\*') | ForEach-Object { $_.Groups[1].Value })
        $mnemonics = @($bold | Where-Object { $_ -match '_' })
        if ($found.Count -gt 0 -or $mnemonics.Count -gt 0) { $issues.Add(@{culture=$culture;topic=$topic.id;alphabet=$found;mnemonics=$mnemonics}) }
        $images = @([regex]::Matches($body, '!\[([^\]]+)\]\(images/([^\s]+)\s+"([^"]+)"\)') | ForEach-Object { @{id=$_.Groups[2].Value;alt=$_.Groups[1].Value;caption=$_.Groups[3].Value} })
        $rows.Add([ordered]@{
            culture=$culture; topic=$topic.id; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            title=([regex]::Match($body,'(?m)^# (.+)$').Groups[1].Value.Trim()); commandLabels=$bold
            exactResourceLabels=@($bold | Where-Object { $_ -in $labels }); images=$images
            alphabetCandidates=$found; mnemonicCandidates=$mnemonics
            review='Automated alphabet/mnemonic/resource-label inventory; not a semantic or native-speaker pass.'
        })
    }
}
$keySetsEqual = @($keys | ForEach-Object { $_ -join "`n" } | Sort-Object -Unique).Count -eq 1
$result = [ordered]@{ articles=$rows.Count; keysPerCulture=@($keys | ForEach-Object { $_.Count }); keySetsEqual=$keySetsEqual; issues=$issues; rows=$rows }
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $evidence 'language-audit.json') -Encoding utf8
Write-Output "articles=$($rows.Count) keysEqual=$keySetsEqual candidates=$($issues.Count)"
$issues | ConvertTo-Json -Depth 5
if (-not $keySetsEqual) { throw 'Localized resource key sets differ.' }
