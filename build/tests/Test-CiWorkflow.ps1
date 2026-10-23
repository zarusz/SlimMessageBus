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
}
finally {
    Remove-Item -LiteralPath $resultFile -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $summaryFile -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fixture
}

Write-Host "$script:passed CI policy and summary tests passed."
