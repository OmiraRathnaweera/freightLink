import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClientProvider } from '@tanstack/react-query'
import InvoiceFormModal from '../../components/InvoiceFormModal.jsx'
import { createTestQueryClient } from '../../../../test/testUtils.jsx'
import * as tripsApi from '../../../trips/api/tripsApi.js'

// This system has no direct customers and no manual price negotiation: every invoice bills the
// shipper of an already-delivered trip, at the price already agreed when the job was accepted.
// These tests guard that shape — a required trip link, an amount pre-filled from the trip's
// agreed price (but still editable), and no resurrected multi-line-item/currency/discount/due-date
// fields.
vi.mock('../../../trips/api/tripsApi.js', async (importOriginal) => {
  const actual = await importOriginal()
  return { ...actual, useTripsQuery: vi.fn(), useTripDetailQuery: vi.fn() }
})

function stubEligibleTrips(items) {
  tripsApi.useTripsQuery.mockReturnValue({
    data: { items, page: 1, pageSize: 100, totalItems: items.length, totalPages: 1 },
    isLoading: false,
  })
}

// Mirrors the real hook's `enabled: Boolean(tripId)` gating: only resolves once the given tripId
// is actually selected, matching what a disabled TanStack Query returns (`data: undefined`)
// beforehand. A mock that ignored the id argument would make `selectedTrip` truthy from the very
// first render, which breaks the component's dependency-array-driven pre-fill effect.
function stubTripDetail(trip) {
  tripsApi.useTripDetailQuery.mockImplementation((id) =>
    trip && id === trip.tripId ? { data: trip, isLoading: false } : { data: undefined, isLoading: false },
  )
}

// InvoiceFormModal only re-populates its fields when its props *change* while mounted (it stays
// mounted across opens in the real app, toggling `isOpen`/`initialInvoice` — see its own comment on
// `resetKey`/`populatedForKey`). So tests must mirror that: mount closed first, then open, rather
// than mounting already-open, or the population effect never fires. It only needs TanStack Query
// (no Redux/Router usage), so a plain QueryClientProvider wrapper is enough here.
function renderModal(props = {}) {
  const onSubmit = vi.fn()
  const onClose = vi.fn()
  const queryClient = createTestQueryClient()
  const wrap = (ui) => <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>

  const { rerender } = render(wrap(<InvoiceFormModal isOpen={false} onClose={onClose} onSubmit={onSubmit} />))
  rerender(wrap(<InvoiceFormModal isOpen onClose={onClose} onSubmit={onSubmit} {...props} />))
  return { onSubmit, onClose }
}

afterEach(() => {
  vi.mocked(tripsApi.useTripsQuery).mockReset()
  vi.mocked(tripsApi.useTripDetailQuery).mockReset()
  cleanup()
})

describe('InvoiceFormModal — create mode (trip-linked, single amount)', () => {
  it('lists only the eligible (Delivered, uninvoiced) trips fetched via hasInvoice: false', () => {
    stubEligibleTrips([{ tripId: 'trip-1', referenceCode: 'LD-CMB-KDY-01' }])
    stubTripDetail(undefined)
    renderModal()

    expect(tripsApi.useTripsQuery).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'Delivered', hasInvoice: false }),
      expect.anything(),
    )
    expect(screen.getByRole('option', { name: 'LD-CMB-KDY-01' })).toBeInTheDocument()
  })

  it('rejects submission with no trip selected and no amount', async () => {
    const user = userEvent.setup()
    stubEligibleTrips([])
    stubTripDetail(undefined)
    const { onSubmit } = renderModal()

    await user.click(screen.getByRole('button', { name: 'Save Draft' }))

    expect(screen.getByText('Select the delivered trip to bill.')).toBeInTheDocument()
    expect(screen.getByText('Amount must be greater than zero.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('pre-fills the amount from the selected trip’s agreed price and shows the shipper name', async () => {
    const user = userEvent.setup()
    stubEligibleTrips([{ tripId: 'trip-1', referenceCode: 'LD-CMB-KDY-01' }])
    stubTripDetail({
      tripId: 'trip-1',
      referenceCode: 'LD-CMB-KDY-01',
      shipperName: 'Acme Traders',
      agreedPrice: 45000,
    })
    renderModal()

    await user.selectOptions(screen.getByLabelText('Delivered Trip'), 'trip-1')

    expect(await screen.findByDisplayValue('45000')).toBeInTheDocument()
    expect(screen.getByText('Acme Traders')).toBeInTheDocument()
  })

  it('submits {tripId, amount, notes, issueImmediately} with no lineItems/currency/discount/dueDate', async () => {
    const user = userEvent.setup()
    stubEligibleTrips([{ tripId: 'trip-1', referenceCode: 'LD-CMB-KDY-01' }])
    stubTripDetail({
      tripId: 'trip-1',
      referenceCode: 'LD-CMB-KDY-01',
      shipperName: 'Acme Traders',
      agreedPrice: 45000,
    })
    const { onSubmit } = renderModal()

    await user.selectOptions(screen.getByLabelText('Delivered Trip'), 'trip-1')
    await screen.findByDisplayValue('45000')
    await user.click(screen.getByRole('button', { name: 'Issue Immediately' }))

    expect(onSubmit).toHaveBeenCalledTimes(1)
    const [payload, invoiceId] = onSubmit.mock.calls[0]
    expect(payload).toEqual({ tripId: 'trip-1', amount: 45000, notes: null, issueImmediately: true })
    expect(invoiceId).toBeNull()
  })

  it('lets Agency Staff override the pre-filled amount before submitting', async () => {
    const user = userEvent.setup()
    stubEligibleTrips([{ tripId: 'trip-1', referenceCode: 'LD-CMB-KDY-01' }])
    stubTripDetail({ tripId: 'trip-1', referenceCode: 'LD-CMB-KDY-01', shipperName: 'Acme Traders', agreedPrice: 45000 })
    const { onSubmit } = renderModal()

    await user.selectOptions(screen.getByLabelText('Delivered Trip'), 'trip-1')
    const amountInput = await screen.findByDisplayValue('45000')
    await user.clear(amountInput)
    await user.type(amountInput, '47500')
    await user.click(screen.getByRole('button', { name: 'Save Draft' }))

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ amount: 47500, issueImmediately: false }),
      null,
    )
  })
})

describe('InvoiceFormModal — edit mode', () => {
  it('has no trip picker and pre-fills the amount from the existing invoice', () => {
    stubEligibleTrips([])
    stubTripDetail(undefined)
    renderModal({
      initialInvoice: {
        invoiceId: 'inv-1',
        invoiceNumber: 'INV-0001',
        recipientName: 'Acme Traders',
        totalAmount: 45000,
        notes: 'Bank transfer preferred',
      },
    })

    expect(screen.queryByLabelText('Delivered Trip')).not.toBeInTheDocument()
    expect(screen.getByDisplayValue('45000')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Bank transfer preferred')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeInTheDocument()
  })

  it('submits only {amount, notes} on save, scoped to the invoice id', async () => {
    const user = userEvent.setup()
    stubEligibleTrips([])
    stubTripDetail(undefined)
    const { onSubmit } = renderModal({
      initialInvoice: { invoiceId: 'inv-1', invoiceNumber: 'INV-0001', totalAmount: 45000, notes: '' },
    })

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    expect(onSubmit).toHaveBeenCalledWith({ amount: 45000, notes: null }, 'inv-1')
  })
})
