import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const successes = new Counter('redeem_success');
const stockExhausted = new Counter('redeem_stock_exhausted');
const insufficient = new Counter('redeem_insufficient');

export const options = {
    scenarios: {
        burst: {
            executor: 'shared-iterations',
            vus: 100,
            iterations: 1000,
            maxDuration: '1m',
        },
    },
    thresholds: {
        http_req_failed: ['rate<0.005'],
    },
};

const baseUrl = __ENV.API_URL || 'http://localhost:5000';
const richCustomers = JSON.parse(open(__ENV.RICH_IDS_FILE || './rich-customer-ids.json'));
const scarceRewardId = __ENV.REWARD_ID;
if (!scarceRewardId) {
    throw new Error('REWARD_ID env variable is required (output by --seed CLI mode).');
}

export default function () {
    const customerId = richCustomers[Math.floor(Math.random() * richCustomers.length)];
    const res = http.post(
        `${baseUrl}/api/customers/${customerId}/redeem`,
        JSON.stringify({ rewardId: scarceRewardId }),
        { headers: { 'content-type': 'application/json' } }
    );

    if (res.status === 200) {
        successes.add(1);
    } else if (res.status === 422) {
        const body = (res.body || '').toString();
        if (body.toLowerCase().includes('out of stock')) stockExhausted.add(1);
        else if (body.toLowerCase().includes('insufficient')) insufficient.add(1);
    }

    check(res, {
        'status is 200/422/404': (r) => [200, 422, 404].includes(r.status),
        'no server error': (r) => r.status < 500,
    });
}
