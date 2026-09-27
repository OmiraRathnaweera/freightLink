import { useEffect, useState, useMemo } from 'react'
import { Plus, Trash2, X, Calculator, Loader2 } from 'lucide-react'
import { fetchRecipients, fetchTrips } from '../api/invoiceApi.js'
import { formatCurrency } from '../lib/formatters.js'

function InvoiceFormModal({ isOpen, onClose, onSubmit, initialInvoice = null, isSubmitting = false }) {
  const isEditing = Boolean(initialInvoice && (initialInvoice.id || initialInvoice.invoiceId))

  const [recipients, setRecipients] = useState([])
  const [trips, setTrips] = useState([])
  const [isLoadingLookups, setIsLoadingLookups] = useState(false)

  const [recipientId, setRecipientId] = useState('')
  const [tripId, setTripId] = useState('')
  const [currency, setCurrency] = useState('LKR')
  const [dueDate, setDueDate] = useState('')
  const [notes, setNotes] = useState('')
  const [discountTotal, setDiscountTotal] = useState(0)
  const [lineItems, setLineItems] = useState([
    { description: 'Standard Freight Haulage', quantity: 1, unitPrice: 25000, taxRate: 0 },
  ])
  const [errors, setErrors] = useState({})

  // Fetch recipients and trips when modal opens
  useEffect(() => {
    if (!isOpen) return

    let isMounted = true
    setIsLoadingLookups(true)

    Promise.all([fetchRecipients(), fetchTrips()])
      .then(([recipList, tripList]) => {
        if (!isMounted) return
        setRecipients(recipList || [])
        setTrips(tripList || [])
      })
      .finally(() => {
        if (isMounted) setIsLoadingLookups(false)
      })

    // Populate fields if editing
    if (initialInvoice) {
      setRecipientId(initialInvoice.recipientId || '')
      setTripId(initialInvoice.tripId || initialInvoice.linkedEntityId || '')
      setCurrency(initialInvoice.currency || 'LKR')
      setDueDate(initialInvoice.dueDate ? String(initialInvoice.dueDate).slice(0, 10) : '')
      setNotes(initialInvoice.notes || '')
      setDiscountTotal(Number(initialInvoice.discountTotal || 0))

      if (initialInvoice.lineItems && initialInvoice.lineItems.length > 0) {
        setLineItems(
          initialInvoice.lineItems.map((li) => ({
            invoiceLineItemId: li.invoiceLineItemId,
            description: li.description || '',
            quantity: Number(li.quantity || 1),
            unitPrice: Number(li.unitPrice || 0),
            taxRate: Number(li.taxRate || 0),
          })),
        )
      } else if (initialInvoice.amount) {
        setLineItems([
          {
            description: 'Freight Services',
            quantity: 1,
            unitPrice: Number(initialInvoice.amount),
            taxRate: 0,
          },
        ])
      }
    } else {
      // Defaults for new invoice
      setRecipientId('')
      setTripId('')
      setCurrency('LKR')
      // Default due date = 7 days from now
      const due = new Date()
      due.setDate(due.getDate() + 7)
      setDueDate(due.toISOString().slice(0, 10))
      setNotes('')
      setDiscountTotal(0)
      setLineItems([
        { description: 'Primary Cargo Haulage Service', quantity: 1, unitPrice: 35000, taxRate: 0 },
      ])
    }

    setErrors({})
    return () => {
      isMounted = false
    }
  }, [isOpen, initialInvoice])

  // Real-time client calculations
  const totals = useMemo(() => {
    let subtotal = 0
    let taxTotal = 0

    const computedItems = lineItems.map((item) => {
      const q = Math.max(0, Number(item.quantity) || 0)
      const p = Math.max(0, Number(item.unitPrice) || 0)
      const t = Math.max(0, Math.min(100, Number(item.taxRate) || 0))

      const itemSubtotal = q * p
      const itemTax = itemSubtotal * (t / 100)
      const itemTotal = itemSubtotal + itemTax

      subtotal += itemSubtotal
      taxTotal += itemTax

      return {
        ...item,
        itemSubtotal,
        itemTax,
        itemTotal,
      }
    })

    const disc = Math.max(0, Number(discountTotal) || 0)
    const finalAmount = Math.max(0, subtotal + taxTotal - disc)

    return {
      computedItems,
      subtotal,
      taxTotal,
      discountTotal: disc,
      finalAmount,
    }
  }, [lineItems, discountTotal])

  if (!isOpen) return null

  const handleAddLineItem = () => {
    setLineItems((prev) => [
      ...prev,
      { description: '', quantity: 1, unitPrice: 0, taxRate: 0 },
    ])
  }

  const handleRemoveLineItem = (index) => {
    if (lineItems.length <= 1) return
    setLineItems((prev) => prev.filter((_, i) => i !== index))
  }

  const handleItemChange = (index, field, value) => {
    setLineItems((prev) => {
      const updated = [...prev]
      updated[index] = { ...updated[index], [field]: value }
      return updated
    })
  }

  const validate = () => {
    const errs = {}
    if (lineItems.length === 0) {
      errs.lineItems = 'At least one line item is required.'
    }
    lineItems.forEach((item, idx) => {
      if (!item.description || !item.description.trim()) {
        errs[`desc_${idx}`] = 'Description is required'
      }
      if (Number(item.quantity) <= 0) {
        errs[`qty_${idx}`] = 'Qty must be > 0'
      }
      if (Number(item.unitPrice) < 0) {
        errs[`price_${idx}`] = 'Price cannot be negative'
      }
    })
    setErrors(errs)
    return Object.keys(errs).length === 0
  }

  const handleSubmit = (issueImmediately) => {
    if (!validate()) return

    const payload = {
      recipientId: recipientId || null,
      tripId: tripId || null,
      currency,
      dueDate: dueDate || null,
      notes: notes.trim() || null,
      discountTotal: Number(discountTotal) || 0,
      lineItems: lineItems.map((li) => ({
        ...(li.invoiceLineItemId ? { invoiceLineItemId: li.invoiceLineItemId } : {}),
        description: li.description.trim(),
        quantity: Number(li.quantity),
        unitPrice: Number(li.unitPrice),
        taxRate: Number(li.taxRate || 0),
      })),
      issueImmediately,
      status: issueImmediately ? 'Issued' : 'Draft',
    }

    onSubmit(payload, isEditing ? initialInvoice.id || initialInvoice.invoiceId : null)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs overflow-y-auto animate-in fade-in duration-200">
      <div
        className="w-full max-w-3xl rounded-2xl bg-white p-6 shadow-2xl ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800 my-8 max-h-[90vh] flex flex-col"
        role="dialog"
        aria-modal="true"
      >
        {/* Header */}
        <div className="flex items-center justify-between pb-4 border-b border-slate-100 dark:border-slate-800 shrink-0">
          <div>
            <h2 className="text-lg font-bold text-slate-900 dark:text-white">
              {isEditing ? `Edit Invoice (${initialInvoice.invoiceNumber || 'Draft'})` : 'Create Manual Invoice'}
            </h2>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              Draft bespoke invoices, specify granular line items, and adjust tax rates dynamically.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-500 dark:hover:bg-slate-800 dark:hover:text-slate-300"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Form Body - Scrollable */}
        <div className="overflow-y-auto flex-1 py-4 pr-1 space-y-6">
          {/* Top row: Recipient & Linked Entity (Trip) */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label htmlFor="recipient-select" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300 mb-1.5">
                Recipient
              </label>
              <select
                id="recipient-select"
                value={recipientId}
                onChange={(e) => setRecipientId(e.target.value)}
                disabled={isLoadingLookups}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              >
                <option value="">-- Unassigned / Direct Billing --</option>
                {recipients.map((recip) => (
                  <option key={recip.recipientId} value={recip.recipientId}>
                    {recip.fullName} ({recip.role}) - {recip.email}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label htmlFor="trip-select" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300 mb-1.5">
                Linked Trip (Optional)
              </label>
              <select
                id="trip-select"
                value={tripId}
                onChange={(e) => setTripId(e.target.value)}
                disabled={isLoadingLookups}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              >
                <option value="">-- No Linked Trip (Standalone) --</option>
                {trips.map((t) => (
                  <option key={t.tripId} value={t.tripId}>
                    Trip #{t.tripId.slice(0, 8)} • {t.status || 'Active'}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Currency and Due Date */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label htmlFor="currency-select" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300 mb-1.5">
                Currency
              </label>
              <select
                id="currency-select"
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              >
                <option value="LKR">LKR (Sri Lankan Rupee)</option>
                <option value="USD">USD (US Dollar)</option>
                <option value="EUR">EUR (Euro)</option>
              </select>
            </div>

            <div>
              <label htmlFor="due-date-input" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300 mb-1.5">
                Due Date
              </label>
              <input
                id="due-date-input"
                type="date"
                value={dueDate}
                onChange={(e) => setDueDate(e.target.value)}
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </div>

            <div>
              <label htmlFor="discount-input" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300 mb-1.5">
                Discount ({currency})
              </label>
              <input
                id="discount-input"
                type="number"
                min="0"
                step="0.01"
                value={discountTotal}
                onChange={(e) => setDiscountTotal(e.target.value)}
                placeholder="0.00"
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </div>
          </div>

          {/* Line Items Repeater */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                Invoice Line Items
              </h3>
              <button
                type="button"
                onClick={handleAddLineItem}
                className="inline-flex items-center gap-1.5 rounded-lg border border-brand-300 bg-brand-50/50 px-2.5 py-1 text-xs font-medium text-brand-700 hover:bg-brand-100 dark:border-brand-800 dark:bg-brand-950/40 dark:text-brand-300"
              >
                <Plus className="h-3.5 w-3.5" />
                Add Item
              </button>
            </div>

            {errors.lineItems && (
              <p className="text-xs font-medium text-rose-600 dark:text-rose-400">{errors.lineItems}</p>
            )}

            <div className="space-y-2">
              {lineItems.map((item, index) => {
                const sub = (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0)
                const tax = sub * ((Number(item.taxRate) || 0) / 100)
                const itemTotal = sub + tax

                return (
                  <div
                    key={index}
                    className="flex flex-col md:flex-row items-start md:items-center gap-2 p-2.5 rounded-xl border border-slate-200 bg-slate-50/50 dark:border-slate-800 dark:bg-slate-800/40"
                  >
                    <div className="flex-1 w-full">
                      <input
                        type="text"
                        value={item.description}
                        onChange={(e) => handleItemChange(index, 'description', e.target.value)}
                        placeholder="Item Description (e.g. Fuel Surcharge)"
                        className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs text-slate-900 placeholder:text-slate-400 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                      />
                      {errors[`desc_${index}`] && (
                        <p className="text-[10px] text-rose-500 mt-0.5">{errors[`desc_${index}`]}</p>
                      )}
                    </div>

                    <div className="w-20">
                      <input
                        type="number"
                        min="0.01"
                        step="0.01"
                        value={item.quantity}
                        onChange={(e) => handleItemChange(index, 'quantity', e.target.value)}
                        placeholder="Qty"
                        title="Quantity"
                        className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-xs text-right text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                      />
                      {errors[`qty_${index}`] && (
                        <p className="text-[10px] text-rose-500 mt-0.5">{errors[`qty_${index}`]}</p>
                      )}
                    </div>

                    <div className="w-28">
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        value={item.unitPrice}
                        onChange={(e) => handleItemChange(index, 'unitPrice', e.target.value)}
                        placeholder="Unit Price"
                        title="Unit Price"
                        className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-xs text-right text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                      />
                      {errors[`price_${index}`] && (
                        <p className="text-[10px] text-rose-500 mt-0.5">{errors[`price_${index}`]}</p>
                      )}
                    </div>

                    <div className="w-20">
                      <div className="relative">
                        <input
                          type="number"
                          min="0"
                          max="100"
                          step="0.1"
                          value={item.taxRate}
                          onChange={(e) => handleItemChange(index, 'taxRate', e.target.value)}
                          placeholder="Tax %"
                          title="Tax Rate Percentage"
                          className="w-full rounded-md border border-slate-300 px-2 py-1.5 text-xs text-right text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white pr-5"
                        />
                        <span className="absolute right-1.5 top-1.5 text-[10px] text-slate-400">%</span>
                      </div>
                    </div>

                    <div className="w-24 text-right">
                      <span className="text-xs font-semibold text-slate-900 dark:text-slate-100">
                        {formatCurrency(itemTotal, currency)}
                      </span>
                    </div>

                    <button
                      type="button"
                      onClick={() => handleRemoveLineItem(index)}
                      disabled={lineItems.length <= 1}
                      className="p-1 text-slate-400 hover:text-rose-500 disabled:opacity-30 disabled:cursor-not-allowed"
                      title="Remove Item"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                )
              })}
            </div>
          </div>

          {/* Notes */}
          <div>
            <label htmlFor="notes-textarea" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300 mb-1.5">
              Invoice Notes & Payment Instructions
            </label>
            <textarea
              id="notes-textarea"
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="e.g. Standard 7-day payment term. Bank transfers to Commercial Bank A/C 10293847."
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-xs text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
            />
          </div>

          {/* Real-time Calculation Summary Box */}
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 dark:border-slate-800 dark:bg-slate-800/60">
            <div className="flex items-center gap-2 mb-3 text-xs font-semibold text-slate-700 dark:text-slate-300">
              <Calculator className="h-4 w-4 text-brand-600 dark:text-brand-400" />
              Calculated Breakdown
            </div>
            <div className="space-y-1.5 text-xs text-slate-600 dark:text-slate-300">
              <div className="flex justify-between">
                <span>Subtotal (Net)</span>
                <span className="font-medium text-slate-900 dark:text-white">{formatCurrency(totals.subtotal, currency)}</span>
              </div>
              <div className="flex justify-between">
                <span>Tax Total</span>
                <span className="font-medium text-slate-900 dark:text-white">{formatCurrency(totals.taxTotal, currency)}</span>
              </div>
              {totals.discountTotal > 0 && (
                <div className="flex justify-between text-emerald-600 dark:text-emerald-400">
                  <span>Discount Total</span>
                  <span className="font-medium">-{formatCurrency(totals.discountTotal, currency)}</span>
                </div>
              )}
              <div className="pt-2 border-t border-slate-200 dark:border-slate-700 flex justify-between text-sm font-bold text-slate-900 dark:text-white">
                <span>Final Total Amount</span>
                <span className="text-brand-700 dark:text-brand-400">{formatCurrency(totals.finalAmount, currency)}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Footer Actions */}
        <div className="flex items-center justify-between pt-4 border-t border-slate-100 dark:border-slate-800 shrink-0">
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="rounded-lg border border-slate-300 px-4 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            Cancel
          </button>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => handleSubmit(false)}
              disabled={isSubmitting}
              className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-4 py-2 text-xs font-medium text-slate-800 hover:bg-slate-50 shadow-xs dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            >
              {isSubmitting && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
              Save Draft
            </button>

            <button
              type="button"
              onClick={() => handleSubmit(true)}
              disabled={isSubmitting}
              className="inline-flex items-center gap-1.5 rounded-lg bg-brand-600 px-4 py-2 text-xs font-semibold text-white shadow-xs hover:bg-brand-700 focus:ring-2 focus:ring-brand-500 focus:ring-offset-2"
            >
              {isSubmitting && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
              {isEditing ? 'Issue & Finalize' : 'Issue Immediately'}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}

export default InvoiceFormModal
