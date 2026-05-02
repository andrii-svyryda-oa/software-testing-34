# Task 14 — CI pipeline (GitHub Actions)

## Goal

A CI workflow that runs on every `push` and `pull_request`: restore, build, run unit + integration + database tests with code coverage, and upload reports as artifacts. Performance tests run only on `workflow_dispatch` (they're slow + flaky on shared runners).

## Files to add

```
.github/workflows/ci.yml
.github/workflows/perf.yml
```

## `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

jobs:
  build-and-test:
    name: build + test (.NET 8 / Postgres via Testcontainers)
    runs-on: ubuntu-latest
    timeout-minutes: 25

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore SoftwareTesting.sln

      - name: Build
        run: dotnet build SoftwareTesting.sln -c Release --no-restore /warnaserror

      - name: Unit tests
        run: |
          dotnet test Api.Tests.Unit/Api.Tests.Unit.csproj \
            -c Release --no-build \
            --logger "trx;LogFileName=unit.trx" \
            --logger "GitHubActions;summary.includePassedTests=false" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults/unit

      - name: Integration + database tests
        # Testcontainers picks up the Docker daemon already available on ubuntu-latest.
        run: |
          dotnet test Api.Tests.Integrations/Api.Tests.Integrations.csproj \
            -c Release --no-build \
            --logger "trx;LogFileName=integrations.trx" \
            --logger "GitHubActions;summary.includePassedTests=false" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults/integrations

      - name: Merge coverage and produce summary
        if: always()
        uses: danielpalme/ReportGenerator-GitHub-Action@5.3.11
        with:
          reports: 'TestResults/**/coverage.cobertura.xml'
          targetdir: 'TestResults/coverage-report'
          reporttypes: 'HtmlInline;Cobertura;MarkdownSummaryGithub'

      - name: Append coverage summary to job summary
        if: always()
        run: |
          if [ -f TestResults/coverage-report/SummaryGithub.md ]; then
            cat TestResults/coverage-report/SummaryGithub.md >> $GITHUB_STEP_SUMMARY
          fi

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: |
            TestResults/**/*.trx
            TestResults/coverage-report/**

      - name: Fail if coverage below threshold
        if: success()
        # Soft gate — set to your project target. 80% across Domain+Application is a reasonable bar.
        run: |
          line_rate=$(grep -oP 'line-rate="\K[^"]+' TestResults/coverage-report/Cobertura.xml | head -1)
          pct=$(awk "BEGIN {print $line_rate * 100}")
          echo "Total line coverage: $pct%"
          awk "BEGIN { exit !($line_rate >= 0.80) }"
```

Notes:

- `ubuntu-latest` runners ship Docker — Testcontainers works without extra setup.
- `--no-build` after `dotnet build` keeps the test step fast.
- The `GitHubActions` logger requires the package `GitHubActionsTestLogger` referenced from each test project (or installed globally with `dotnet tool`). Add to `Tests.Common.csproj`:
  ```xml
  <PackageReference Include="GitHubActionsTestLogger" Version="2.4.1" />
  ```
- The coverage gate is intentionally soft (80 %) and global. If you want per-project gates, switch to `coverlet.msbuild` thresholds inside each `.csproj`.

## `.github/workflows/perf.yml`

Triggered manually only — keeps PRs fast and avoids flaky perf failures on shared runners.

```yaml
name: Performance (k6)

on:
  workflow_dispatch:

jobs:
  k6:
    runs-on: ubuntu-latest
    timeout-minutes: 30

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Start Postgres + API via docker compose
        working-directory: perf
        run: |
          docker compose -f docker-compose.perf.yml up -d --build postgres api
          # wait for the API to be ready
          for i in {1..30}; do
            curl -fsS http://localhost:5000/swagger/v1/swagger.json && break || sleep 2;
          done

      - name: Seed database
        working-directory: perf
        run: |
          dotnet run --project ../Api -- --seed > scarce-reward-id.txt
          test -s scarce-reward-id.txt

      - name: Setup k6
        uses: grafana/setup-k6-action@v1

      - name: balance-load
        working-directory: perf
        env:
          API_URL: http://localhost:5000
        run: k6 run --quiet --summary-export balance-load.json balance-load.js

      - name: redeem-stress
        working-directory: perf
        env:
          API_URL: http://localhost:5000
          REWARD_ID: ${{ env.REWARD_ID }}
        run: |
          export REWARD_ID=$(cat scarce-reward-id.txt)
          k6 run --quiet --summary-export redeem-stress.json redeem-stress.js

      - name: Upload k6 reports
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: k6-results
          path: perf/*.json

      - name: Tear down
        if: always()
        working-directory: perf
        run: docker compose -f docker-compose.perf.yml down -v
```

## Branch protection (manual setup)

After this workflow lands and runs successfully once on `main`, configure on GitHub:

- Branch protection rule for `main`:
  - Require pull request before merging.
  - Require status check `build + test (.NET 8 / Postgres via Testcontainers)` to pass.
  - Require linear history (optional).

This is a repo-settings change, not a code change — document it in the PR description so reviewers know to apply it.

## Acceptance criteria

- A trivial PR (e.g. README typo) triggers the `CI` workflow and it passes green.
- Test results and the coverage report appear in the PR's "Checks" tab and as artifacts.
- Performance workflow can be triggered from the Actions tab (`workflow_dispatch`) and runs to completion.
- A PR that introduces a regression in the redeem flow (e.g. removing the EF concurrency token) fails CI on the database test from task 12.

## Out of scope

- Deploy steps. The reference project deploys to Azure App Service; for this assessment, no deploy is required by the spec.
- Trigger-on-tag releases.

## Commit message

```
Task 14: CI pipeline (GitHub Actions)

ci.yml: restore + build (warnings as errors) + unit + integration/database
tests with code-coverage report and 80% global gate, on every push/PR.
perf.yml: workflow_dispatch-only k6 perf run via docker-compose
(balance-load + redeem-stress) with artifact upload.
```
