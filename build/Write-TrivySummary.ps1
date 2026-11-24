param(
    [Parameter(Mandatory)] [string] $ReportPath,
    [string] $SummaryPath = $env:GITHUB_STEP_SUMMARY
)

$ErrorActionPreference = 'Stop'
if (!$SummaryPath) { throw 'A summary output path is required.' }

function Format-TableCell([string] $Value) {
    $text = $Value -replace '\p{Cc}', ' '
    if ($text.Length -gt 200) { $text = $text.Substring(0, 200) + '…' }
    # Report fields can contain PR-controlled text. Keep it inside the Markdown cell.
    $text -replace '[&<>"''\\`*_\[\]|~!:@$]', { '&#{0};' -f [int][char]$_.Value }
}

$report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json -AsHashtable
if ($report.SchemaVersion -ne 2) { throw 'Expected a Trivy JSON report with schema version 2.' }

$findings = @(foreach ($result in $report.Results) {
    foreach ($item in $result.Vulnerabilities) {
        [pscustomobject] @{
            Type = 'Vulnerability'
            Severity = $item.Severity
            Id = $item.VulnerabilityID
            Subject = $item.PkgName
            Installed = $item.InstalledVersion
            Fixed = $item.FixedVersion
        }
    }
    foreach ($item in $result.Misconfigurations) {
        if ($item.Status -eq 'FAIL') {
            [pscustomobject] @{
                Type = 'Misconfiguration'
                Severity = $item.Severity
                Id = $item.ID
                Subject = $result.Target
                Installed = ''
                Fixed = ''
            }
        }
    }
})

$severities = @('CRITICAL', 'HIGH', 'MEDIUM', 'LOW', 'UNKNOWN')
$previewLimit = 25
$summary = @('## Trivy security scan', '', 'Counts across all scan targets.', '',
    '| Severity | Vulnerabilities | Misconfigurations |', '| --- | ---: | ---: |')
$distinctFindings = @(foreach ($severity in $severities) {
    $atSeverity = @($findings | Where-Object Severity -eq $severity)
    $vulnerabilities = @($atSeverity | Where-Object Type -eq 'Vulnerability').Count
    $misconfigurations = $atSeverity.Count - $vulnerabilities
    $summary += "| $severity | $vulnerabilities | $misconfigurations |"
    $atSeverity | Sort-Object Type, Id, Subject, Installed, Fixed -CaseSensitive -Unique
})

if ($distinctFindings.Count) {
    $preview = @($distinctFindings | Select-Object -First $previewLimit)
    $summary += '', "Showing $($preview.Count) of $($distinctFindings.Count) distinct findings, highest severity first.", '',
        '| Severity | Type | ID | Package or file | Installed | Fixed in |', '| --- | --- | --- | --- | --- | --- |'
    foreach ($finding in $preview) {
        $cells = $finding.Severity, $finding.Type, $finding.Id, $finding.Subject, $finding.Installed, $finding.Fixed
        $summary += '| ' + (($cells | ForEach-Object { Format-TableCell $_ }) -join ' | ') + ' |'
    }
} else {
    $summary += '', 'No vulnerabilities or misconfigurations found.'
}

$summary += '', 'See the **trivy-report** artifact for the full HTML report.', ''
Add-Content -LiteralPath $SummaryPath -Value $summary -Encoding utf8
