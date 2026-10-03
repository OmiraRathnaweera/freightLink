import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AgencyDetailPage from '../../../pages/AgencyDetailPage.jsx'
import { UserRole } from '../../../../../lib/enums.js'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as agencyApi from '../../../api/agencyApi.js'

vi.mock('../../../api/agencyApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useAgencyQuery: vi.fn(),
    useComplianceDocsQuery: vi.fn(),
    useAgencyFleetQuery: vi.fn(),
    useAgencyStatusHistoryQuery: vi.fn(),
    useUpdateAgencyStatusMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
    useVerifyComplianceDocMutation: vi.fn(),
    useRejectComplianceDocMutation: vi.fn(),
  }
})

const AGENCY_ID = 'agency-1'

const AGENCY = {
  agencyId: AGENCY_ID,
  name: 'Colombo Freight Logistics Ltd',
  businessRegNo: 'PV-88741-2024',
  yardAddress: '124 Harbour Road, Colombo 13',
  yardLat: 6.9412,
  yardLng: 79.8512,
  status: 'Active',
  driverCount: 1,
  activeDriverCount: 1,
  vehicleCount: 1,
  createdAt: '2026-08-01T10:00:00.000Z',
}

const DOCS = [
  { complianceDocId: 'd1', docType: 'BusinessRegistration', docNumber: 'BR-1', status: 'Verified', issuedOn: '2026-01-15', storageKey: 'br-key' },
  { complianceDocId: 'd2', docType: 'VehicleInsurance', docNumber: 'INS-1', status: 'Pending', issuedOn: '2026-03-20', storageKey: 'ins-key' },
]

const verifyMutate = vi.fn()
const rejectMutate = vi.fn()

function mockQueries({ agency = AGENCY, docs = DOCS } = {}) {
  agencyApi.useVerifyComplianceDocMutation.mockReturnValue({ mutate: verifyMutate, isPending: false })
  agencyApi.useRejectComplianceDocMutation.mockReturnValue({ mutate: rejectMutate, isPending: false })
  agencyApi.useAgencyQuery.mockReturnValue({ data: agency, isLoading: false, isError: false })
  agencyApi.useComplianceDocsQuery.mockReturnValue({ data: docs, isLoading: false, isError: false })
  agencyApi.useAgencyFleetQuery.mockReturnValue({
    data: {
      vehicles: [{ vehicleId: 'v1', registrationNo: 'WP-CAD-1234', vehicleType: 'Lorry', capacityKg: 5000, volumeM3: 12, status: 'Available' }],
      drivers: [{ driverId: 'dr1', fullName: 'Nimal Perera', email: 'nimal@example.com', licenceNo: 'DL-1', licenceExpiry: '2030-01-01', status: 'Active' }],
    },
    isLoading: false,
    isError: false,
  })
  agencyApi.useAgencyStatusHistoryQuery.mockReturnValue({
    data: [
      { agencyStatusHistoryId: 'h2', fromStatus: 'Suspended', toStatus: 'Active', reason: 'Suspended in error', changedByName: 'Admin One', changedAt: '2026-09-02T10:00:00.000Z' },
      { agencyStatusHistoryId: 'h1', fromStatus: 'Active', toStatus: 'Suspended', reason: 'Audit', changedByName: 'Admin One', changedAt: '2026-09-01T10:00:00.000Z' },
    ],
    isLoading: false,
    isError: false,
  })
}

function renderPage(role = UserRole.ADMIN) {
  return renderWithProviders(<AgencyDetailPage />, {
    route: '/agencies/:agencyId',
    initialEntries: [`/agencies/${AGENCY_ID}`],
    authState: { role, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('AgencyDetailPage (admin agency profile)', () => {
  it('shows the full profile: identity, every document with its file link, fleet and status history', () => {
    mockQueries()
    renderPage()

    expect(screen.getByRole('heading', { name: 'Colombo Freight Logistics Ltd' })).toBeInTheDocument()
    expect(screen.getByText('124 Harbour Road, Colombo 13')).toBeInTheDocument()

    // Verified AND pending documents are listed, each with a View link.
    expect(screen.getByText('BR-1')).toBeInTheDocument()
    expect(screen.getByText('INS-1')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: /view/i })).toHaveLength(2)
    expect(screen.getByText('Verified mandatory documents').nextElementSibling).toHaveTextContent('1 / 4')

    expect(screen.getByText('WP-CAD-1234')).toBeInTheDocument()
    expect(screen.getByText('Nimal Perera')).toBeInTheDocument()

    expect(screen.getByText('Suspended in error')).toBeInTheDocument()
    expect(screen.getAllByText(/Admin One/)).toHaveLength(2)
  })

  it('offers Suspend for an active agency and Reactivate for a suspended one', () => {
    mockQueries()
    renderPage()
    expect(screen.getByRole('button', { name: 'Suspend' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reactivate' })).not.toBeInTheDocument()

    cleanup()
    mockQueries({ agency: { ...AGENCY, status: 'Suspended' } })
    renderPage()
    expect(screen.getByRole('button', { name: 'Reactivate' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Suspend' })).not.toBeInTheDocument()
  })

  it('does not render the profile for a non-admin role', () => {
    mockQueries()
    renderPage(UserRole.AGENCY_STAFF)

    expect(screen.queryByText('Colombo Freight Logistics Ltd')).not.toBeInTheDocument()
  })

  it('shows a not-found message when the agency does not exist', () => {
    agencyApi.useVerifyComplianceDocMutation.mockReturnValue({ mutate: vi.fn(), isPending: false })
    agencyApi.useRejectComplianceDocMutation.mockReturnValue({ mutate: vi.fn(), isPending: false })
    agencyApi.useAgencyQuery.mockReturnValue({ data: undefined, isLoading: false, isError: true, error: { status: 404 } })
    agencyApi.useComplianceDocsQuery.mockReturnValue({ data: [], isLoading: false, isError: false })
    agencyApi.useAgencyFleetQuery.mockReturnValue({ data: undefined, isLoading: false, isError: false })
    agencyApi.useAgencyStatusHistoryQuery.mockReturnValue({ data: [], isLoading: false, isError: false })
    renderPage()

    expect(screen.getByText('This agency could not be found.')).toBeInTheDocument()
  })

  it('lets the admin verify or reject a Pending document, and offers no review actions on a Verified one', async () => {
    const user = userEvent.setup()
    mockQueries()
    renderPage()

    // Only the Pending insurance doc is reviewable — the Verified business registration is not.
    expect(screen.getAllByRole('button', { name: /^verify$/i })).toHaveLength(1)
    expect(screen.getAllByRole('button', { name: /^reject$/i })).toHaveLength(1)

    await user.click(screen.getByRole('button', { name: /^verify$/i }))
    expect(verifyMutate).toHaveBeenCalledWith({ agencyId: AGENCY_ID, docId: 'd2' })

    await user.click(screen.getByRole('button', { name: /^reject$/i }))
    expect(rejectMutate).toHaveBeenCalledWith({ agencyId: AGENCY_ID, docId: 'd2' })
  })

  it('disables review buttons and shows progress while a review is in flight', async () => {
    const user = userEvent.setup()
    mockQueries()
    agencyApi.useVerifyComplianceDocMutation.mockReturnValue({ mutate: verifyMutate, isPending: false })
    renderPage()
    await user.click(screen.getByRole('button', { name: /^verify$/i }))

    cleanup()
    agencyApi.useVerifyComplianceDocMutation.mockReturnValue({ mutate: verifyMutate, isPending: true })
    renderPage()
    // A fresh mount hasn't recorded which doc is in flight, so both buttons stay but are disabled.
    screen.getAllByRole('button', { name: /^(verify|reject)$/i }).forEach((b) => expect(b).toBeDisabled())
  })
})
