$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workflow = Get-Content (Join-Path $root '.github/workflows/build.yml') -Raw
$script:passed = 0
$sha = 'a' * 40
$repository = 'zarusz/SlimMessageBus'

function Assert-That([bool] $Condition, [string] $Message) {
    if (!$Condition) { throw $Message }
}

function Test-Case([string] $Name, [scriptblock] $Body) {
    & $Body
    $script:passed++
    Write-Host "PASS: $Name"
}

# Extract known job/script indentation to exercise the workflow's actual JS;
# actionlint remains responsible for full YAML and expression validation.
function Get-Job([string] $Name) {
    $match = [regex]::Match($workflow, "(?ms)^  $([regex]::Escape($Name)):\r?`n(?<body>.*?)(?=^  [a-z][\w-]*:\r?$|\z)")
    if (!$match.Success) { throw "Missing workflow job: $Name" }
    $match.Groups['body'].Value
}

function Get-InlineScript([string] $Job) {
    $match = [regex]::Match((Get-Job $Job), '(?ms)^          script: \|\r?\n(?<code>.*?)(?=^\s{0,10}\S|\z)')
    if (!$match.Success) { throw "Missing inline script: $Job" }
    [regex]::Replace($match.Groups['code'].Value, '(?m)^ {12}', '')
}

function Invoke-SourceScript(
    [string] $Script = (Get-InlineScript 'source'),
    [string] $Event = 'workflow_dispatch',
    [string] $Ref = 'refs/heads/master',
    [string] $Number = '', [string] $Reviewed = '',
    [string] $State = 'open', [string] $Head = $sha,
    [string] $BaseRepo = $repository, [string] $BaseRef = 'master',
    [string] $MergeBase = $sha, [switch] $DeletedFork
) {
    $data = @{
        context = @{
            eventName = $Event; ref = $Ref; sha = $sha
            repo = @{ owner = 'zarusz'; repo = 'SlimMessageBus' }
            payload = @{ repository = @{ full_name = $repository } }
        }
        pr = @{
            state = $State
            head = @{ sha = $Head; repo = $(if ($DeletedFork) { $null } else { @{ full_name = 'contributor/SlimMessageBus' } }) }
            base = @{ ref = $BaseRef; repo = @{ full_name = $BaseRepo } }
        }
        number = $Number; reviewed = $Reviewed; mergeBase = $MergeBase
    }
    $harness = @'
const data = JSON.parse(process.argv[1]);
process.env.PR_NUMBER = data.number;
process.env.REVIEWED_SHA = data.reviewed;
const outputs = {};
let reads = 0;
const github = {rest: {
  pulls: {get: async () => { reads++; return {data: data.pr}; }},
  repos: {compareCommitsWithBasehead: async () => ({data: {merge_base_commit: {sha: data.mergeBase}}})}
}};
const summary = {addHeading() {return this;}, addCodeBlock() {return this;}, async write() {}};
const core = {setOutput(key, value) {outputs[key] = value;}, summary};
const AsyncFunction = Object.getPrototypeOf(async function() {}).constructor;
(async () => {
  try {
    await new AsyncFunction('context', 'github', 'core', __SCRIPT__)(data.context, github, core);
    console.log(JSON.stringify({outputs, reads}));
  } catch (error) {
    console.log(JSON.stringify({error: error.message, outputs, reads}));
  }
})();
'@
    $harness = $harness.Replace('__SCRIPT__', (ConvertTo-Json -InputObject $Script -Compress))
    $result = & node -e $harness ($data | ConvertTo-Json -Depth 12 -Compress)
    if ($LASTEXITCODE) { throw 'Workflow JavaScript harness failed.' }
    $result | ConvertFrom-Json -AsHashtable
}

Test-Case 'Forks use pull_request with read-only default permissions' {
    Assert-That ($workflow -match '(?m)^  pull_request:' -and $workflow -notmatch '(?m)^  pull_request_target:') 'Unsafe PR trigger'
    Assert-That ($workflow -match '(?m)^permissions:\r?\n  contents: read\r?\n\r?$') 'Unexpected global permissions'
}
Test-Case 'Local jobs have no secrets or protected environment' {
    foreach ($name in @('source', 'build', 'integration-tests', 'report', 'ci')) {
        $job = Get-Job $name
        Assert-That ($job -notmatch 'secrets\.|(?m)^    environment:') "Secrets/environment in $name"
        if ($job -match 'actions/checkout@') {
            Assert-That ($job -match 'persist-credentials: false') "Persisted credentials in $name"
        }
    }
}
Test-Case 'Cloud and Sonar jobs are restricted to approved events' {
    $cloud = Get-Job 'cloud-tests'
    Assert-That ($cloud -match "github.event_name == 'push'" -and $cloud -match "github.event_name == 'workflow_dispatch'") 'Cloud event guard missing'
    Assert-That ($cloud -match "environment:.*'integration-tests'") 'Cloud approval gate missing'
    Assert-That ($cloud.IndexOf('Recheck reviewed PR') -lt $cloud.IndexOf('- name: Checkout')) 'PR recheck must precede checkout'
    Assert-That ((Get-Job 'sonar') -match "github.event_name == 'push'") 'Sonar must only run on trusted pushes'
}
Test-Case 'Local matrix excludes Azure outbox cases' {
    $local = Get-Job 'integration-tests'
    Assert-That ($local -notmatch 'filter:.*Azure') 'Cloud case selected locally'
    Assert-That ($local -match 'filter:.*Outbox.Kafka' -and $local -match 'filter:.*Outbox.RabbitMQ') 'Local outbox coverage missing'
}
Test-Case 'All test jobs exclude generated code through shared coverage settings' {
    foreach ($name in @('build', 'integration-tests', 'cloud-tests')) {
        Assert-That ((Get-Job $name) -match '--settings ../build/coverage.runsettings') "Coverage settings missing in $name"
    }
    [xml] $settings = Get-Content (Join-Path $root 'build/coverage.runsettings') -Raw
    $configuration = $settings.RunSettings.DataCollectionRunSettings.DataCollectors.DataCollector.Configuration
    Assert-That ($configuration.Format -eq 'opencover') 'Unexpected coverage format'
    Assert-That ($configuration.ExcludeByFile -match '\*\*/obj/\*\*' -and $configuration.ExcludeByFile -match '\*\.g.cs') 'Generated file exclusion missing'
    Assert-That ($configuration.ExcludeByAttribute -eq 'GeneratedCodeAttribute') 'Generated-code attribute exclusion missing'
    Assert-That ($configuration.ExcludeByAttribute -notmatch 'CompilerGenerated') 'Do not exclude user-written async methods or lambdas'
}
Test-Case 'Sonar keeps coverage artifact directories and imports reports recursively' {
    $sonar = Get-Job 'sonar'
    Assert-That ($sonar -match '(?s)pattern: coverage-\*.*?merge-multiple: false') 'Coverage artifacts must not share an extraction directory'
    Assert-That ($sonar.Contains('../coverage-sonar/**/*.opencover.xml')) 'Nested Sonar coverage import is missing'
}
Test-Case 'Regular PRs use merge commit without a privileged API lookup' {
    $result = Invoke-SourceScript -Event pull_request -Ref refs/pull/484/merge
    Assert-That ($result.outputs.repository -eq $repository -and $result.outputs.sha -eq $sha -and $result.reads -eq 0) 'Incorrect PR source'
}
Test-Case 'Manual trusted branch run uses triggering commit' {
    $result = Invoke-SourceScript
    Assert-That ($result.outputs.repository -eq $repository -and $result.outputs.sha -eq $sha) 'Incorrect manual source'
}
Test-Case 'Manual PR uses exact reviewed fork SHA' {
    $result = Invoke-SourceScript -Number 484 -Reviewed $sha
    Assert-That ($result.outputs.repository -eq 'contributor/SlimMessageBus' -and $result.outputs.sha -eq $sha -and $result.reads -eq 1) 'Incorrect fork source'
}
Test-Case 'Reject untrusted dispatch branches and invalid inputs' {
    foreach ($arguments in @(
        @{ Ref = 'refs/heads/feature/ci' }, @{ Number = '484' },
        @{ Number = '484; echo injected'; Reviewed = $sha },
        @{ Number = '484'; Reviewed = 'master' }, @{ Reviewed = $sha }
    )) {
        $result = Invoke-SourceScript @arguments
        Assert-That ($result.ContainsKey('error') -and $result.outputs.Count -eq 0) 'Invalid input accepted'
    }
}
Test-Case 'Reject changed, closed, deleted, or incorrectly targeted PRs' {
    foreach ($arguments in @(
        @{ Head = ('b' * 40) }, @{ State = 'closed' }, @{ DeletedFork = $true },
        @{ BaseRepo = 'other/repo' }, @{ BaseRef = 'feature/untrusted' }
    )) {
        $result = Invoke-SourceScript -Number 484 -Reviewed $sha @arguments
        Assert-That ($result.ContainsKey('error') -and $result.outputs.Count -eq 0) 'Invalid PR accepted'
    }
}
Test-Case 'Recheck head after environment approval' {
    $code = Get-InlineScript 'cloud-tests'
    Assert-That (!(Invoke-SourceScript -Script $code -Number 484 -Reviewed $sha).ContainsKey('error')) 'Reviewed PR rejected'
    Assert-That ((Invoke-SourceScript -Script $code -Number 484 -Reviewed ('b' * 40)).ContainsKey('error')) 'Changed PR accepted'
}
Test-Case 'Manual PR must include trusted CI setup' {
    $result = Invoke-SourceScript -Number 484 -Reviewed $sha -MergeBase ('b' * 40)
    Assert-That ($result.error -match 'Update the PR' -and $result.outputs.Count -eq 0) 'Outdated CI setup accepted'
}

$fixture = Join-Path ([IO.Path]::GetTempPath()) ('SlimMessageBus-summary-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$resultFile = Join-Path $fixture 'results.trx'
$summaryFile = Join-Path $fixture 'summary.md'
try {
    Test-Case 'PowerShell summary reads namespaced TRX counters' {
        Set-Content -LiteralPath $resultFile -Value '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary><Counters total="5" passed="3" failed="1" notExecuted="1" /></ResultSummary></TestRun>'
        & (Join-Path $root 'build/Write-TestSummary.ps1') -ResultsPath $fixture -SummaryPath $summaryFile
        Assert-That ((Get-Content $summaryFile -Raw) -match '\| 5 \| 3 \| 1 \| 1 \|') 'Incorrect counters'
    }
    Test-Case 'Summary handles missing results' {
        Remove-Item -LiteralPath $resultFile
        Remove-Item -LiteralPath $summaryFile
        & (Join-Path $root 'build/Write-TestSummary.ps1') -ResultsPath $fixture -SummaryPath $summaryFile
        Assert-That ((Get-Content $summaryFile -Raw) -match 'No TRX test results') 'Missing-results message absent'
    }
    Test-Case 'Summary rejects XML DTDs in untrusted artifacts' {
        Set-Content -LiteralPath $resultFile -Value '<!DOCTYPE TestRun [<!ENTITY injected "5">]><TestRun><Counters total="&injected;" /></TestRun>'
        $rejected = $false
        try { & (Join-Path $root 'build/Write-TestSummary.ps1') -ResultsPath $fixture -SummaryPath $summaryFile }
        catch { $rejected = $true }
        Assert-That $rejected 'DTD was accepted'
    }
    Test-Case 'Sonar import removes generated points and preserves handwritten coverage by module' {
        $coverageInput = Join-Path $fixture 'raw'
        $coverageOutput = Join-Path $fixture 'sonar'
        New-Item -ItemType Directory -Path $coverageInput | Out-Null
        $rawReport = Join-Path $coverageInput 'example.opencover.xml'
        Set-Content -LiteralPath $rawReport -Value @'
<CoverageSession><Modules>
  <Module><Files>
    <File uid="1" fullPath="/src/Scanner.cs" />
    <File uid="2" fullPath="/_/src/obj/Release/LoggerMessage.g.cs" />
  </Files><Classes><Class><Methods><Method><FileRef uid="2" />
    <SequencePoints><SequencePoint fileid="2" sl="864" vc="0" /><SequencePoint fileid="1" sl="12" vc="1" /></SequencePoints>
    <BranchPoints><BranchPoint fileid="2" sl="864" vc="0" /><BranchPoint sl="864" vc="0" /><BranchPoint fileid="1" sl="12" vc="1" /></BranchPoints>
  </Method></Methods></Class></Classes></Module>
  <Module><Files><File uid="2" fullPath="/src/Other.cs" /></Files><Classes><Class><Methods><Method>
    <FileRef uid="2" /><SequencePoints><SequencePoint fileid="2" sl="864" vc="5" /></SequencePoints>
  </Method></Methods></Class></Classes></Module>
</Modules></CoverageSession>
'@
        $rawHash = (Get-FileHash $rawReport).Hash
        & (Join-Path $root 'build/Prepare-SonarCoverage.ps1') -InputPath $coverageInput -OutputPath $coverageOutput
        [xml] $prepared = Get-Content (Join-Path $coverageOutput 'example.opencover.xml') -Raw
        Assert-That ($prepared.SelectNodes('//File[contains(@fullPath,"obj")]').Count -eq 0) 'Generated file retained'
        Assert-That ($prepared.SelectNodes('//Module[1]//SequencePoint[@sl="864"] | //Module[1]//BranchPoint[@sl="864"]').Count -eq 0) 'Generated points retained'
        Assert-That ($prepared.SelectNodes('//Module[1]//SequencePoint[@sl="12" and @vc="1"] | //Module[1]//BranchPoint[@sl="12" and @vc="1"]').Count -eq 2) 'Handwritten points changed'
        Assert-That ($prepared.SelectSingleNode('//Module[1]//FileRef').uid -eq '1') 'Mixed-method file reference not repaired'
        Assert-That ($prepared.SelectSingleNode('//Module[2]//SequencePoint').vc -eq '5') 'File IDs leaked across modules'
        Assert-That ((Get-FileHash $rawReport).Hash -eq $rawHash) 'Raw artifact was modified'
        $rejected = $false
        try { & (Join-Path $root 'build/Prepare-SonarCoverage.ps1') -InputPath $coverageInput -OutputPath $coverageInput }
        catch { $rejected = $true }
        Assert-That $rejected 'Overwriting raw coverage was permitted'
    }
    Test-Case 'Sonar preserves different reports with the same filename in separate artifacts' {
        $nestedInput = Join-Path $fixture 'nested-raw'
        $nestedOutput = Join-Path $fixture 'nested-sonar'
        $reportXml = '<CoverageSession><Modules><Module><Files><File uid="1" fullPath="/src/Example.cs" /></Files><Classes><Class><Methods><Method><FileRef uid="1" /><SequencePoints><SequencePoint fileid="1" sl="12" vc="__HITS__" /></SequencePoints></Method></Methods></Class></Classes></Module></Modules></CoverageSession>'
        foreach ($artifact in @('coverage-unit', 'coverage-cloud')) {
            $directory = Join-Path $nestedInput $artifact
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            $hits = if ($artifact -eq 'coverage-unit') { '3' } else { '7' }
            Set-Content -LiteralPath (Join-Path $directory 'runner.opencover.xml') -Value $reportXml.Replace('__HITS__', $hits)
        }
        & (Join-Path $root 'build/Prepare-SonarCoverage.ps1') -InputPath $nestedInput -OutputPath $nestedOutput
        Assert-That (@(Get-ChildItem -LiteralPath $nestedOutput -Recurse -Filter '*.opencover.xml').Count -eq 2) 'A same-named report was lost'
        foreach ($artifact in @('coverage-unit', 'coverage-cloud')) {
            [xml] $report = Get-Content (Join-Path $nestedOutput "$artifact/runner.opencover.xml") -Raw
            $expected = if ($artifact -eq 'coverage-unit') { '3' } else { '7' }
            Assert-That ($report.SelectSingleNode('//SequencePoint').vc -eq $expected) 'Coverage reports overwrote each other'
        }
        $rejected = $false
        try { & (Join-Path $root 'build/Prepare-SonarCoverage.ps1') -InputPath $nestedInput -OutputPath $nestedOutput }
        catch { $rejected = $_.Exception.Message -match 'old reports' }
        Assert-That $rejected 'Existing nested reports were overwritten'
        $rejected = $false
        try { & (Join-Path $root 'build/Prepare-SonarCoverage.ps1') -InputPath $nestedInput -OutputPath (Join-Path $nestedInput 'prepared') }
        catch { $rejected = $_.Exception.Message -match 'separate' }
        Assert-That $rejected 'Output inside the raw artifact tree was allowed'
    }
    Test-Case 'Malformed coverage fails with the artifact path and XML error' {
        $badInput = Join-Path $fixture 'malformed-raw/coverage-unit'
        New-Item -ItemType Directory -Path $badInput -Force | Out-Null
        $badReport = Join-Path $badInput 'runner.opencover.xml'
        Set-Content -LiteralPath $badReport -Value '<CoverageSession />trailing-data'
        $rawHash = (Get-FileHash $badReport).Hash
        $errorMessage = ''
        try { & (Join-Path $root 'build/Prepare-SonarCoverage.ps1') -InputPath (Join-Path $fixture 'malformed-raw') -OutputPath (Join-Path $fixture 'malformed-sonar') }
        catch { $errorMessage = $_.Exception.Message }
        Assert-That ($errorMessage -match 'coverage-unit[\\/]runner.opencover.xml' -and $errorMessage -match 'root level') 'Missing report path or XML parse diagnostic'
        Assert-That ((Get-FileHash $badReport).Hash -eq $rawHash) 'Malformed raw report was modified'
    }
}
finally {
    # Only delete the freshly created, task-specific temporary fixture tree.
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $tempRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))
    if ((Split-Path $resolvedFixture -Parent) -ne $tempRoot -or
        (Split-Path $resolvedFixture -Leaf) -notmatch '^SlimMessageBus-summary-[a-f0-9]{32}$') {
        throw "Refusing to remove unexpected fixture path: $resolvedFixture"
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse
}

Write-Host "$script:passed CI policy and summary tests passed."
