import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AgentWorkflowConsolePage from '../../pages/AgentWorkflowConsolePage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as loadsApi from '../../../loads/api/loadsApi.js'
import * as agentWorkflowsApi from '../../api/agentWorkflowsApi.js'

vi.mock('../../../loads/api/loadsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useLoadsQuery: vi.fn(),
    useLoadDetailQuery: vi.fn(),
  }
})

vi.mock('../../api/agentWorkflowsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useLoadMatchQuery: vi.fn(),
    useTriggerMatchMutation: vi.fn(),
    useConfirmMatchMutation: vi.fn(),
    useLoadMatchHistoryQuery: vi.fn(),
  }
})

const mockLoad = {
  loadId: 'load-1',
  referenceCode: 'LD-0001',
  status: 'Posted',
  pickupAddress: 'Colombo',
  dropoffAddress: 'Kandy',
  weightKg: 5000,
}

const mockMatchData = {
  loadId: 'load-1',
  referenceCode: 'LD-0001',
  loadStatus: 'Posted',
  workflowStatus: 'PendingReview',
  recommendedAgency: {
    agencyId: 'agency-101',
    name: 'Rapid Haul Logistics',
    yardAddress: '55 Port Access Rd, Colombo 15',
    suggestedVehicleClass: 'MediumLorry',
    positioningDistanceKm: 12.0,
    positioningEtaMinutes: 25,
    cargoDistanceKm: 110.0,
    estimatedPrice: 68000,
    selectionJustification: 'Shortest positioning time and immediate fleet availability.',
  },
  alternateCandidates: [
    {
      agencyId: 'agency-102',
      name: 'Wayamba Transporters',
      yardAddress: '10 Negombo Rd, Kurunegala',
      rank: 2,
      positioningDistanceKm: 28.0,
      positioningEtaMinutes: 45,
      eligible: true,
    },
  ],
  validation: {
    recommendation: 'Approve',
    explanation: 'Payload conforms to axle weight and safety limits.',
    checks: [
      { name: 'Cargo Weight Limits', passed: true, details: '5,000 kg <= 8,000 kg GVWR' },
    ],
  },
  steps: [
    { stepNo: 1, agentRole: 'Planner', status: 'Completed', durationMs: 110 },
    { stepNo: 2, agentRole: 'DomainAnalysis', status: 'Completed', durationMs: 180 },
    { stepNo: 3, agentRole: 'MatchingPricing', status: 'Completed', durationMs: 340 },
    { stepNo: 4, agentRole: 'ValidationSafety', status: 'Completed', durationMs: 90 },
  ],
  existingAssignment: null,
}

beforeEach(() => {
  // Sane default so tests that don't care about the call history section (most of them)
  // don't crash AgentCallHistory's internal useLoadMatchHistoryQuery() call.
  agentWorkflowsApi.useLoadMatchHistoryQuery.mockReturnValue({
    isLoading: false,
    isError: false,
    data: undefined,
  })
})

afterEach(() => {
  vi.mocked(loadsApi.useLoadsQuery).mockReset()
  vi.mocked(loadsApi.useLoadDetailQuery).mockReset()
  vi.mocked(agentWorkflowsApi.useLoadMatchQuery).mockReset()
  vi.mocked(agentWorkflowsApi.useTriggerMatchMutation).mockReset()
  vi.mocked(agentWorkflowsApi.useConfirmMatchMutation).mockReset()
  vi.mocked(agentWorkflowsApi.useLoadMatchHistoryQuery).mockReset()
  cleanup()
})

describe('AgentWorkflowConsolePage', () => {
  it('renders page with workflow stepper, recommendation, and validation', () => {
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      data: { items: [mockLoad] },
    })
    loadsApi.useLoadDetailQuery.mockReturnValue({
      isLoading: false,
      data: mockLoad,
    })
    agentWorkflowsApi.useLoadMatchQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockMatchData,
      refetch: vi.fn(),
    })
    agentWorkflowsApi.useTriggerMatchMutation.mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn(),
      isPending: false,
    })
    agentWorkflowsApi.useConfirmMatchMutation.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    })

    renderWithProviders(<AgentWorkflowConsolePage />, {
      route: '/agent-workflows',
      initialEntries: ['/agent-workflows?loadId=load-1'],
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    expect(screen.getByText('AI Agent Matching & Approval Console')).toBeInTheDocument()
    expect(screen.getByText('Rapid Haul Logistics')).toBeInTheDocument()
    expect(screen.getByText('Wayamba Transporters')).toBeInTheDocument()
    expect(screen.getByText('Agent 1: Planner')).toBeInTheDocument()
    expect(screen.getByText('Agent 3: Matching & Pricing')).toBeInTheDocument()
    expect(screen.getByText(/Shortest positioning time/)).toBeInTheDocument()
  })

  it('triggers confirm match mutation when Approve button is clicked', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockResolvedValue({ assignmentId: 'assign-99' })

    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      data: { items: [mockLoad] },
    })
    loadsApi.useLoadDetailQuery.mockReturnValue({
      isLoading: false,
      data: mockLoad,
    })
    agentWorkflowsApi.useLoadMatchQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockMatchData,
      refetch: vi.fn(),
    })
    agentWorkflowsApi.useTriggerMatchMutation.mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn(),
      isPending: false,
    })
    agentWorkflowsApi.useConfirmMatchMutation.mockReturnValue({
      mutateAsync,
      isPending: false,
    })

    renderWithProviders(<AgentWorkflowConsolePage />, {
      route: '/agent-workflows',
      initialEntries: ['/agent-workflows?loadId=load-1'],
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    const approveButton = screen.getByRole('button', { name: /Approve Match & Dispatch/i })
    await user.click(approveButton)

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith({
        loadId: 'load-1',
        agencyId: 'agency-101',
      })
    })

    expect(await screen.findByText('Proposal Approved')).toBeInTheDocument()
  })

  it('automatically triggers matching once when a load has no prior run, via an explicit command', async () => {
    // Regression test: matching used to start as a side effect of the GET the page already makes
    // on mount. Now it's a separate, explicit POST /match/trigger call the page fires once it sees
    // workflowStatus "NotStarted" (plans/04-backend-integration.md §1).
    const mutate = vi.fn()

    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      data: { items: [mockLoad] },
    })
    loadsApi.useLoadDetailQuery.mockReturnValue({
      isLoading: false,
      data: mockLoad,
    })
    agentWorkflowsApi.useLoadMatchQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { ...mockMatchData, workflowStatus: 'NotStarted', steps: [] },
      refetch: vi.fn(),
    })
    agentWorkflowsApi.useTriggerMatchMutation.mockReturnValue({
      mutate,
      mutateAsync: vi.fn(),
      isPending: false,
    })
    agentWorkflowsApi.useConfirmMatchMutation.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    })

    renderWithProviders(<AgentWorkflowConsolePage />, {
      route: '/agent-workflows',
      initialEntries: ['/agent-workflows?loadId=load-1'],
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    await waitFor(() => {
      expect(mutate).toHaveBeenCalledWith('load-1')
    })
    expect(mutate).toHaveBeenCalledTimes(1)
  })

  it('shows only the agent call history, not the live decision UI, once the load is already Matched', () => {
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      data: { items: [mockLoad] },
    })
    loadsApi.useLoadDetailQuery.mockReturnValue({
      isLoading: false,
      data: mockLoad,
    })
    agentWorkflowsApi.useLoadMatchQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        ...mockMatchData,
        loadStatus: 'Matched',
        workflowStatus: 'Completed',
        existingAssignment: {
          assignmentId: 'assign-1',
          agencyId: 'agency-101',
          agencyName: 'Rapid Haul Logistics',
          status: 'Proposed',
          proposedPrice: 68000,
          createdAt: '2026-09-28T10:00:00Z',
        },
      },
      refetch: vi.fn(),
    })
    agentWorkflowsApi.useTriggerMatchMutation.mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn(),
      isPending: false,
    })
    agentWorkflowsApi.useConfirmMatchMutation.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    })

    renderWithProviders(<AgentWorkflowConsolePage />, {
      route: '/agent-workflows',
      initialEntries: ['/agent-workflows?loadId=load-1'],
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    expect(screen.getByTestId('finalized-load-history-view')).toBeInTheDocument()
    expect(screen.getByText('Agent Call History')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Approve Match & Dispatch/i })).not.toBeInTheDocument()
    expect(screen.queryByText('Agent 1: Planner')).not.toBeInTheDocument()
  })

  it('shows only the focused workflow run view when ?workflowRunId= is present, not the live UI or the history list', () => {
    loadsApi.useLoadsQuery.mockReturnValue({
      isLoading: false,
      data: { items: [mockLoad] },
    })
    loadsApi.useLoadDetailQuery.mockReturnValue({
      isLoading: false,
      data: mockLoad,
    })
    agentWorkflowsApi.useLoadMatchQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockMatchData,
      refetch: vi.fn(),
    })
    agentWorkflowsApi.useTriggerMatchMutation.mockReturnValue({
      mutate: vi.fn(),
      mutateAsync: vi.fn(),
      isPending: false,
    })
    agentWorkflowsApi.useConfirmMatchMutation.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    })

    renderWithProviders(<AgentWorkflowConsolePage />, {
      route: '/agent-workflows',
      initialEntries: ['/agent-workflows?loadId=load-1&workflowRunId=run-1'],
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    expect(screen.getByTestId('focused-workflow-run-view')).toBeInTheDocument()
    expect(screen.getByTestId('agent-call-history-focused')).toBeInTheDocument()
    expect(screen.queryByTestId('finalized-load-history-view')).not.toBeInTheDocument()
    expect(screen.queryByText('Agent 1: Planner')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Approve Match & Dispatch/i })).not.toBeInTheDocument()
  })
})
