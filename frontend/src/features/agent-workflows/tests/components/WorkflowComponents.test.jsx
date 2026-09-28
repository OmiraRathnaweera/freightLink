import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import WorkflowStepper from '../../components/WorkflowStepper.jsx'
import ValidationChecklist from '../../components/ValidationChecklist.jsx'
import AlternateCandidatesList from '../../components/AlternateCandidatesList.jsx'

describe('WorkflowStepper', () => {
  it('renders all 4 pipeline agents and their roles', () => {
    const steps = [
      { stepNo: 1, agentRole: 'Planner', status: 'Completed', durationMs: 145 },
      { stepNo: 2, agentRole: 'DomainAnalysis', status: 'Completed', durationMs: 210 },
      { stepNo: 3, agentRole: 'MatchingPricing', status: 'Completed', durationMs: 380 },
      { stepNo: 4, agentRole: 'ValidationSafety', status: 'Completed', durationMs: 95 },
    ]

    render(<WorkflowStepper steps={steps} workflowStatus="PendingReview" />)

    expect(screen.getByText('Agent 1: Planner')).toBeInTheDocument()
    expect(screen.getByText('Agent 2: Domain Analysis')).toBeInTheDocument()
    expect(screen.getByText('Agent 3: Matching & Pricing')).toBeInTheDocument()
    expect(screen.getByText('Agent 4: Validation & Safety')).toBeInTheDocument()
    expect(screen.getByText('380 ms')).toBeInTheDocument()
  })

  it('never shows a step as Completed unless the backend actually reported it', () => {
    // Regression test: getStepData used to default an unreported step to "Completed" with a
    // fabricated duration whenever workflowStatus looked done - showing a step as passed when no
    // real AgentStep exists behind it (plans/04-backend-integration.md §5's client-side sibling).
    render(<WorkflowStepper steps={[]} workflowStatus="NotStarted" />)

    expect(screen.getAllByText('Pending')).toHaveLength(4)
    expect(screen.queryByText('Passed')).not.toBeInTheDocument()
    expect(screen.queryByText(/ms$/)).not.toBeInTheDocument()
  })
})

describe('ValidationChecklist', () => {
  it('renders recommendation status and checklist items', () => {
    const validation = {
      recommendation: 'Approve',
      explanation: 'All safety, capacity, and driver regulatory constraints satisfied.',
      checks: [
        { name: 'Cargo Weight & Volume Limits', passed: true, details: 'Within 8,000 kg capacity limit.' },
        { name: 'Hazardous Cargo Restrictions', passed: true, details: 'Standard general freight.' },
      ],
    }

    render(<ValidationChecklist validation={validation} />)

    expect(screen.getByText(/Recommendation: Approve/i)).toBeInTheDocument()
    expect(screen.getByText(/All safety, capacity, and driver regulatory constraints satisfied/)).toBeInTheDocument()
    expect(screen.getByText('Cargo Weight & Volume Limits')).toBeInTheDocument()
    expect(screen.getByText('Hazardous Cargo Restrictions')).toBeInTheDocument()
  })
})

describe('AlternateCandidatesList', () => {
  it('renders alternate candidates and triggers onSelectAgency', async () => {
    const user = userEvent.setup()
    const handleSelect = vi.fn()

    const candidates = [
      {
        agencyId: 'agency-222',
        name: 'Colombo Express Haulers',
        yardAddress: '88 Kandy Rd, Kelaniya',
        rank: 2,
        positioningDistanceKm: 22.1,
        positioningEtaMinutes: 38,
        eligible: true,
      },
      {
        agencyId: 'agency-333',
        name: 'Southern Freightways',
        yardAddress: '15 Galle Rd, Dehiwala',
        rank: 3,
        positioningDistanceKm: 31.4,
        positioningEtaMinutes: 52,
        eligible: true,
      },
    ]

    render(
      <AlternateCandidatesList
        candidates={candidates}
        selectedAgencyId="agency-111"
        onSelectAgency={handleSelect}
        isMatched={false}
      />
    )

    expect(screen.getByText('Colombo Express Haulers')).toBeInTheDocument()
    expect(screen.getByText('Southern Freightways')).toBeInTheDocument()
    expect(screen.getByText('#2')).toBeInTheDocument()
    expect(screen.getByText('#3')).toBeInTheDocument()

    const chooseBtn = screen.getByTestId('select-carrier-agency-222')
    await user.click(chooseBtn)

    expect(handleSelect).toHaveBeenCalledWith('agency-222')
  })
})
