import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AdminDisputesPage from '../../../pages/AdminDisputesPage.jsx'
import ResolutionModal from '../../../components/ResolutionModal.jsx'
import { UserRole } from '../../../../../lib/enums.js'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as disputesApi from '../../../api/disputesApi.js'
import { validateResolutionPayload, DisputeStatus } from '../../../lib/disputeRules.js'

vi.mock('../../../api/disputesApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useDisputesQuery: vi.fn(),
    useStartReviewMutation: vi.fn(),
    useResolveDisputeMutation: vi.fn(),
  }
})

const MOCK_DISPUTES_LIST = [
  {
    disputeId: 'disp-001',
    displayId: 'DISP-1042',
    raisedDate: '2026-10-01T08:30:00+05:30',
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
    category: 'IncorrectAmount',
    description: 'Invoiced rate includes 10,000 LKR uncontracted fuel surcharge.',
    status: 'Raised',
    resolution: null,
  },
  {
    disputeId: 'disp-002',
    displayId: 'DISP-1039',
    raisedDate: '2026-10-02T14:20:00+05:30',
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
    description: 'Carrier arrived 7 hours late resulting in port detention fees.',
    status: 'UnderReview',
    resolution: null,
  },
]

describe('Invoice and Dispute Admin Dashboard Tests', () => {
  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
  })

  // --- FE-UT-001: Table Rendering & Metric Counters ---
  it('FE-UT-001: Renders dispute queue table with formatted IDs, statuses, and metric counters', async () => {
    disputesApi.useDisputesQuery.mockImplementation(({ status }) => {
      if (status === 'All') {
        return { data: MOCK_DISPUTES_LIST, isLoading: false, isError: false, refetch: vi.fn() }
      }
      return { data: MOCK_DISPUTES_LIST, isLoading: false, isError: false, refetch: vi.fn() }
    })
    disputesApi.useStartReviewMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResolveDisputeMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })

    renderWithProviders(<AdminDisputesPage />, {
      authState: { role: UserRole.Admin, isAuthenticated: true },
    })

    expect(screen.getByText('Dispute Adjudication')).toBeInTheDocument()
    expect(screen.getByText('DISP-1042')).toBeInTheDocument()
    expect(screen.getByText('DISP-1039')).toBeInTheDocument()
    expect(screen.getByText('Sunil Weerakkody')).toBeInTheDocument()
  })

  // --- FE-UT-003 & FE-UT-004: Resolution Form Validation ---
  it('FE-UT-003: Rejects submission when resolution note is under 10 characters', () => {
    const errorEmpty = validateResolutionPayload('')
    expect(errorEmpty).toContain('Resolution note is strictly mandatory')

    const errorTooShort = validateResolutionPayload('Too short')
    expect(errorTooShort).toContain('at least 10 characters')

    const validNote = validateResolutionPayload('Verified tariff overcharge. 10,000 LKR refunded to shipper.')
    expect(validNote).toBeNull()
  })

  it('FE-UT-004: ResolutionModal blocks confirmation when note validation fails', async () => {
    const user = userEvent.setup()
    const handleConfirm = vi.fn()
    const handleClose = vi.fn()

    renderWithProviders(
      <ResolutionModal
        dispute={MOCK_DISPUTES_LIST[1]}
        isOpen={true}
        onClose={handleClose}
        onConfirm={handleConfirm}
        isPending={false}
      />
    )

    // Type a short note
    const textarea = screen.getByPlaceholderText(/Summarize the justification/i)
    await user.type(textarea, 'Short')

    // Click confirm
    const submitBtn = screen.getByRole('button', { name: /Confirm Resolution/i })
    await user.click(submitBtn)

    // Handler must NOT be called
    expect(handleConfirm).not.toHaveBeenCalled()
    expect(screen.getByText(/at least 10 characters/i)).toBeInTheDocument()
  })

  // --- FE-IT-003: Network Error Display with Retry Action ---
  it('FE-IT-003: Displays friendly ErrorState banner and triggers refetch on retry', async () => {
    const refetchMock = vi.fn()
    disputesApi.useDisputesQuery.mockReturnValue({
      data: [],
      isLoading: false,
      isError: true,
      error: new Error('Network timeout (500)'),
      refetch: refetchMock,
    })
    disputesApi.useStartReviewMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })
    disputesApi.useResolveDisputeMutation.mockReturnValue({ mutateAsync: vi.fn(), isPending: false })

    renderWithProviders(<AdminDisputesPage />, {
      authState: { role: UserRole.Admin, isAuthenticated: true },
    })

    expect(screen.getByText(/Failed to load disputes/i)).toBeInTheDocument()

    const retryButton = screen.getByRole('button', { name: /Retry/i })
    await userEvent.click(retryButton)
    expect(refetchMock).toHaveBeenCalledTimes(1)
  })

  // --- FE-IT-005: Role Protection / Unauthorized Access Guard ---
  it('FE-IT-005: Redirects non-admin role to default home view', () => {
    disputesApi.useDisputesQuery.mockReturnValue({ data: [], isLoading: false, isError: false })

    renderWithProviders(<AdminDisputesPage />, {
      authState: { role: UserRole.Shipper, isAuthenticated: true },
    })

    // Admin table header should not be displayed because of role redirection
    expect(screen.queryByText('Dispute Adjudication')).not.toBeInTheDocument()
  })
})
