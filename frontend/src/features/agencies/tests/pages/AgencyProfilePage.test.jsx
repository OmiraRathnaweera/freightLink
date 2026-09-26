import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import AgencyProfilePage from '../../pages/AgencyProfilePage.jsx'
import { UserRole } from '../../../../lib/enums.js'
import { renderWithProviders } from '../../../../test/testUtils.jsx'
import * as agencyApi from '../../api/agencyApi.js'
import * as authApi from '../../../auth/api/authApi.js'

vi.mock('../../api/agencyApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useAgencyQuery: vi.fn(),
    useComplianceDocsQuery: vi.fn(),
    useAddComplianceDocMutation: vi.fn(() => ({
      mutateAsync: vi.fn(),
      isPending: false,
    })),
  }
})

vi.mock('../../../auth/api/authApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useCurrentUserQuery: vi.fn(),
  }
})

vi.mock('../../../loads/api/loadsApi.js', () => ({
  useUploadFileMutation: vi.fn(() => ({
    mutateAsync: vi.fn().mockResolvedValue({ publicId: 'mock-public-id' }),
    isPending: false,
  })),
}))

const MOCK_USER = {
  id: 'user-1',
  email: 'staff@colombofreight.com',
  name: 'Pasindu Staff',
  role: 'AgencyStaff',
  agencyId: 'agency-999',
}

const MOCK_AGENCY = {
  agencyId: 'agency-999',
  name: 'Colombo Freight Logistics Ltd',
  businessRegNo: 'PV-88741-2024',
  yardAddress: '124 Harbour Road, Colombo 13',
  yardLat: 6.9412,
  yardLng: 79.8512,
  status: 'Verified',
}

const MOCK_DOCS = [
  {
    complianceDocId: 'doc-1',
    docType: 'BusinessRegistration',
    docNumber: 'BR-2024-9988',
    status: 'Verified',
    issuedOn: '2026-01-15',
    storageKey: 'https://example.com/br.pdf',
  },
  {
    complianceDocId: 'doc-2',
    docType: 'VehicleInsurance',
    docNumber: 'INS-88123-A',
    status: 'Pending',
    issuedOn: '2026-03-20',
  },
]

function renderPage() {
  return renderWithProviders(<AgencyProfilePage />, {
    route: '/agencies',
    authState: { role: UserRole.AGENCY_STAFF, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('AgencyProfilePage', () => {
  it('renders agency identity banner and compliance metrics', () => {
    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useAgencyQuery.mockReturnValue({
      data: MOCK_AGENCY,
      isLoading: false,
    })
    agencyApi.useComplianceDocsQuery.mockReturnValue({
      data: MOCK_DOCS,
      isLoading: false,
    })

    renderPage()

    // Agency name & reg no
    expect(screen.getByText('Colombo Freight Logistics Ltd')).toBeInTheDocument()
    expect(screen.getByText('PV-88741-2024')).toBeInTheDocument()
    expect(screen.getByText('124 Harbour Road, Colombo 13')).toBeInTheDocument()

    // Compliance metrics
    expect(screen.getByText('Submitted Documents')).toBeInTheDocument()
    expect(screen.getByText('Verified Documents')).toBeInTheDocument()
  })

  it('renders all compliance document category cards instead of a dropdown', () => {
    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useAgencyQuery.mockReturnValue({
      data: MOCK_AGENCY,
      isLoading: false,
    })
    agencyApi.useComplianceDocsQuery.mockReturnValue({
      data: MOCK_DOCS,
      isLoading: false,
    })

    renderPage()

    // Document category cards
    expect(screen.getByRole('heading', { level: 4, name: 'Business Registration' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 4, name: 'Fleet Insurance Policy' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 4, name: 'Revenue Licence' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 4, name: 'Goods Transport Permit' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 4, name: 'Additional Documentation' })).toBeInTheDocument()

    // Uploaded doc numbers
    expect(screen.getByText('BR-2024-9988')).toBeInTheDocument()
    expect(screen.getByText('INS-88123-A')).toBeInTheDocument()

    // Not uploaded cards should have "Not Uploaded" badges
    const notUploadedBadges = screen.getAllByText('Not Uploaded')
    expect(notUploadedBadges.length).toBeGreaterThanOrEqual(2)
  })

  it('opens upload modal when "Upload Document" button on an unuploaded card is clicked', async () => {
    const user = userEvent.setup()
    authApi.useCurrentUserQuery.mockReturnValue({
      data: MOCK_USER,
      isLoading: false,
    })
    agencyApi.useAgencyQuery.mockReturnValue({
      data: MOCK_AGENCY,
      isLoading: false,
    })
    agencyApi.useComplianceDocsQuery.mockReturnValue({
      data: MOCK_DOCS,
      isLoading: false,
    })

    renderPage()

    // Find upload buttons
    const uploadButtons = screen.getAllByRole('button', { name: /upload document/i })
    expect(uploadButtons.length).toBeGreaterThan(0)

    // Click upload on the first unuploaded doc
    await user.click(uploadButtons[0])

    // Modal dialog opens
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByLabelText(/close upload dialog/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/e\.g\. BR-2024-9901/i)).toBeInTheDocument()
    expect(screen.getByText(/drag and drop your file here/i)).toBeInTheDocument()
  })
})
