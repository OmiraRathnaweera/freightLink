/**
 * k6 load test proxying Agentic AI pipeline latency: repeatedly drives the same internal
 * workflow-run + 4-step-report callback sequence the Python agent service makes for one load
 * match (see FreightLink.Api.Tests/EndToEnd/LoadToTripWorkflowEndToEndTests.cs for the same
 * sequence used functionally). This measures the BACKEND's side of that exchange — persisting a
 * run, four steps, and the resulting AwaitingApproval transition — under concurrent load; it does
 * not invoke a real LLM (neither should this backend endpoint, which is dumb-audit-trail storage,
 * not itself calling OpenAI). Pair this with the Python agent service's own timing logs for full
 * end-to-end Agentic AI latency (planner/domain-analysis/matching-pricing/validation LLM calls
 * happen entirely on that side, not here).
 *
 * Run:
 *   API_BASE_URL=http://localhost:5159 INTERNAL_API_KEY=<value from backend/.env> \
 *     k6 run PerformanceTests/agent-workflow-latency.perf.js
 */
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL = __ENV.API_BASE_URL || 'http://localhost:5159';
const INTERNAL_API_KEY = __ENV.INTERNAL_API_KEY;

const workflowFailureRate = new Rate('agent_workflow_failure_rate');
const fullSequenceDuration = new Trend('agent_workflow_full_sequence_duration', true);

export const options = {
  scenarios: {
    ramping_concurrency: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 5 },
        { duration: '40s', target: 5 },
        { duration: '20s', target: 20 },
        { duration: '40s', target: 20 },
        { duration: '20s', target: 0 },
      ],
    },
  },
  thresholds: {
    agent_workflow_failure_rate: ['rate<0.01'],
    // The full 5-call sequence (create run + 4 step reports) completing under 1.5s at load is
    // this script's proxy for "Agentic AI latency" on the backend-persistence side.
    agent_workflow_full_sequence_duration: ['p(95)<1500'],
  },
};

export function setup() {
  if (!INTERNAL_API_KEY) {
    throw new Error('INTERNAL_API_KEY env var is required (matches backend/.env INTERNAL_API_KEY).');
  }

  const email = `perf-agent-shipper-${Date.now()}@example.com`;
  const password = 'Sup3r$ecret1';

  http.post(
    `${BASE_URL}/api/v1/auth/register/shipper`,
    JSON.stringify({
      email,
      password,
      fullName: 'Perf Agent Shipper',
      companyName: 'Perf Agent Co',
      billingAddress: '1 Perf Agent Lane, Colombo',
    }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  const loginRes = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  const accessToken = loginRes.json('accessToken');

  const meRes = http.get(`${BASE_URL}/api/v1/auth/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  const shipperUserId = meRes.json('userId');

  return { accessToken, shipperUserId };
}

function createPostedLoad(accessToken) {
  const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${accessToken}` };
  const res = http.post(
    `${BASE_URL}/api/v1/loads`,
    JSON.stringify({
      cargoDescription: 'k6 agent-latency test cargo',
      weightKg: 500,
      volumeM3: 3,
      pickupAddress: '123 Perf Pickup Street, Colombo',
      pickupLat: 6.93,
      pickupLng: 79.85,
      dropoffAddress: '456 Perf Dropoff Road, Kandy',
      dropoffLat: 7.29,
      dropoffLng: 80.63,
      pickupWindowStart: new Date(Date.now() + 2 * 3600 * 1000).toISOString(),
      pickupWindowEnd: new Date(Date.now() + 6 * 3600 * 1000).toISOString(),
      postImmediately: true,
    }),
    { headers }
  );
  return res.json('loadId');
}

export default function (data) {
  const loadId = createPostedLoad(data.accessToken);
  if (!loadId) {
    workflowFailureRate.add(true);
    return;
  }

  const internalHeaders = { 'Content-Type': 'application/json', 'X-Internal-Api-Key': INTERNAL_API_KEY };
  const start = Date.now();
  let ok = true;

  const runRes = http.post(
    `${BASE_URL}/internal/agent-workflow-runs`,
    JSON.stringify({ loadId, triggeredByUserId: data.shipperUserId, attemptNo: 1 }),
    { headers: internalHeaders }
  );
  ok = ok && check(runRes, { 'create workflow run: 2xx': (r) => r.status >= 200 && r.status < 300 });
  const workflowRunId = runRes.json('workflowRunId');

  const steps = [
    { stepNo: 1, agentRole: 'Planner', outputJson: '{"objective":"Match load"}' },
    { stepNo: 2, agentRole: 'DomainAnalysis', outputJson: '{"candidates":[]}' },
    { stepNo: 3, agentRole: 'MatchingPricing', outputJson: '{"proposedPrice":22000}' },
    { stepNo: 4, agentRole: 'ValidationSafety', outputJson: '{"recommendation":"Approve"}' },
  ];

  for (const step of steps) {
    const stepRes = http.post(
      `${BASE_URL}/internal/agent-workflow-runs/${workflowRunId}/steps`,
      JSON.stringify({
        stepNo: step.stepNo,
        agentRole: step.agentRole,
        status: 'Succeeded',
        outputJson: step.outputJson,
        startedAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
      }),
      { headers: internalHeaders }
    );
    ok = ok && check(stepRes, { [`report step ${step.stepNo}: 2xx`]: (r) => r.status >= 200 && r.status < 300 });
  }

  fullSequenceDuration.add(Date.now() - start);
  workflowFailureRate.add(!ok);

  sleep(0.5);
}
