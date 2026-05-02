import http from "k6/http";
import { check, sleep } from "k6";
import { Trend } from "k6/metrics";

const latency = new Trend("balance_lookup_ms");

export const options = {
  stages: [
    { duration: "30s", target: 50 },
    { duration: "2m", target: 100 },
    { duration: "30s", target: 0 },
  ],
  thresholds: {
    http_req_duration: ["p(95)<1200"],
    http_req_failed: ["rate<0.01"],
    balance_lookup_ms: ["p(99)<5000"],
  },
};

const baseUrl = __ENV.API_URL || "http://localhost:5000";
const customerIds = JSON.parse(open(__ENV.IDS_FILE || "./customer-ids.json"));

export default function () {
  const id = customerIds[Math.floor(Math.random() * customerIds.length)];
  const t0 = Date.now();
  const res = http.get(`${baseUrl}/api/customers/${id}`);
  latency.add(Date.now() - t0);

  check(res, {
    "status 200": (r) => r.status === 200,
    "has totalPoints": (r) => r.json("totalPoints") !== undefined,
  });

  sleep(0.1);
}
