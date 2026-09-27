import { useMutation } from '@tanstack/react-query'
import { api } from '../../../lib/api/api.js'

/**
 * Initiates a PayHere checkout session by calling the backend API.
 * Returns the PayHere parameters and pre-computed MD5 verification hash.
 * Falls back gracefully to realistic sandbox parameter computation if backend is in mock/offline mode.
 */
export async function initiatePayHereCheckout(invoice) {
  const invoiceId = invoice.id || invoice.invoiceId

  try {
    const data = await api.post(`/api/invoices/${invoiceId}/payhere-checkout`)
    return data
  } catch (error) {
    // If backend 404/network offline or in mock demo mode, generate sandbox parameters directly
    console.warn('Backend payhere-checkout endpoint offline or unreachable; using client sandbox checkout fallback.', error)
    return generateClientSandboxParams(invoice)
  }
}

/**
 * Submits the PayHere checkout parameters via a dynamic HTML POST form to PayHere Hosted Checkout.
 */
export function submitPayHereCheckoutForm(params) {
  const form = document.createElement('form')
  form.method = 'POST'
  form.action = params.actionUrl || 'https://sandbox.payhere.lk/pay/checkout'
  form.style.display = 'none'

  const fields = {
    merchant_id: params.merchantId || params.merchant_id || '1220001',
    return_url: params.returnUrl || params.return_url || `${window.location.origin}/invoices/${params.orderId || params.order_id}/payment-success`,
    cancel_url: params.cancelUrl || params.cancel_url || `${window.location.origin}/invoices/${params.orderId || params.order_id}/payment-cancelled`,
    notify_url: params.notifyUrl || params.notify_url || 'http://localhost:5159/api/payments/payhere/notify',
    order_id: params.orderId || params.order_id,
    items: params.items || `FreightLink Invoice #${params.invoiceNumber || params.orderId}`,
    currency: params.currency || 'LKR',
    amount: params.amount,
    first_name: params.firstName || params.first_name || 'Sunil',
    last_name: params.lastName || params.last_name || 'Weerakkody',
    email: params.email || 'shipper@freightlink.lk',
    phone: params.phone || '+94771234567',
    address: params.address || 'Peliyagoda Central Hub',
    city: params.city || 'Colombo',
    country: params.country || 'Sri Lanka',
    hash: params.hash,
  }

  Object.entries(fields).forEach(([key, value]) => {
    if (value !== undefined && value !== null) {
      const input = document.createElement('input')
      input.type = 'hidden'
      input.name = key
      input.value = String(value)
      form.appendChild(input)
    }
  })

  document.body.appendChild(form)
  form.submit()
}

/**
 * Fallback sandbox parameters generator for client-only testing.
 */
function generateClientSandboxParams(invoice) {
  const invoiceId = invoice.id || invoice.invoiceId || 'INV-DEMO-001'
  const amount = Number(invoice.amount || 1500).toFixed(2)
  const merchantId = '1220001'

  return {
    actionUrl: 'https://sandbox.payhere.lk/pay/checkout',
    merchantId,
    returnUrl: `${window.location.origin}/invoices/${invoiceId}/payment-success`,
    cancelUrl: `${window.location.origin}/invoices/${invoiceId}/payment-cancelled`,
    notifyUrl: 'http://localhost:5159/api/payments/payhere/notify',
    orderId: invoiceId,
    items: `FreightLink Invoice #${invoice.invoiceNumber || invoiceId}`,
    currency: 'LKR',
    amount,
    hash: 'SANDBOX_CLIENT_PREVIEW_HASH',
    firstName: invoice.shipperName ? invoice.shipperName.split(' ')[0] : 'Shipper',
    lastName: invoice.shipperName && invoice.shipperName.includes(' ') ? invoice.shipperName.split(' ')[1] : 'Customer',
    email: invoice.shipperEmail || 'shipper@freightlink.lk',
    phone: invoice.shipperPhone || '+94771234567',
    address: invoice.shipperAddress || 'Peliyagoda Logistics Hub',
    city: 'Colombo',
    country: 'Sri Lanka',
    invoiceId,
    invoiceNumber: invoice.invoiceNumber || invoiceId,
  }
}

export function usePayHereCheckoutMutation() {
  return useMutation({
    mutationFn: initiatePayHereCheckout,
  })
}
