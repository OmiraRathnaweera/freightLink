import { api } from '../../../lib/api/api.js'

/**
 * Invoice management API client endpoints.
 */

export async function fetchInvoices(params = {}) {
  try {
    const data = await api.get('/invoices', { params })
    return data
  } catch (error) {
    console.error('Failed to fetch invoices from API:', error)
    throw error
  }
}

export async function fetchInvoiceById(id) {
  try {
    const data = await api.get(`/invoices/${id}`)
    return data
  } catch (error) {
    console.error(`Failed to fetch invoice ${id}:`, error)
    throw error
  }
}

export async function createInvoice(payload) {
  try {
    const data = await api.post('/invoices', payload)
    return data
  } catch (error) {
    console.error('Failed to create invoice:', error)
    throw error
  }
}

export async function updateInvoice(id, payload) {
  try {
    const data = await api.put(`/invoices/${id}`, payload)
    return data
  } catch (error) {
    console.error(`Failed to update invoice ${id}:`, error)
    throw error
  }
}

export async function issueInvoice(id) {
  try {
    const data = await api.post(`/invoices/${id}/issue`)
    return data
  } catch (error) {
    console.error(`Failed to issue invoice ${id}:`, error)
    throw error
  }
}

export async function voidInvoice(id, voidReason) {
  try {
    const data = await api.post(`/invoices/${id}/void`, { voidReason })
    return data
  } catch (error) {
    console.error(`Failed to void invoice ${id}:`, error)
    throw error
  }
}

/**
 * Submits an already-uploaded file (via POST /files/single) as the Shipper's proof-of-payment
 * receipt for an invoice. Advances the invoice to PaymentPending for Agency review.
 */
export async function uploadPaymentProof(id, publicId) {
  try {
    const data = await api.post(`/invoices/${id}/payment-proof`, { publicId })
    return data
  } catch (error) {
    console.error(`Failed to submit payment receipt for invoice ${id}:`, error)
    throw error
  }
}

/**
 * Confirms a Shipper's submitted payment receipt and closes the invoice as Paid (Agency only).
 */
export async function confirmPayment(id) {
  try {
    const data = await api.post(`/invoices/${id}/confirm-payment`)
    return data
  } catch (error) {
    console.error(`Failed to confirm payment for invoice ${id}:`, error)
    throw error
  }
}

export async function fetchRecipients() {
  try {
    const data = await api.get('/invoices/recipients')
    return data
  } catch (error) {
    console.warn('Failed to fetch recipients from API:', error)
    return []
  }
}

export async function fetchTrips() {
  try {
    const data = await api.get('/trips', { params: { pageSize: 50 } })
    return data?.items || []
  } catch (error) {
    console.warn('Failed to fetch trips from API:', error)
    return []
  }
}
