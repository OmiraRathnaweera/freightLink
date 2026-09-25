import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import MatchRecommendationCard from '../../components/MatchRecommendationCard.jsx'

const mockRecommendedAgency = {
  agencyId: 'agency-111',
  name: 'Lanka Swift Logistics',
  yardAddress: '124 Baseline Rd, Colombo 09',
  yardLat: 6.932,
  yardLng: 79.871,
  suggestedVehicleClass: 'MediumLorry',
  positioningDistanceKm: 14.5,
  positioningEtaMinutes: 28,
  cargoDistanceKm: 98.4,
  estimatedPrice: 72500,
  selectionJustification: 'Optimal positioning distance (14.5 km) with Medium Lorry available immediately.',
}

describe('MatchRecommendationCard', () => {
  it('renders recommendation details correctly', () => {
    render(
      <MatchRecommendationCard
        loadId="load-1"
        loadStatus="Posted"
        recommendedAgency={mockRecommendedAgency}
        selectedAgencyId="agency-111"
        onApproveMatch={vi.fn()}
        onRetryMatch={vi.fn()}
        isApproving={false}
        isRetrying={false}
      />
    )

    expect(screen.getByText('Lanka Swift Logistics')).toBeInTheDocument()
    expect(screen.getByText(/124 Baseline Rd/)).toBeInTheDocument()
    expect(screen.getAllByText(/Medium Lorry/i).length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(/72,500/)).toBeInTheDocument()
    expect(screen.getByText(/28 min/)).toBeInTheDocument()
    expect(screen.getByText(/98.4 km/)).toBeInTheDocument()
    expect(screen.getByText(/Optimal positioning distance/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Approve Match & Dispatch/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Retry Match/i })).toBeInTheDocument()
  })

  it('calls onApproveMatch when Approve Match button is clicked', async () => {
    const user = userEvent.setup()
    const handleApprove = vi.fn()

    render(
      <MatchRecommendationCard
        loadId="load-1"
        loadStatus="Posted"
        recommendedAgency={mockRecommendedAgency}
        selectedAgencyId="agency-111"
        onApproveMatch={handleApprove}
        onRetryMatch={vi.fn()}
        isApproving={false}
        isRetrying={false}
      />
    )

    const approveBtn = screen.getByRole('button', { name: /Approve Match & Dispatch/i })
    await user.click(approveBtn)

    expect(handleApprove).toHaveBeenCalledWith({
      loadId: 'load-1',
      agencyId: 'agency-111',
    })
  })

  it('calls onRetryMatch when Retry Match button is clicked', async () => {
    const user = userEvent.setup()
    const handleRetry = vi.fn()

    render(
      <MatchRecommendationCard
        loadId="load-1"
        loadStatus="Posted"
        recommendedAgency={mockRecommendedAgency}
        selectedAgencyId="agency-111"
        onApproveMatch={vi.fn()}
        onRetryMatch={handleRetry}
        isApproving={false}
        isRetrying={false}
      />
    )

    const retryBtn = screen.getByRole('button', { name: /Retry Match/i })
    await user.click(retryBtn)

    expect(handleRetry).toHaveBeenCalled()
  })

  it('renders confirmed match state when load is already matched', () => {
    const mockExistingAssignment = {
      assignmentId: 'assign-1',
      agencyId: 'agency-111',
      agencyName: 'Lanka Swift Logistics',
      status: 'Proposed',
      proposedPrice: 72500,
      createdAt: '2026-09-25T10:00:00Z',
    }

    render(
      <MatchRecommendationCard
        loadId="load-1"
        loadStatus="Matched"
        recommendedAgency={mockRecommendedAgency}
        selectedAgencyId="agency-111"
        existingAssignment={mockExistingAssignment}
        onApproveMatch={vi.fn()}
        onRetryMatch={vi.fn()}
        isApproving={false}
        isRetrying={false}
      />
    )

    expect(screen.getByText('Proposal Accepted by Shipper')).toBeInTheDocument()
    expect(screen.getByText(/Match Approved & Dispatched/i)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Approve Match & Dispatch/i })).not.toBeInTheDocument()
  })
})
