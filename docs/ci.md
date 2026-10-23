# Continuous integration

The `build` GitHub Actions workflow separates contributor validation from tests that use cloud credentials.

| Trigger | Build, unit tests, local integration tests | Cloud integration tests | SonarCloud |
| --- | --- | --- | --- |
| Pull request, including a fork | After any required GitHub workflow approval | Never | Never |
| Push to `feature/*` | Automatic | Never | Never |
| Push to `master` or `release/*` | Automatic | Automatic | Automatic |
| Manual run from `master` or `release/*` | Automatic | After protected environment approval | Never |

Local integration tests use Docker Compose, Testcontainers, or in-process transports. They receive no GitHub secrets. The SQL Server password in the workflow is a disposable local container password, not a cloud credential. Outbox theory entry points have separate `Transport` traits for Kafka/RabbitMQ and Azure Service Bus, so local cases remain covered on fork PRs.

PR validation checks GitHub's merge commit against the target branch. Test failures fail their jobs; the stable `CI` check requires the build and all local integration jobs to pass. TRX results and coverage are uploaded as artifacts, and the `report` job produces an Actions summary without write permissions. Cloud tests have separate artifact names.

## Repository settings

These settings live in GitHub, outside workflow YAML:

1. In **Settings > Actions > General**, select **Require approval for all external contributors**. Review the full change, including workflow and build files, before choosing **Approve workflows to run** on a fork PR. GitHub permits users with write access to grant this approval.
2. In **Settings > Environments > integration-tests**, set **Required reviewers** to `zarusz`. Keep **Prevent self-review** disabled so the maintainer can approve a run they dispatched. For strict enforcement, disable administrator bypass. Every manual cloud matrix job uses this environment before any checkout, restore, or test execution.
3. Protect `master` and `release/*` so only reviewed code reaches the automatic cloud/Sonar path. Require the **CI** check for merges. Do not require cloud or Sonar checks on fork PRs: those jobs deliberately do not run on PR events. Replace any previously required `.NET Tests` reporter check with **CI**.
4. Prefer read-only default workflow permissions. This workflow explicitly requests `contents: read`, with `pull-requests: read` only for source resolution and manual cloud validation. It requests no OIDC or checks-write permission.

The fork approval policy and the `integration-tests` reviewer gate must remain configured. Naming an environment in YAML alone does not establish approval rules. Ordinary workflow approval does not grant a public fork workflow access to repository secrets.

See GitHub's [fork approval instructions](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/approve-runs-from-forks) and [environment configuration](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments).

## Cloud credentials

The workflow uses the existing repository secrets for automatic trusted-branch runs. Manual cloud runs must additionally pass the `integration-tests` environment gate. If environment-scoped copies of these secrets exist, they override repository values for manual runs.

| Cloud matrix job | Secrets passed to its test process |
| --- | --- |
| AmazonSQS | `amazon_access_key`, `amazon_secret_access_key` |
| AzureServiceBus and Azure outbox cases | `azure_servicebus_connectionstring` |
| AzureEventHub | `azure_eventhub_connectionstring`, `azure_storagecontainer_connectionstring` |

Use dedicated test accounts/resources and credentials with only the required test permissions. Local Kafka, MQTT, RabbitMQ, Redis, and SQL tests no longer consume remote broker credentials. SonarCloud uses `SONAR_TOKEN` only on trusted branch pushes, after the test jobs, and includes their uploaded coverage.

Approving a manual fork cloud run means trusting the selected commit's build and test code with the listed test credentials. Credentials are passed only to the test step, but contributor code runs in that job. This workflow does not publish packages or deploy releases.

## Manual cloud validation

After the CI change is merged, select **Actions > build > Run workflow** and select `master` or a protected `release/*` branch. Leave both inputs empty to test that branch's triggering commit. Cloud jobs then wait for the `integration-tests` environment approval.

To test an open PR before merging, first update it with the selected trusted branch. This ensures its head includes the CI policy tests and the local/cloud outbox grouping. The resolver refuses manual PR runs whose head does not contain the selected trusted branch commit.

1. Review the PR and copy its full 40-character head commit SHA.
2. Dispatch the workflow from `master` or a protected release branch, supplying `pr_number` and `reviewed_sha`. The PR must target `master` or `release/*`.
3. Check the **Source selected for validation** summary. It identifies the fork repository and immutable commit selected by the trusted workflow.
4. Approve the pending cloud jobs through **Review deployments**. They recheck the PR head after approval and refuse to run if the PR changed or closed while waiting.

For example, using GitHub CLI:

```shell
gh pr view 484 --repo zarusz/SlimMessageBus --json headRefOid --jq .headRefOid
gh workflow run build.yml --repo zarusz/SlimMessageBus --ref master -f pr_number=484 -f reviewed_sha=<full-reviewed-head-sha>
```

Manual PR validation uses the reviewed head commit, while ordinary PR validation uses the merge commit. New PR commits require a new review and a new manual dispatch; an old approval cannot select a newer commit. Neither PR comments nor labels grant cloud access. Manual dispatch from a feature branch is rejected.

The workflow change must reach the trusted branch before it can be used there. Re-running an old `pull_request_target` run does not adopt the new trigger; generate a new PR event after the updated workflow is available.

## Checking CI changes locally

The policy tests execute the source-resolution JavaScript with a mocked GitHub API and check fork isolation. They require Python 3.12+, Node.js, and PyYAML:

```shell
python -m pip install -r build/tests/requirements.txt
python -m unittest discover -s build/tests -v
```

Validate workflow syntax with [actionlint](https://github.com/rhysd/actionlint). For outbox test changes, use `dotnet test --list-tests --filter "Category=Integration&Transport=Outbox.Kafka"` (or `Outbox.RabbitMQ` / `Outbox.AzureSB`) on the matching test project to verify the cases selected without connecting to a broker.