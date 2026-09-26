import { api } from '../../../lib/api/api.js'

/**
 * Invoice management API client endpoints.
 */

export async function fetchInvoices(params = {}) {
  try {
    const data = await api.get('/api/invoices', { params })
    return data
  } catch (error) {
    console.error('Failed to fetch invoices from API:', error)
    throw error
  }
}

export async function fetchInvoiceById(id) {
  try {
    const data = await api.get(`/api/invoices/${id}`)
    return data
  } catch (error) {
    console.error(`Failed to fetch invoice ${id}:`, error)
    throw error
  }
}

export async function createInvoice(payload) {
  try {
    const data = await api.post('/api/invoices', payload)
    return data
  } catch (error) {
    console.error('Failed to create invoice:', error)
    throw error
  }
}

export async function updateInvoice(id, payload) {
  try {
    const data = await api.put(`/api/invoices/${id}`, payload)
    return data
  } catch (error) {
    console.error(`Failed to update invoice ${id}:`, error)
    throw error
  }
}

export async function issueInvoice(id) {
  try {
    const data = await api.post(`/api/invoices/${id}/issue`)
    return data
  } catch (error) {
    console.error(`Failed to issue invoice ${id}:`, error)
    throw error
  }
}

export async function voidInvoice(id, voidReason) {
  try {
    const data = await api.post(`/api/invoices/${id}/void`, { voidReason })
    return data
  } catch (error) {
    console.error(`Failed to void invoice ${id}:`, error)
    throw error
  }
}

export async function payInvoice(id, payload = {}) {
  try {
    const data = await api.post(`/api/invoices/${id}/pay`, payload)
    return data
  } catch (error) {
    console.error(`Failed to pay invoice ${id}:`, error)
    throw error
  }
}

export async function fetchRecipients() {
  try {
    const data = await api.get('/api/invoices/recipients')
    return data
  } catch (error) {
    console.warn('Failed to fetch recipients from API:', error)
    return []
  }
}

export async function fetchTrips() {
  try {
    const data = await api.get('/api/v1/trips', { params: { pageSize: 50 } })
    return data?.items || []
  } catch (error) {
    console.warn('Failed to fetch trips from API:', error)
    return []
  }
}
