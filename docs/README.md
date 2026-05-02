# Documentation index

Source-of-truth task requirement: [`task_requirement.txt`](task_requirement.txt) (Завдання 34: Програма лояльності).

Architecture / conventions: [`../AGENTS.md`](../AGENTS.md). Read it first.

## Tasks (one PR per task)

Each file under [`tasks/`](tasks/) describes a single, self-contained PR. Tasks are ordered by their dependency chain — finish task NN before starting NN+1 unless the doc says otherwise.

| #  | Title | File |
|----|-------|------|
| 01 | Solution scaffold (clean architecture skeleton) | [`tasks/01-solution-scaffold.md`](tasks/01-solution-scaffold.md) |
| 02 | Domain layer — entities, IDs, enums, invariants | [`tasks/02-domain-layer.md`](tasks/02-domain-layer.md) |
| 03 | Application layer — common building blocks | [`tasks/03-application-common.md`](tasks/03-application-common.md) |
| 04 | Application — Customers (register / earn / redeem) | [`tasks/04-application-customers.md`](tasks/04-application-customers.md) |
| 05 | Application — Rewards & Tier query | [`tasks/05-application-rewards-tier.md`](tasks/05-application-rewards-tier.md) |
| 06 | Application — Point transaction history & expiration | [`tasks/06-application-history-expiration.md`](tasks/06-application-history-expiration.md) |
| 07 | Infrastructure — DbContext, configurations, repositories, migrations | [`tasks/07-infrastructure-persistence.md`](tasks/07-infrastructure-persistence.md) |
| 08 | API — controllers, DTOs, error handlers, Program.cs | [`tasks/08-api-layer.md`](tasks/08-api-layer.md) |
| 09 | Test infrastructure (`Tests.Common` + `Test.Data` + seeders) | [`tasks/09-test-infrastructure.md`](tasks/09-test-infrastructure.md) |
| 10 | Unit tests | [`tasks/10-unit-tests.md`](tasks/10-unit-tests.md) |
| 11 | Integration tests (WebApplicationFactory) | [`tasks/11-integration-tests.md`](tasks/11-integration-tests.md) |
| 12 | Database tests (Testcontainers — consistency & concurrency) | [`tasks/12-database-tests.md`](tasks/12-database-tests.md) |
| 13 | Performance tests (k6) | [`tasks/13-performance-tests.md`](tasks/13-performance-tests.md) |
| 14 | CI pipeline (GitHub Actions) | [`tasks/14-ci-pipeline.md`](tasks/14-ci-pipeline.md) |

## How to work a task

1. Create a branch named `task/NN-short-name`.
2. Read the corresponding doc top-to-bottom — every doc has explicit "Files to add/edit", "Acceptance criteria" and "Verification" sections.
3. Implement only what the doc lists. If you discover a need that isn't covered, update the doc in the same PR.
4. Run `dotnet build` and `dotnet test` locally. From task 14 onward, CI must also be green.
5. Open the PR with title `Task NN: <title>` and link it to this doc.
