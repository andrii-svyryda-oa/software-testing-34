# Performance tests (k6)

Two k6 scripts targeting the most performance-sensitive endpoints:

- `balance-load.js` — load test on the point-balance lookup (`GET /api/customers/{id}`).
- `redeem-stress.js` — stress test on concurrent redemption against a scarce reward.

## API port

The API always runs on **`http://localhost:5000`** (HTTP only). This is hard-wired in:

- `Api/appsettings.json` — `"Urls": "http://localhost:5000"`.
- `Api/Properties/launchSettings.json` — `applicationUrl: http://localhost:5000`.
- `perf/docker-compose.perf.yml` — `ASPNETCORE_URLS=http://+:5000` and `ports: ['5000:5000']`.

All commands and k6 scripts in this README assume that fixed port. If port 5000 is busy, free
it before starting the API rather than overriding the URL piecemeal.

## Prerequisites

- Docker (Docker Desktop on Windows / Docker Engine on Linux).
- [`k6`](https://k6.io/docs/get-started/installation/) CLI (or use the Grafana k6 Docker image).
- .NET SDK 8 if you want to run the seeder outside the container.

## Local run

1. Bring up Postgres + the API:

   ```bash
   docker compose -f perf/docker-compose.perf.yml up -d --build postgres api
   # wait for the API to be ready
   for i in {1..30}; do
     curl -fsS http://localhost:5000/swagger/v1/swagger.json && break || sleep 2;
   done
   ```

2. Seed >=10 000 records and capture the scarce reward GUID:

   ```bash
   dotnet run --project Api -- --seed --output perf
   ```

   The seeder writes (relative to the `Api/` working directory used by `dotnet run`):
   - `Api/perf/customer-ids.json` — every seeded customer GUID.
   - `Api/perf/rich-customer-ids.json` — customers with `TotalPoints > 100_000`.
   - `Api/perf/scarce-reward-id.txt` — the GUID of a freshly inserted reward with
     `StockQuantity = 50` (file contains only the GUID, no trailing newline).
   - stdout — the same GUID, echoed for visibility. **Do not redirect stdout to capture
     it**: `dotnet run`'s build output and EF Core's console logger also write there.

3. Run the balance-lookup load test:

   ```bash
   k6 run -e API_URL=http://localhost:5000 \
          -e IDS_FILE=../Api/perf/customer-ids.json \
          perf/balance-load.js
   ```

4. Run the redeem-stress test:

   ```bash
   k6 run -e API_URL=http://localhost:5000 \
          -e RICH_IDS_FILE=../Api/perf/rich-customer-ids.json \
          -e REWARD_ID=$(< Api/perf/scarce-reward-id.txt) \
          perf/redeem-stress.js
   ```

## Pass criteria

- `balance-load.js`: `http_req_duration p(95) < 200ms`, error rate < 1%.
- `redeem-stress.js` enforces two thresholds:
  - `http_req_failed: rate<0.01` — true infrastructure / 5xx failures only. The script
    calls `http.setResponseCallback(http.expectedStatuses(2xx, 422, 404))`, so business
    rejections (out-of-stock, insufficient points) do **not** count as failures here.
    Without that override the metric would be ~100% the moment stock is exhausted.
  - `redeem_unexpected_status: count==0` — any HTTP status outside `{200, 422, 404}` is
    a bug (e.g. a 500 from an unhandled exception or a 409 leaking from the optimistic
    concurrency retry loop). The body of any such response is logged to stderr.
- Manual correctness check (not a k6 threshold because it depends on initial stock,
  which the script can't observe): `redeem_success` should equal the scarce reward's
  starting stock — **50** when the run is preceded by a fresh seed. A higher value
  means over-redemption. A lower value means the reward was already partially or
  fully drained before the run started — re-seed and try again.

## Notes

- **Re-seed before every meaningful redeem-stress run.** The script *consumes* stock,
  so the second run against the same DB will see `StockQuantity = 0` and report
  `redeem_success = 0` / `redeem_stock_exhausted ≈ iterations`. Re-running
  `dotnet run --project Api -- --seed --output perf` wipes the DB, re-creates the
  scarce reward with stock 50, and overwrites `Api/perf/scarce-reward-id.txt` with
  the new GUID, so the k6 command above keeps working unchanged.
- The seeder is deterministic (`seed: 42`) so re-running yields identical aggregate
  counts (the scarce reward GUID itself is fresh each run because `RewardId.New()`
  is non-deterministic — that's why we read it from a file).
- The optimistic-concurrency token on `Reward` (and the redeem handler's retry loop)
  means the scarce-reward stress test can never over-redeem; spurious conflicts are
  retried until the stock is genuinely exhausted, at which point the next attempt
  sees `StockQuantity == 0` and returns 422.
