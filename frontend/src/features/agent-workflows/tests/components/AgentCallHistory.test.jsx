import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AgentCallHistory from '../../components/AgentCallHistory.jsx'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as agentWorkflowsApi from '../../api/agentWorkflowsApi.js'

vi.mock('../../api/agentWorkflowsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useLoadMatchHistoryQuery: vi.fn(),
  }
})

const mockHistory = {
  loadId: 'load-1',
  referenceCode: 'LD-0001',
  attempts: [
    {
      workflowRunId: 'run-2',
      attemptNo: 2,
      status: 'AwaitingApproval',
      startedAt: '2026-09-28T10:00:00Z',
      completedAt: null,
      objective: 'Find and assign a suitable carrier',
      shipperMessage: 'This load needs a MediumLorry. Finding the best agency now.',
      selectedAgencyId: 'agency-102',
      selectedAgencyName: 'Wayamba Transporters',
      proposedPrice: 71000,
      decision: null,
      decisionReason: null,
      steps: [
        {
          stepNo: 3,
          agentRole: 'MatchingPricing',
          status: 'Succeeded',
          errorMessage: null,
          durationMs: 4200,
          toolCalls: [
            {
              toolName: 'get_route_and_eta',
              attemptNo: 1,
              success: true,
              durationMs: 340,
              httpStatusCode: 200,
              errorMessage: null,
              calledAt: '2026-09-28T10:00:05Z',
            },
          ],
        },
      ],
    },
    {
      workflowRunId: 'run-1',
      attemptNo: 1,
      status: 'Aborted',
      startedAt: '2026-09-28T09:00:00Z',
      completedAt: '2026-09-28T09:05:00Z',
      objective: 'Find and assign a suitable carrier',
      shipperMessage: null,
      selectedAgencyId: 'agency-101',
      selectedAgencyName: 'Rapid Haul Logistics',
      proposedPrice: 68000,
      decision: 'Reject',
      decisionReason: 'Price above budget',
      steps: [
        { stepNo: 1, agentRole: 'Planner', status: 'Succeeded', errorMessage: null, durationMs: 90, toolCalls: [] },
      ],
    },
  ],
}

afterEach(() => {
  vi.mocked(agentWorkflowsApi.useLoadMatchHistoryQuery).mockReset()
  cleanup()
})

describe('AgentCallHistory', () => {
  it('does not fetch history until the section is opened', () => {
    agentWorkflowsApi.useLoadMatchHistoryQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
    })

    renderWithProviders(<AgentCallHistory loadId="load-1" />)

    expect(screen.getByText('Agent Call History')).toBeInTheDocument()
    expect(screen.queryByText('Wayamba Transporters')).not.toBeInTheDocument()
    expect(agentWorkflowsApi.useLoadMatchHistoryQuery).toHaveBeenCalledWith('load-1', { enabled: false })
  })

  it('shows every past attempt with its steps and the rejected decision once opened', async () => {
    const user = userEvent.setup()
    agentWorkflowsApi.useLoadMatchHistoryQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockHistory,
    })

    renderWithProviders(<AgentCallHistory loadId="load-1" />)

    await user.click(screen.getByTestId('toggle-agent-call-history-btn'))

    expect(screen.getByText('Wayamba Transporters')).toBeInTheDocument()
    expect(screen.getByText('Rapid Haul Logistics')).toBeInTheDocument()

    // Only the latest attempt (#2) is expanded by default - expand attempt #1 to see its
    // recorded shipper decision.
    await user.click(screen.getByText('Rapid Haul Logistics'))
    expect(screen.getByText(/Shipper decision: Reject/)).toBeInTheDocument()
    expect(screen.getByText('Price above budget')).toBeInTheDocument()
  })

  it('expands a step to reveal its individual tool calls', async () => {
    const user = userEvent.setup()
    agentWorkflowsApi.useLoadMatchHistoryQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: mockHistory,
    })

    renderWithProviders(<AgentCallHistory loadId="load-1" />)

    await user.click(screen.getByTestId('toggle-agent-call-history-btn'))
    await user.click(screen.getByRole('button', { name: /1 tool call/i }))

    expect(screen.getByText('get_route_and_eta')).toBeInTheDocument()
  })
})
