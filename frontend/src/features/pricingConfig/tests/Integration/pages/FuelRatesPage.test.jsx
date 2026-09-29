import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import FuelRatesPage from '../../../pages/FuelRatesPage.jsx'
import { UserRole } from '../../../../../lib/enums.js'
import { renderWithProviders } from '../../../../../test/testUtils.jsx'
import * as pricingConfigApi from '../../../api/pricingConfigApi.js'

// Mirrors the mocking style of features/agencies/tests/pages/FleetVehiclesPage.test.jsx:
// mock every hook FuelRateSection/FuelRateForm call, drive each test's scenario
// via mockReturnValue/mockResolvedValueOnce/mockRejectedValueOnce.
const mockMutateAsync = vi.fn()
const mockRefetch = vi.fn()

vi.mock('../../../api/pricingConfigApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    useFuelRatesQuery: vi.fn(),
    useFuelRateHistoryQuery: vi.fn(() => ({ data: undefined, isLoading: false, isError: false })),
    useDeleteFuelRateMutation: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
    useCreateFuelRateMutation: vi.fn(() => ({ mutateAsync: mockMutateAsync, isPending: false })),
  }
})

const MOCK_RATES = [
  {
    fuelPriceRateId: 'rate-1',
    fuelType: 'AutoDiesel',
    pricePerLitre: 350.5,
    source: 'CPC official price list',
    effectiveFrom: '2026-09-01T00:00:00Z',
    setByUserName: 'Admin User',
    createdAt: '2026-09-01T00:00:00Z',
    deletedAt: null,
  },
]

function renderPage() {
  return renderWithProviders(<FuelRatesPage />, {
    route: '/pricing-config/fuel-rates',
    authState: { role: UserRole.ADMIN, isAuthenticated: true },
  })
}

afterEach(() => {
  vi.clearAllMocks()
  cleanup()
})

describe('FuelRatesPage — component / list rendering', () => {
  it('renders the current fuel rate list', () => {
    pricingConfigApi.useFuelRatesQuery.mockReturnValue({ data: MOCK_RATES, isLoading: false, isError: false })

    renderPage()

    expect(screen.getAllByText('Fuel Prices').length).toBeGreaterThanOrEqual(1)
    const table = screen.getByRole('table')
    expect(within(table).getByText('AutoDiesel')).toBeInTheDocument()
    expect(within(table).getByText('CPC official price list')).toBeInTheDocument()
  })

  it('renders an empty state when there are no fuel rates', () => {
    pricingConfigApi.useFuelRatesQuery.mockReturnValue({ data: [], isLoading: false, isError: false })

    renderPage()

    expect(screen.getByText('No fuel rates yet')).toBeInTheDocument()
  })
})

describe('FuelRatesPage — error state', () => {
  it('shows an error state with a retry action when the fuel rates fetch fails', async () => {
    const user = userEvent.setup()
    pricingConfigApi.useFuelRatesQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { code: 'PRICING_CONFIG_MISSING' },
      refetch: mockRefetch,
    })

    renderPage()

    expect(screen.getByText('No pricing configuration exists for this yet.')).toBeInTheDocument()
    const retryButton = screen.getByRole('button', { name: /retry/i })
    await user.click(retryButton)
    expect(mockRefetch).toHaveBeenCalledTimes(1)
  })
})

describe('FuelRatesPage — form validation', () => {
  it('shows validation errors and does not submit when the Add Fuel Rate form is empty', async () => {
    const user = userEvent.setup()
    pricingConfigApi.useFuelRatesQuery.mockReturnValue({ data: MOCK_RATES, isLoading: false, isError: false })

    renderPage()

    await user.click(screen.getByRole('button', { name: /add fuel rate/i }))

    expect(await screen.findByText('Fuel type is required')).toBeInTheDocument()
    expect(screen.getByText('Price per litre is required')).toBeInTheDocument()
    expect(screen.getByText('Source is required')).toBeInTheDocument()
    expect(screen.getByText('Effective date is required')).toBeInTheDocument()
    expect(mockMutateAsync).not.toHaveBeenCalled()
  })
})

describe('FuelRatesPage — API integration', () => {
  it('submits a valid fuel rate and calls the create mutation with the mapped payload', async () => {
    const user = userEvent.setup()
    mockMutateAsync.mockResolvedValueOnce({ ...MOCK_RATES[0], fuelPriceRateId: 'rate-new' })
    pricingConfigApi.useFuelRatesQuery.mockReturnValue({ data: MOCK_RATES, isLoading: false, isError: false })

    renderPage()

    await user.selectOptions(screen.getByLabelText('Fuel Type'), 'AutoDiesel')
    await user.type(screen.getByLabelText('Price per Litre (LKR)'), '360.75')
    await user.type(screen.getByLabelText('Source'), 'ceypetco.gov.lk')
    const dateInput = screen.getByLabelText('Effective From')
    await user.type(dateInput, '2026-10-01T08:00')

    await user.click(screen.getByRole('button', { name: /add fuel rate/i }))

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          fuelType: 'AutoDiesel',
          pricePerLitre: 360.75,
          source: 'ceypetco.gov.lk',
        }),
      )
    })
  })

  it('maps a backend VALIDATION_ERROR onto the matching form field', async () => {
    const user = userEvent.setup()
    const backendError = new Error('One or more validation errors occurred.')
    backendError.code = 'VALIDATION_ERROR'
    backendError.details = [{ field: 'source', issue: 'This source is already recorded for today.' }]
    mockMutateAsync.mockRejectedValueOnce(backendError)
    pricingConfigApi.useFuelRatesQuery.mockReturnValue({ data: MOCK_RATES, isLoading: false, isError: false })

    renderPage()

    await user.selectOptions(screen.getByLabelText('Fuel Type'), 'AutoDiesel')
    await user.type(screen.getByLabelText('Price per Litre (LKR)'), '360')
    await user.type(screen.getByLabelText('Source'), 'ceypetco.gov.lk')
    await user.type(screen.getByLabelText('Effective From'), '2026-10-01T08:00')
    await user.click(screen.getByRole('button', { name: /add fuel rate/i }))

    expect(await screen.findByText('This source is already recorded for today.')).toBeInTheDocument()
  })

  it('shows a top-level banner for a non-validation backend error', async () => {
    const user = userEvent.setup()
    const backendError = new Error('This fuel rate no longer exists.')
    backendError.code = 'FUEL_PRICE_RATE_NOT_FOUND'
    mockMutateAsync.mockRejectedValueOnce(backendError)
    pricingConfigApi.useFuelRatesQuery.mockReturnValue({ data: MOCK_RATES, isLoading: false, isError: false })

    renderPage()

    await user.selectOptions(screen.getByLabelText('Fuel Type'), 'AutoDiesel')
    await user.type(screen.getByLabelText('Price per Litre (LKR)'), '360')
    await user.type(screen.getByLabelText('Source'), 'ceypetco.gov.lk')
    await user.type(screen.getByLabelText('Effective From'), '2026-10-01T08:00')
    await user.click(screen.getByRole('button', { name: /add fuel rate/i }))

    expect(await screen.findByText('This fuel rate no longer exists.')).toBeInTheDocument()
  })
})
