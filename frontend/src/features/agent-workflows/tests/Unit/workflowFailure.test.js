import { describe, expect, it } from 'vitest'
import { explainStepFailure, explainWorkflowFailure } from '../../lib/workflowFailure.js'

describe('explainStepFailure', () => {
  it('returns null for a step that did not fail', () => {
    expect(explainStepFailure({ stepNo: 2, agentRole: 'DomainAnalysis', status: 'Succeeded' })).toBeNull()
    expect(explainStepFailure(undefined)).toBeNull()
  })

  it('explains zero_eligible_agencies from Agent 2 in plain language', () => {
    const result = explainStepFailure({
      stepNo: 2,
      agentRole: 'DomainAnalysis',
      status: 'Failed',
      errorMessage: 'zero_eligible_agencies',
    })

    expect(result.title).toBe('No suitable carrier available')
    expect(result.agentTitle).toBe('Agent 2: Domain Analysis')
    expect(result.message).toMatch(/vehicle and driver/i)
    expect(result.technicalDetail).toBe('zero_eligible_agencies')
  })

  it('treats the Agent 3 empty-shortlist message the same way', () => {
    const result = explainStepFailure({
      stepNo: 3,
      agentRole: 'MatchingPricing',
      status: 'Failed',
      errorMessage: 'No eligible candidate agencies available in shortlist',
    })
    expect(result.title).toBe('No suitable carrier available')
  })

  it('explains routing and pricing outages', () => {
    const result = explainStepFailure({
      stepNo: 3,
      agentRole: 'MatchingPricing',
      status: 'Failed',
      errorMessage: 'hold for review: routing lookup failed after retry',
    })
    expect(result.title).toBe('Route or price could not be calculated')
  })

  it('keeps the Agent 4 explanation text for safety failures', () => {
    const result = explainStepFailure({
      stepNo: 4,
      agentRole: 'ValidationSafety',
      status: 'Failed',
      errorMessage: 'Price deviates too far from the formula estimate',
    })
    expect(result.title).toBe('Safety checks did not approve this match')
    expect(result.message).toBe('Price deviates too far from the formula estimate')
  })

  it('falls back to the raw message for an unknown failure', () => {
    const result = explainStepFailure({
      stepNo: 1,
      agentRole: 'Planner',
      status: 'Failed',
      errorMessage: 'something unexpected',
    })
    expect(result.title).toBe('Matching could not be completed')
    expect(result.message).toBe('something unexpected')
  })
})

describe('explainWorkflowFailure', () => {
  it('returns null unless the run is Failed', () => {
    const steps = [{ stepNo: 2, agentRole: 'DomainAnalysis', status: 'Failed', errorMessage: 'zero_eligible_agencies' }]
    expect(explainWorkflowFailure(steps, 'AwaitingApproval')).toBeNull()
    expect(explainWorkflowFailure(steps, 'Running')).toBeNull()
  })

  it('uses the earliest failed step', () => {
    const steps = [
      { stepNo: 3, agentRole: 'MatchingPricing', status: 'Failed', errorMessage: 'hold for review: x' },
      { stepNo: 2, agentRole: 'DomainAnalysis', status: 'Failed', errorMessage: 'zero_eligible_agencies' },
    ]
    expect(explainWorkflowFailure(steps, 'Failed').agentRole).toBe('DomainAnalysis')
  })

  it('still returns a generic explanation when no step recorded a reason', () => {
    const result = explainWorkflowFailure([], 'Failed')
    expect(result.title).toBe('Matching could not be completed')
    expect(result.agentTitle).toBeNull()
  })
})
