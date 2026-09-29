import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

import { UserRole } from '../../../../../lib/enums.js'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as disputesApi from '../../../api/disputesApi.js'
import ClaimantDisputeDetailPage from '../../../pages/ClaimantDisputeDetailPage.jsx'
import MyDisputesPage from '../../../pages/MyDisputesPage.jsx'
import RaiseDisputePage from '../../../pages/RaiseDisputePage.jsx'

vi.mock('../../../api/disputesApi.js', () => ({
  useDisputesQuery: vi.fn(),
  useDisputeDetailQuery: vi.fn(),
  useRaiseDisputeMutation: vi.fn(),
}))

const dispute = {
  disputeId: 'dispute-1',
  displayId: 'DISPUTE1',
  tripId: 'trip-1',
  raisedByUserId: 'shipper-1',
  raisedDate: '2026-09-27T10:00:00Z',
  createdAt: '2026-09-27T10:00:00Z',
  updatedAt: '2026-09-27T12:00:00Z',
  raisedByUser: { name: 'Nadeesha Fernando', role: 'Shipper' },
  trip: { tripId: 'trip-1', routeSummary: 'Colombo → Kandy', carrierAgency: 'Central Express' },
  category: 'Delay',
  description: 'The carrier arrived many hours after the agreed pickup window.',
  status: 'Resolved',
  resolution: {
    outcome: 'PartiallyUpheld',
    notes: 'The delay was verified. A partial credit will be issued.',
    resolvedAt: '2026-09-27T12:00:00Z',
  },
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('claimant dispute views', () => {
  it('renders only the role-scoped dispute response and its claimant/status context', () => {
    disputesApi.useDisputesQuery.mockReturnValue({
      data: [dispute],
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    })

    renderWithProviders(<MyDisputesPage />, {
      route: '/my-disputes',
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    expect(screen.getByText('Trip Disputes')).toBeInTheDocument()
    expect(screen.getByText('Colombo → Kandy')).toBeInTheDocument()
    expect(screen.getByText('Filed by Nadeesha Fernando · Raised Sep 27, 2026 · Updated Sep 27, 2026')).toBeInTheDocument()
    expect(screen.getAllByText('Resolved')).not.toHaveLength(0)
  })

  it('validates a raise form and sends the fixed backend category and trip reference', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockResolvedValue({ disputeId: 'dispute-new' })
    disputesApi.useRaiseDisputeMutation.mockReturnValue({ mutateAsync, isPending: false })

    renderWithProviders(<RaiseDisputePage />, {
      route: '/my-disputes/new',
      initialEntries: ['/my-disputes/new?tripId=trip-1'],
      authState: { role: UserRole.AGENCY_STAFF, isAuthenticated: true },
    })

    await user.click(screen.getByRole('button', { name: /submit dispute/i }))
    expect(screen.getByRole('alert')).toHaveTextContent('Description must be between 10 and 2,000 characters.')

    await user.type(screen.getByLabelText('What happened?'), 'The freight arrived after the documented pickup window had closed.')
    await user.click(screen.getByRole('button', { name: /submit dispute/i }))

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith({
        tripId: 'trip-1',
        category: 'Damage',
        description: 'The freight arrived after the documented pickup window had closed.',
      })
    })
  })

  it('shows the active-duplicate rejection as a claimant-friendly form error', async () => {
    const user = userEvent.setup()
    disputesApi.useRaiseDisputeMutation.mockReturnValue({
      mutateAsync: vi.fn().mockRejectedValue({
        response: {
          data: {
            error: {
              code: 'DISPUTE_ALREADY_EXISTS_FOR_TRIP_AND_CATEGORY',
              message: 'Backend conflict text',
            },
          },
        },
      }),
      isPending: false,
    })

    renderWithProviders(<RaiseDisputePage />, {
      route: '/my-disputes/new',
      initialEntries: ['/my-disputes/new?tripId=trip-1'],
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    await user.type(screen.getByLabelText('What happened?'), 'The carrier arrived after the agreed pickup window had already closed.')
    await user.click(screen.getByRole('button', { name: /submit dispute/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'You already have an active dispute in this category for this trip.',
    )
  })

  it('renders a final resolution without any Admin review or resolve controls', () => {
    disputesApi.useDisputeDetailQuery.mockReturnValue({
      data: dispute,
      isLoading: false,
      isError: false,
      isFetching: false,
      refetch: vi.fn(),
    })

    renderWithProviders(<ClaimantDisputeDetailPage />, {
      route: '/my-disputes/:disputeId',
      initialEntries: ['/my-disputes/dispute-1'],
      authState: { role: UserRole.SHIPPER, isAuthenticated: true },
    })

    expect(screen.getByText('Admin Resolution')).toBeInTheDocument()
    expect(screen.getByText('PartiallyUpheld')).toBeInTheDocument()
    expect(screen.getByText('The delay was verified. A partial credit will be issued.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /start review|resolve dispute/i })).not.toBeInTheDocument()
  })
})
