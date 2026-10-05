// Turns the raw failure strings the agent pipeline records on a failed AgentStep (for example
// `zero_eligible_agencies`) into a message a shipper can act on. The backend already returns each
// step's errorMessage; without this the UI only showed a red "Failed" label.

const AGENT_TITLES = {
  Planner: 'Agent 1: Planner',
  DomainAnalysis: 'Agent 2: Domain Analysis',
  MatchingPricing: 'Agent 3: Matching & Pricing',
  ValidationSafety: 'Agent 4: Validation & Safety',
}

const NO_CARRIER = {
  title: 'No suitable carrier available',
  message:
    "None of the active carriers has an available vehicle and driver that can carry this load's weight and volume.",
  hint: "Check the load's weight and volume, or split it into smaller loads. You can try again once carriers have free capacity.",
}

const SYSTEM_PROBLEM = {
  title: 'Matching hit a system problem',
  message: 'The matching service could not finish this request.',
  hint: 'Try again shortly. If it keeps failing, contact support.',
}

function classify(agentRole, rawMessage) {
  const raw = (rawMessage || '').trim()

  if (/zero_eligible_agencies/i.test(raw) || /no eligible candidate agencies/i.test(raw)) {
    return NO_CARRIER
  }
  if (/^hold for review/i.test(raw)) {
    return {
      title: 'Route or price could not be calculated',
      message: 'The routing or pricing service did not respond for this load.',
      hint: 'Try again in a few minutes.',
    }
  }
  if (/malformed_load_payload/i.test(raw)) {
    return {
      title: 'Load details could not be read',
      message: 'Some of the load details are missing or invalid, so matching could not start.',
      hint: 'Edit the load, check the pickup, delivery and cargo details, then try again.',
    }
  }
  if (/create_workflow_run_failed|report_step_failed|planner_llm_failed|unhandled exception/i.test(raw)) {
    return SYSTEM_PROBLEM
  }
  if (agentRole === 'ValidationSafety') {
    return {
      title: 'Safety checks did not approve this match',
      message: raw || 'The recommended match did not pass the safety validation.',
      hint: 'Request a new recommendation. If this keeps happening, contact support.',
    }
  }
  return {
    title: 'Matching could not be completed',
    message: raw || 'The matching pipeline stopped before it produced a recommendation.',
    hint: 'Try again shortly. If it keeps failing, contact support.',
  }
}

/** Plain-language explanation for one failed step, or null if the step did not fail. */
export function explainStepFailure(step) {
  if (!step || step.status !== 'Failed') return null
  const rawMessage = step.errorMessage || ''
  return {
    ...classify(step.agentRole, rawMessage),
    agentRole: step.agentRole,
    agentTitle: AGENT_TITLES[step.agentRole] || `Step ${step.stepNo}`,
    technicalDetail: rawMessage || null,
  }
}

/**
 * Explanation for a failed run: the earliest failed step, or a generic one when the run is
 * Failed but no step recorded a reason. Returns null when the run has not failed.
 */
export function explainWorkflowFailure(steps = [], workflowStatus) {
  if (workflowStatus !== 'Failed') return null

  const failedStep = [...steps]
    .filter((s) => s.status === 'Failed')
    .sort((a, b) => (a.stepNo ?? 0) - (b.stepNo ?? 0))[0]

  const explained = explainStepFailure(failedStep)
  if (explained) return explained

  return {
    ...classify(undefined, ''),
    agentRole: null,
    agentTitle: null,
    technicalDetail: null,
  }
}
