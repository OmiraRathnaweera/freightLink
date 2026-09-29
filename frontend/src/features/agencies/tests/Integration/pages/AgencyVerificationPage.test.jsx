import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AgencyVerificationPage from '../../../pages/AgencyVerificationPage.jsx'
import { ComplianceDocStatus, UserRole } from '../../../../../lib/enums.js'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as agencyApi from '../../../api/agencyApi.js'

vi.mock('../../../api/agencyApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useVerificationQueueQuery: vi.fn(),
    useAgenciesQuery: vi.fn(),
    useVerifyAgencyMutation: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
    useActivateAgencyMutation: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
    useSuspendAgencyMutation: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
    useVerifyComplianceDocMutation: vi.fn(),
    useRejectComplianceDocMutation: vi.fn(),
  }
})

const MOCK_QUEUE_ITEMS = [
  {
    agency: {
      agencyId: 'agency-1',
      name: 'Lanka Trans Express',
      businessRegNo: 'PV-100200',
      status: 'Pending',
    },
    complianceDocs: [
      {
        complianceDocId: 'doc-101',
        docType: 'BusinessRegistration',
        docNumber: 'BR-100200',
        status: ComplianceDocStatus.PENDING,
        storageKey: 'freightlink/br100200.pdf',
        expiresOn: '2028-12-31',
      },
      {
        complianceDocId: 'doc-102',
        docType: 'GoodsTransportPermit',
        docNumber: 'GTP-99201',
        status: ComplianceDocStatus.VERIFIED,
        storageKey: 'freightlink/gtp99201.jpg',
        expiresOn: '2027-06-30',
      },
    ],
  },
]

function renderPage(role = UserRole.ADMIN) {
  return renderWithProviders(<AgencyVerificationPage />, {
    route: '/admin/agencies/verification',
    authState: { role, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('AgencyVerificationPage', () => {
  it('redirects unauthorized non-admin user', () => {
    agencyApi.useVerificationQueueQuery.mockReturnValue({
      data: MOCK_QUEUE_ITEMS,
      isLoading: false,
      isError: false,
    })
    agencyApi.useAgenciesQuery.mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
    })

    renderPage(UserRole.AGENCY_STAFF)

    expect(screen.queryByText('Agency Verification Queue')).not.toBeInTheDocument()
  })

  it('renders pending agencies and compliance documents table', () => {
    agencyApi.useVerificationQueueQuery.mockReturnValue({
      data: MOCK_QUEUE_ITEMS,
      isLoading: false,
      isError: false,
    })
    agencyApi.useAgenciesQuery.mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
    })
    agencyApi.useVerifyComplianceDocMutation.mockReturnValue({ mutate: vi.fn(), isPending: false })
    agencyApi.useRejectComplianceDocMutation.mockReturnValue({ mutate: vi.fn(), isPending: false })

    renderPage(UserRole.ADMIN)

    expect(screen.getByText('Agency Verification Queue')).toBeInTheDocument()
    expect(screen.getByText('Lanka Trans Express')).toBeInTheDocument()
    expect(screen.getByText('Reg No: PV-100200')).toBeInTheDocument()
    expect(screen.getByText('BR-100200')).toBeInTheDocument()
    expect(screen.getByText('GTP-99201')).toBeInTheDocument()

    // Action buttons only rendered for Pending doc-101, not doc-102 (Verified)
    expect(screen.getAllByRole('button', { name: /verify/i })).toHaveLength(2) // 1 doc "Verify" + 1 agency "Verify Agency"
    expect(screen.getAllByRole('button', { name: /reject/i })).toHaveLength(1)
  })

  it('hides all action buttons immediately when doc Verify button is clicked', async () => {
    const user = userEvent.setup()
    const verifyDocMock = vi.fn()

    agencyApi.useVerificationQueueQuery.mockReturnValue({
      data: MOCK_QUEUE_ITEMS,
      isLoading: false,
      isError: false,
    })
    agencyApi.useAgenciesQuery.mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
    })
    agencyApi.useVerifyComplianceDocMutation.mockReturnValue({
      mutate: verifyDocMock,
      isPending: false,
    })
    agencyApi.useRejectComplianceDocMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    })

    renderPage(UserRole.ADMIN)

    const verifyDocBtn = screen.getByRole('button', { name: /^verify$/i })
    const rejectDocBtn = screen.getByRole('button', { name: /^reject$/i })

    expect(verifyDocBtn).toBeInTheDocument()
    expect(rejectDocBtn).toBeInTheDocument()

    await user.click(verifyDocBtn)

    expect(verifyDocMock).toHaveBeenCalledWith(
      expect.objectContaining({ agencyId: 'agency-1', docId: 'doc-101' }),
    )

    // Both action buttons should now be hidden immediately
    expect(screen.queryByRole('button', { name: /^verify$/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
  })

  it('hides all action buttons immediately when doc Reject button is clicked', async () => {
    const user = userEvent.setup()
    const rejectDocMock = vi.fn()

    agencyApi.useVerificationQueueQuery.mockReturnValue({
      data: MOCK_QUEUE_ITEMS,
      isLoading: false,
      isError: false,
    })
    agencyApi.useAgenciesQuery.mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
    })
    agencyApi.useVerifyComplianceDocMutation.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    })
    agencyApi.useRejectComplianceDocMutation.mockReturnValue({
      mutate: rejectDocMock,
      isPending: false,
    })

    renderPage(UserRole.ADMIN)

    const rejectDocBtn = screen.getByRole('button', { name: /^reject$/i })
    expect(rejectDocBtn).toBeInTheDocument()

    await user.click(rejectDocBtn)

    expect(rejectDocMock).toHaveBeenCalledWith(
      expect.objectContaining({ agencyId: 'agency-1', docId: 'doc-101' }),
    )

    // Both action buttons should now be hidden immediately
    expect(screen.queryByRole('button', { name: /^verify$/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^reject$/i })).not.toBeInTheDocument()
  })

  it('opens document with target="popup" and window.open on click', async () => {
    const user = userEvent.setup()
    agencyApi.useVerificationQueueQuery.mockReturnValue({
      data: MOCK_QUEUE_ITEMS,
      isLoading: false,
      isError: false,
    })
    agencyApi.useAgenciesQuery.mockReturnValue({
      data: { items: [] },
      isLoading: false,
      isError: false,
    })
    agencyApi.useVerifyComplianceDocMutation.mockReturnValue({ mutate: vi.fn(), isPending: false })
    agencyApi.useRejectComplianceDocMutation.mockReturnValue({ mutate: vi.fn(), isPending: false })

    const windowOpenSpy = vi.spyOn(window, 'open').mockImplementation(() => ({}))

    renderPage(UserRole.ADMIN)

    const viewLinks = screen.getAllByRole('link', { name: /view/i })
    expect(viewLinks.length).toBeGreaterThanOrEqual(1)

    const firstViewLink = viewLinks[0]
    expect(firstViewLink).toHaveAttribute('target', 'popup')

    await user.click(firstViewLink)

    expect(windowOpenSpy).toHaveBeenCalledWith(
      expect.stringContaining('freightlink/br100200.pdf'),
      'popup',
      expect.stringContaining('width=900'),
    )
  })
})
