import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const successes = new Counter('redeem_success');
const stockExhausted = new Counter('redeem_stock_exhausted');
const insufficient = new Counter('redeem_insufficient');
const unexpected = new Counter('redeem_unexpected_status');

http.setResponseCallback(
    http.expectedStatuses({ min: 200, max: 299 }, 422, 404)
);

export const options = {
    scenarios: {
        burst: {
            executor: 'shared-iterations',
            vus: 50,
            iterations: 1000,
            maxDuration: '1m',
        },
    },
    thresholds: {
        http_req_failed: ['rate<0.01'],
        redeem_unexpected_status: ['count==0'],
    },
};

const baseUrl = __ENV.API_URL || 'http://localhost:5000';
const richCustomers = JSON.parse(open(__ENV.RICH_IDS_FILE || './rich-customer-ids.json'));
const scarceRewardId = __ENV.REWARD_ID;
if (!scarceRewardId) {
    throw new Error(
        'REWARD_ID env variable is required (written by --seed CLI mode to ' +
        'Api/perf/scarce-reward-id.txt).'
    );
}

export function setup() {
    const res = http.get(`${baseUrl}/api/rewards`);
    if (res.status !== 200) {
        throw new Error(
            `Pre-check GET /api/rewards failed: status=${res.status}. ` +
            `Is the API running on ${baseUrl}?`
        );
    }
    const rewards = res.json();
    const scarce = Array.isArray(rewards)
        ? rewards.find((r) => r && r.id === scarceRewardId)
        : null;
    if (!scarce) {
        throw new Error(
            `Scarce reward ${scarceRewardId} is not in GET /api/rewards (the endpoint ` +
            `returns only active + in-stock rewards). It was almost certainly drained ` +
            `by a previous run. Re-seed the database before running this test:\n` +
            `  dotnet run --project Api -- --seed --output perf\n` +
            `That regenerates Api/perf/scarce-reward-id.txt with a fresh GUID and ` +
            `restocks the reward to 50.`
        );
    }
    return { initialStock: scarce.stockQuantity };
}

export default function (data) {
    const customerId = richCustomers[Math.floor(Math.random() * richCustomers.length)];
    const res = http.post(
        `${baseUrl}/api/customers/${customerId}/redeem`,
        JSON.stringify({ rewardId: scarceRewardId }),
        { headers: { 'content-type': 'application/json' } }
    );

    if (res.status === 200) {
        successes.add(1);
    } else if (res.status === 422) {
        const body = (res.body || '').toString().toLowerCase();
        if (body.includes('out of stock')) stockExhausted.add(1);
        else if (body.includes('insufficient')) insufficient.add(1);
    } else if (res.status !== 404) {
        const bodySnippet = (res.body || '')
            .toString()
            .replace(/\s+/g, ' ')
            .slice(0, 300);
        console.error(
            `redeem_unexpected status=${res.status} ` +
            `error_code=${res.error_code || 0} ` +
            `error="${res.error || ''}" ` +
            `body="${bodySnippet}"`
        );
        unexpected.add(1);
    }

    check(res, {
        'expected status (200/422/404)': (r) => [200, 422, 404].includes(r.status),
    });
}

export function teardown(data) {
    console.log(
        `redeem-stress finished. initial_stock=${data.initialStock} ` +
        `(redeem_success should equal this; lower = previous-run leftovers, ` +
        `higher = OVER-REDEMPTION BUG).`
    );
}
