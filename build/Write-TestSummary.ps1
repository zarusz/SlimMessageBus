param(
    [string] $ResultsPath = './test-results',
    [string] $SummaryPath = $env:GITHUB_STEP_SUMMARY
)

$ErrorActionPreference = 'Stop'
if (!$SummaryPath) { throw 'A summary output path is required.' }

$totals = [long[]] @(0, 0, 0, 0)
$reportCount = 0
$settings = [System.Xml.XmlReaderSettings]::new()
$settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$settings.XmlResolver = $null

foreach ($file in Get-ChildItem -LiteralPath $ResultsPath -Filter '*.trx' -File -ErrorAction SilentlyContinue) {
    # Results are untrusted data: prohibit DTDs and include only numeric counters.
    $reader = [System.Xml.XmlReader]::Create($file.FullName, $settings)
    try {
        $document = [System.Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        $counters = $document.SelectSingleNode("//*[local-name()='Counters']")
        if ($null -eq $counters) { continue }
        $keys = @('total', 'passed', 'failed', 'notExecuted')
        for ($i = 0; $i -lt $keys.Count; $i++) {
            $value = $counters.GetAttribute($keys[$i])
            if ($value) { $totals[$i] += [long]::Parse($value, [System.Globalization.CultureInfo]::InvariantCulture) }
        }
        $reportCount++
    }
    finally { $reader.Dispose() }
}

$summary = "## .NET tests`n`n| Total | Passed | Failed | Not executed |`n| --- | --- | --- | --- |`n"
if ($reportCount) {
    $summary += '| ' + ($totals -join ' | ') + " |`n"
}
else { $summary += "`nNo TRX test results were produced.`n" }
Add-Content -LiteralPath $SummaryPath -Value $summary -Encoding utf8
