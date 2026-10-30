[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/help'
$catalog = Get-Content -LiteralPath (Join-Path $root 'content/catalog.json') -Raw | ConvertFrom-Json
$errors = [Collections.Generic.List[string]]::new()
function Assert-Help([bool]$condition, [string]$message) { if (-not $condition) { $errors.Add($message) } }
function Same-Set($left, $right) { return (@(Compare-Object @($left | Sort-Object -Unique) @($right | Sort-Object -Unique)).Count -eq 0) }
$ids = @($catalog.topics.id)
$plannedIds = @(Get-Content -LiteralPath (Join-Path $root 'TOPICS.md') | ForEach-Object {
    if ($_ -match '^\| \x60([a-z]+\.[a-z]+)\x60 \|') { $Matches[1] }
})
$features = @(Get-Content -LiteralPath (Join-Path $root 'FEATURES.md') | ForEach-Object {
    if ($_ -match '^\| (F\d{3}) \|') { $Matches[1] }
})
Assert-Help ($catalog.schemaVersion -eq 1) 'Unsupported schema.'
Assert-Help (($ids | Sort-Object -Unique).Count -eq $ids.Count) 'Duplicate topic IDs.'
Assert-Help (Same-Set $ids $plannedIds) 'Catalog and editorial topic IDs differ.'
$available = @($catalog.topics | Where-Object status -ne 'planned')
$articleCount = 0; $imageCount = 0; $pendingLinks = 0; $relativeLinks = 0
$topicLinks = @{}
$uniqueImages = [Collections.Generic.HashSet[string]]::new()
foreach ($topic in $catalog.topics) {
    Assert-Help ($topic.section -in $catalog.sections) ('Unknown section: ' + $topic.id)
    foreach ($feature in $topic.featureIds) { Assert-Help ($feature -in $features) ('Unknown feature: ' + $feature) }
}
foreach ($culture in $catalog.cultures) {
    $localeRoot = Join-Path $root ('content/' + $culture)
    $locale = Get-Content -LiteralPath (Join-Path $localeRoot 'locale.json') -Raw | ConvertFrom-Json
    Assert-Help ($locale.culture -ceq $culture) ('Culture mismatch: ' + $culture)
    Assert-Help (Same-Set $locale.sections.PSObject.Properties.Name $catalog.sections) ('Section translations: ' + $culture)
    Assert-Help (Same-Set $locale.topics.id $available.id) ('Article translations: ' + $culture)
    foreach ($entry in $locale.topics) {
        $articleCount++
        Assert-Help ($entry.file -ceq ($entry.id + '.md')) ('Unsafe article path: ' + $entry.file)
        $body = Get-Content -LiteralPath (Join-Path $localeRoot $entry.file) -Raw
        Assert-Help ([regex]::Matches($body, '(?m)^# ').Count -eq 1) ('Article must have one H1: ' + $entry.file)
        Assert-Help ($body.Length -gt 500 -and $entry.keywords.Count -gt 0) ('Incomplete article: ' + $culture + '/' + $entry.id)
        $targets = @([regex]::Matches($body, '\]\(help:([a-z]+\.[a-z]+)\)') | ForEach-Object { $_.Groups[1].Value })
        if ($topicLinks.ContainsKey($entry.id)) {
            Assert-Help (Same-Set $targets $topicLinks[$entry.id]) ('Translated link set: ' + $culture + '/' + $entry.id)
        } else { $topicLinks[$entry.id] = $targets }
        foreach ($link in [regex]::Matches($body, '\]\(help:([a-z]+\.[a-z]+)\)')) {
            $target = $link.Groups[1].Value
            Assert-Help ($target -in $ids) ('Unknown help target: ' + $target)
            if (($catalog.topics | Where-Object id -eq $target).status -eq 'planned') { $pendingLinks++ }
        }
        $images = @([regex]::Matches($body, '!\[([^\]]+)\]\((images/(S\d{2}-[a-z])\.png) "([^"]+)"\)'))
        $topic = $catalog.topics | Where-Object id -eq $entry.id
        Assert-Help (Same-Set @($images | ForEach-Object { $_.Groups[3].Value }) $topic.illustrationIds) ('Image set: ' + $entry.id)
        foreach ($image in $images) {
            $imageCount++
            $shot = $image.Groups[3].Value
            [void]$uniqueImages.Add($culture + '/' + $shot)
            $illustration = @($catalog.illustrations | Where-Object id -eq $shot)
            Assert-Help ($illustration.Count -eq 1 -and $illustration[0].status -eq 'ready') ('Unregistered or unready image: ' + $shot)
            Assert-Help (Test-Path -LiteralPath (Join-Path $localeRoot $image.Groups[2].Value)) ('Missing image: ' + $culture + '/' + $shot)
            Assert-Help ($image.Groups[1].Value.Length -gt 10 -and $image.Groups[4].Value.Length -gt 10) ('Empty alt/caption: ' + $shot)
            foreach ($kind in @('raw','annotations','passports')) {
                $extension = if ($kind -eq 'raw') { '.png' } else { '.json' }
                Assert-Help (Test-Path -LiteralPath (Join-Path $root ('screenshots/'+$kind+'/'+$culture+'/'+$shot+$extension))) ('Missing '+$kind+': '+$culture+'/'+$shot)
            }
            $passport = Get-Content -LiteralPath (Join-Path $root ('screenshots/passports/'+$culture+'/'+$shot+'.json')) -Raw | ConvertFrom-Json
            Assert-Help ($passport.culture -ceq $culture -and $passport.shotId -ceq $shot) ('Passport identity: '+$culture+'/'+$shot)
            Assert-Help ($passport.visualCheck -like 'Reviewed*') ('Unreviewed image: '+$culture+'/'+$shot)
        }
    }
}
Assert-Help ($uniqueImages.Count -eq @($catalog.illustrations | Where-Object status -eq 'ready').Count * $catalog.cultures.Count) 'Ready illustrations and referenced localized images differ.'
$resourceNames = @{'en-US'='Resources.resx';'ru-RU'='Resources.ru.resx';'uk-UA'='Resources.uk.resx';'de-DE'='Resources.de.resx'}
$keys = $null; $resourceCount = 0
foreach ($culture in $catalog.cultures) {
    [xml]$resource = Get-Content -LiteralPath (Join-Path $PSScriptRoot ('../CryptoBook/Properties/'+$resourceNames[$culture])) -Raw
    if ($null -eq $keys) { $keys = @($resource.root.data.name) }
    Assert-Help (Same-Set $keys @($resource.root.data.name)) ('UI resource key mismatch: '+$culture)
    Assert-Help (@($resource.root.data | Where-Object { [string]::IsNullOrWhiteSpace($_.value) }).Count -eq 0) ('Empty UI resources: '+$culture)
    $resourceCount = $resource.root.data.Count
}
foreach ($file in (Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.md')) {
    $body = [IO.File]::ReadAllText($file.FullName)
    $plain = [regex]::Replace($body, '(?ms)^\x60\x60\x60.*?^\x60\x60\x60[^\r\n]*', '')
    foreach ($link in [regex]::Matches($plain, '(?<!!)\[[^\]]+\]\(([^\s)]+)(?: "[^"]*")?\)')) {
        $target = $link.Groups[1].Value
        if ($target -match '^[a-zA-Z]+:' -or $target.StartsWith('#')) { continue }
        $relativeLinks++
        Assert-Help (Test-Path -LiteralPath (Join-Path $file.DirectoryName ($target.Split('#')[0]))) ('Missing relative link: '+$file.Name+' -> '+$target)
    }
    Assert-Help (-not [regex]::IsMatch($body, '(?m)[ \t]+$')) ('Trailing whitespace: '+$file.Name)
    Assert-Help (-not $body.Contains([char]0xFFFD)) ('Invalid text encoding: '+$file.Name)
}
Write-Output "HELP_CONTENT_CHECK topics=$($ids.Count) cultures=$($catalog.cultures.Count) articles=$articleCount image_references=$imageCount image_files=$($uniqueImages.Count) planned_links=$pendingLinks ui_keys=$resourceCount relative_links=$relativeLinks errors=$($errors.Count)"
$errors | ForEach-Object { Write-Output "ERROR: $_" }
if ($errors.Count) { exit 1 }
