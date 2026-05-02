# Performance tests (k6)

Two k6 scripts targeting the most performance-sensitive endpoints:

- `balance-load.js` — load test on the point-balance lookup (`GET /api/customers/{id}`).
- `redeem-stress.js` — stress test on concurrent redemption against a scarce reward.

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
   dotnet run --project Api -- --seed --output perf > perf/scarce-reward-id.txt
   ```

   The seeder writes:
   - `perf/customer-ids.json` — every seeded customer GUID.
   - `perf/rich-customer-ids.json` — customers with `TotalPoints > 100_000`.
   - stdout — the GUID of a freshly inserted reward with `StockQuantity = 50`.

3. Run the balance-lookup load test:

   ```bash
   k6 run -e API_URL=http://localhost:5000 \
          -e IDS_FILE=./perf/customer-ids.json \
          perf/balance-load.js
   ```

4. Run the redeem-stress test:

   ```bash
   k6 run -e API_URL=http://localhost:5000 \
          -e RICH_IDS_FILE=./perf/rich-customer-ids.json \
          -e REWARD_ID=$(cat perf/scarce-reward-id.txt | tr -d '\r\n ') \
          perf/redeem-stress.js
   ```

## Pass criteria

- `balance-load.js`: `http_req_duration p(95) < 200ms`, error rate < 1%.
- `redeem-stress.js`: `redeem_success` equals the scarce reward's initial stock (50) — anything higher means we have an over-redemption bug. No 5xx is returned (failures are clean 422/404).

## Notes

- The seeder is deterministic (`seed: 42`) so re-running yields identical aggregate counts.
- The optimistic-concurrency token on `Reward` (and the redeem handler's retry loop) means
  the scarce-reward stress test can never over-redeem; spurious conflicts are retried until
  the stock is genuinely exhausted, at which point the next attempt sees `StockQuantity == 0`
  and returns 422.
