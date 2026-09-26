import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AdminDisputesPage from '../../pages/AdminDisputesPage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as disputesApi from '../../api/disputesApi.js'

// Mock the API layer hooks
vi.mock('../../api/disputesApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useDisputesQuery: vi.fn(),
    useStartReviewMutation: vi.fn(),
    useResolveDisputeMutation: vi.fn(),
    useResetDisputesMutation: vi.fn(),
  }
})

const MOCK_TEST_DISPUTES = [
  {
    disputeId: 'disp-001',
    displayId: 'DISP-1042',
    raisedDate: '2026-09-26T08:30:00+05:30',
    raisedByUser: {
      name: 'Sunil Weerakkody',
      role: 'Shipper',
      email: 's.weerakkody@lankateatraders.lk',
      company: 'Lanka Premium Tea Exporters',
    },
    trip: {
      tripId: 'TRP-8841',
      routeSummary: 'Colombo ➔ Kandy',
      carrierAgency: 'Central Express Logistics',
    },
    category: 'Damage',
    description: 'Consignment chests arrived with severe water intrusion.',
    status: 'Raised',
    resolution: null,
  },
  {
    disputeId: 'disp-002',
    displayId: 'DISP-1039',
    raisedDate: '2026-09-25T14:20:00+05:30',
    raisedByUser: {
      name: 'Malini Fernando',
      role: 'Shipper',
      email: 'm.fernando@ceylonspices.com',
      company: 'Ceylon Spice Millers PLC',
    },
    trip: {
      tripId: 'TRP-8799',
      routeSummary: 'Galle ➔ Colombo',
      carrierAgency: 'Southern Coast Haulage',
    },
    category: 'Delay',
    description: 'Carrier arrived 7 hours late due to vehicle maintenance.',
    status: 'UnderReview',
    resolution: null,
  },
  {
    disputeId: 'disp-003',
    displayId: 'DISP-1028',
    raisedDate: '2026-09-24T16:40:00+05:30',
    raisedByUser: {
      name: 'Dinesh Ratnayake',
      role: 'Shipper',
      email: 'dinesh.r@apexgarments.com',
      company: 'Apex Apparel Exports Ltd',
    },
    trip: {
      tripId: 'TRP-8692',
      routeSummary: 'Katunayake ➔ Colombo',
      carrierAgency: 'FastTrack Cargo Services',
    },
    category: 'Payment Issue',
    description: 'Invoice included unverified fuel surcharge adjustment.',
    status: 'Resolved',
    resolution: {
      outcome: 'Upheld',
      notes: 'Reviewed contractual agreement. Surcharge corrected back to 8% cap. Credit note issued for LKR 18,450.',
      resolvedAt: '2026-09-25T15:10:00+05:30',
      resolvedByUser: {
        name: 'Kasun Wickramasinghe (Admin)',
      },
    },
  },
]

function renderAdminDisputesPage(role = UserRole.ADMIN) {
  return renderWithProviders(<AdminDisputesPage />, {
    route: '/disputes',
    authState: { role, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('AdminDisputesPage — Rendering & Lifecycle Rules (Y3S01-81)', () => {
  it('renders the admin disputes table with all core columns and initial data', () => {
    disputesApi.useDisputesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: MOCK_TEST_DISPUTES,
    })
    disputesApi.useStartReviewMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResolveDisputeMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResetDisputesMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })

    renderAdminDisputesPage()

    // Page title and headers
    expect(screen.getByText('Admin Dispute Management')).toBeInTheDocument()
    expect(screen.getByText('Dispute ID & Date')).toBeInTheDocument()
    expect(screen.getByText('Raised By')).toBeInTheDocument()
    expect(screen.getByText('Linked Trip')).toBeInTheDocument()
    expect(screen.getByText('Reason / Description')).toBeInTheDocument()
    expect(screen.getByText('Status')).toBeInTheDocument()

    // Records
    expect(screen.getByText('#DISP-1042')).toBeInTheDocument()
    expect(screen.getByText('#DISP-1039')).toBeInTheDocument()
    expect(screen.getByText('#DISP-1028')).toBeInTheDocument()
    expect(screen.getByText('Colombo ➔ Kandy')).toBeInTheDocument()
    expect(screen.getByText('Sunil Weerakkody')).toBeInTheDocument()
  })

  it('renders state-specific action buttons strictly adhering to lifecycle rules', () => {
    disputesApi.useDisputesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: MOCK_TEST_DISPUTES,
    })
    disputesApi.useStartReviewMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResolveDisputeMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResetDisputesMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })

    renderAdminDisputesPage()

    // 1. Raised dispute (#DISP-1042) must have "Start Review" button
    expect(screen.getByRole('button', { name: /start review/i })).toBeInTheDocument()

    // 2. UnderReview dispute (#DISP-1039) must have "Resolve Dispute" button
    expect(screen.getByRole('button', { name: /resolve dispute/i })).toBeInTheDocument()

    // 3. Resolved dispute (#DISP-1028) must have "View Resolution" button
    expect(screen.getByRole('button', { name: /view resolution/i })).toBeInTheDocument()
  })

  it('triggers startReviewMutation when "Start Review" is clicked', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockResolvedValue({ displayId: 'DISP-1042' })
    disputesApi.useDisputesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: MOCK_TEST_DISPUTES,
    })
    disputesApi.useStartReviewMutation.mockReturnValue({ mutateAsync, isPending: false })
    disputesApi.useResolveDisputeMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResetDisputesMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })

    renderAdminDisputesPage()

    const startReviewBtn = screen.getByRole('button', { name: /start review/i })
    await user.click(startReviewBtn)

    expect(mutateAsync).toHaveBeenCalledWith('disp-001')
  })

  it('opens Resolution Modal when "Resolve Dispute" is clicked and requires non-empty resolution note', async () => {
    const user = userEvent.setup()
    const mutateAsync = vi.fn().mockResolvedValue({ displayId: 'DISP-1039' })
    disputesApi.useDisputesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: MOCK_TEST_DISPUTES,
    })
    disputesApi.useStartReviewMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResolveDisputeMutation.mockReturnValue({ mutateAsync, isPending: false })
    disputesApi.useResetDisputesMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })

    renderAdminDisputesPage()

    // Click Resolve Dispute on the UnderReview row
    await user.click(screen.getByRole('button', { name: /resolve dispute/i }))

    // Modal opens
    expect(screen.getByRole('heading', { name: 'Resolve Dispute' })).toBeInTheDocument()
    expect(screen.getByText('Adjudication for #DISP-1039')).toBeInTheDocument()

    // Confirm button is initially disabled because resolution note is empty
    const confirmBtn = screen.getByRole('button', { name: /confirm resolution/i })
    expect(confirmBtn).toBeDisabled()

    // Type a valid resolution note
    const noteInput = screen.getByLabelText(/resolution note/i)
    await user.type(noteInput, 'Investigation completed with carrier telematics. Delay compensated with 10% credit adjustment.')

    expect(confirmBtn).not.toBeDisabled()
    await user.click(confirmBtn)

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          disputeId: 'disp-002',
          outcome: 'Upheld',
          resolutionNote: expect.stringContaining('Investigation completed with carrier telematics'),
        }),
      )
    })
  })

  it('opens read-only View Resolution modal when "View Resolution" is clicked on a resolved dispute', async () => {
    const user = userEvent.setup()
    disputesApi.useDisputesQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: MOCK_TEST_DISPUTES,
    })
    disputesApi.useStartReviewMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResolveDisputeMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResetDisputesMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })

    renderAdminDisputesPage()

    await user.click(screen.getByRole('button', { name: /view resolution/i }))

    // Read-only modal opens
    expect(screen.getByRole('heading', { name: 'Dispute Resolution Record' })).toBeInTheDocument()
    expect(screen.getByText(/read-only audit log/i)).toBeInTheDocument()
    expect(screen.getByText(/reviewed contractual agreement/i)).toBeInTheDocument()
    expect(screen.getByText('Kasun Wickramasinghe (Admin)')).toBeInTheDocument()

    // Close modal
    await user.click(screen.getByRole('button', { name: 'Close Record' }))
    expect(screen.queryByText(/read-only audit log/i)).not.toBeInTheDocument()
  })
})
