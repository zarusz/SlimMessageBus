### Pull requests are welcome

Pull requests are welcome. Ideally first state the problem or feature you need and engage in a discussion on gitter or github issues.
A high level design discussion will be needed for new features.

### Sign your work

The sign-off is a simple line at the end of the explanation for the
patch, which certifies that you wrote it or otherwise have the right to
pass it on as an open-source patch.  The rules are pretty simple: if you
can certify the below (from
[developercertificate.org](https://developercertificate.org/)):

```
Developer Certificate of Origin
Version 1.1

Copyright (C) 2004, 2006 The Linux Foundation and its contributors.
660 York Street, Suite 102,
San Francisco, CA 94110 USA

Everyone is permitted to copy and distribute verbatim copies of this
license document, but changing it is not allowed.


Developer's Certificate of Origin 1.1

By making a contribution to this project, I certify that:

(a) The contribution was created in whole or in part by me and I
    have the right to submit it under the open source license
    indicated in the file; or

(b) The contribution is based upon previous work that, to the best
    of my knowledge, is covered under an appropriate open source
    license and I have the right under that license to submit that
    work with modifications, whether created in whole or in part
    by me, under the same open source license (unless I am
    permitted to submit under a different license), as indicated
    in the file; or

(c) The contribution was provided directly to me by some other
    person who certified (a), (b) or (c) and I have not modified
    it.

(d) I understand and agree that this project and the contribution
    are public and that a record of the contribution (including all
    personal information I submit with it, including my sign-off) is
    maintained indefinitely and may be redistributed consistent with
    this project or the open source license(s) involved.
```

then you just add a line to every git commit message:

    Signed-off-by: Joe Smith <joe.smith@email.com>

using your real name (sorry, no pseudonyms or anonymous contributions.) and an
e-mail address under which you can be reached (sorry, no github noreply e-mail
addresses (such as username@users.noreply.github.com) or other non-reachable
addresses are allowed).

On the command line you can use `git commit -s` to sign off the commit.

### CI pipeline (for maintainers)

All CI runs through the single workflow in `.github/workflows/build.yml`, triggered on every push to `master`/`release/*`/`feature/*` and on every pull request.

#### Jobs and order

```
build  ──►  integration-tests (matrix)  ──►  sonar
                                         └──►  report
```

| Job                 | What it does                                                                                                                                                                                                                                                                          |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `build`             | Restores, compiles, runs **unit tests** (no external infrastructure needed), archives NuGet packages and coverage.                                                                                                                                                                    |
| `integration-tests` | Parallel matrix — one runner per transport. Local brokers (Kafka, RabbitMQ, Redis, MQTT, NATS, SQL Server, PostgreSQL, MongoDB) are started via Docker Compose or TestContainers. Cloud transports (Azure Service Bus, Azure Event Hub, Amazon SQS) connect using repository secrets. |
| `sonar`             | Downloads all coverage reports from previous jobs and runs a SonarCloud analysis. Decorates PRs with a quality gate result.                                                                                                                                                           |
| `report`            | Aggregates all `.trx` test result files and publishes a single test report via `dorny/test-reporter`.                                                                                                                                                                                 |

#### Fork PRs and the approval gate

Because integration tests require repository secrets (cloud connection strings, API keys), PRs from forks go through an approval gate:

1. `build` runs immediately — no secrets are needed and no risk to credentials.
2. `integration-tests` is paused at the **`integration-tests` GitHub Environment**, which requires a maintainer to approve.
3. A maintainer **reviews the PR diff** to ensure no test code could exfiltrate secrets (e.g. reading env vars and POSTing them externally), then clicks **"Review deployments → Approve"** on the Actions run page.
4. All matrix legs start in parallel and results are posted back to the PR.

> **Security note:** The workflow YAML always comes from the base repository, so an external contributor cannot modify the pipeline itself. The approval gate is the control point before any secret is injected into a runner that executes fork code.

#### Approving a fork PR's integration tests

1. Open the PR on GitHub.
2. Click the **Actions** tab, find the in-progress `build` workflow run.
3. Click **"Review deployments"** (yellow banner) → tick `integration-tests` → **Approve and deploy**.

Same-repo PRs and direct pushes bypass the gate and run immediately.
