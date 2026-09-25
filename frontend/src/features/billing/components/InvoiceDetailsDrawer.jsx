import { useEffect } from 'react'
import {
  Building2,
  Clock,
  CreditCard,
  FileText,
  MapPin,
  Printer,
  Truck,
  X,
} from 'lucide-react'
import InvoiceStatusBadge from './InvoiceStatusBadge.jsx'
import { formatCurrency, formatInvoiceDate } from '../lib/formatters.js'
import Button from '../../../components/Button.jsx'

function InvoiceDetailsDrawer({ invoice, isOpen, onClose }) {
  // Close drawer on escape key
  useEffect(() => {
    function handleKeyDown(e) {
      if (e.key === 'Escape') onClose()
    }
    if (isOpen) {
      document.addEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'hidden'
    }
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = 'unset'
    }
  }, [isOpen, onClose])

  if (!isOpen || !invoice) return null

  return (
    <div className="fixed inset-0 z-50 overflow-hidden">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-primary/40 backdrop-blur-xs transition-opacity duration-200"
        onClick={onClose}
        aria-hidden="true"
      />

      <div className="fixed inset-y-0 right-0 flex max-w-full pl-10">
        <div className="w-screen max-w-md md:max-w-xl bg-surface-container-lowest shadow-2xl flex flex-col h-full border-l border-slate-border">
          {/* Header */}
          <div className="flex items-center justify-between border-b border-slate-border px-6 py-4 bg-slate-50/50">
            <div className="flex items-center gap-3">
              <span className="font-heading text-xl font-bold tracking-tight text-on-surface">
                {invoice.invoiceNumber}
              </span>
              <InvoiceStatusBadge status={invoice.status} />
            </div>
            <button
              type="button"
              onClick={onClose}
              className="rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 transition-colors"
              aria-label="Close drawer"
            >
              <X className="h-5 w-5" />
            </button>
          </div>

          {/* Drawer Body - Scrollable */}
          <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">
            {/* Quick Summary Grid */}
            <div className="grid grid-cols-2 gap-4 rounded-lg border border-slate-border bg-slate-50/60 p-4">
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Related Load Ref</p>
                <div className="mt-1 flex items-center gap-1.5">
                  <span className="rounded bg-white px-2 py-0.5 font-mono text-sm font-semibold text-primary border border-slate-200">
                    {invoice.loadRef}
                  </span>
                </div>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Date Raised</p>
                <p className="mt-1 font-mono text-sm text-on-surface">{formatInvoiceDate(invoice.issueDate)}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Payment Due</p>
                <p className="mt-1 font-mono text-sm text-on-surface">{formatInvoiceDate(invoice.dueDate)}</p>
              </div>
              <div>
                <p className="text-xs uppercase tracking-wider text-slate-500 font-semibold">Total Amount</p>
                <p className="mt-1 font-mono text-base font-bold text-on-surface">
                  LKR {formatCurrency(invoice.amount)}
                </p>
              </div>
            </div>

            {/* Shipper Details */}
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2 flex items-center gap-1.5">
                <Building2 className="h-3.5 w-3.5 text-primary" /> Shipper / Customer
              </h3>
              <div className="rounded-lg border border-slate-border p-4 bg-white space-y-1.5">
                <p className="font-semibold text-body-md text-on-surface">{invoice.shipperName}</p>
                {invoice.shipperEmail && (
                  <p className="text-body-md text-slate-600">{invoice.shipperEmail}</p>
                )}
                {invoice.shipperPhone && (
                  <p className="text-body-md text-slate-600">{invoice.shipperPhone}</p>
                )}
                {invoice.shipperAddress && (
                  <p className="text-xs text-slate-500 mt-2 flex items-start gap-1">
                    <MapPin className="h-3.5 w-3.5 shrink-0 text-slate-400 mt-0.5" />
                    {invoice.shipperAddress}
                  </p>
                )}
              </div>
            </div>

            {/* Shipment & Cargo Info */}
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2 flex items-center gap-1.5">
                <Truck className="h-3.5 w-3.5 text-primary" /> Cargo & Logistics Route
              </h3>
              <div className="rounded-lg border border-slate-border p-4 bg-white space-y-2">
                <div>
                  <p className="text-xs text-slate-500">Route</p>
                  <p className="text-body-md font-medium text-on-surface">{invoice.route}</p>
                </div>
                <div>
                  <p className="text-xs text-slate-500">Cargo Description</p>
                  <p className="text-body-md text-on-surface-variant">{invoice.cargoDescription}</p>
                </div>
              </div>
            </div>

            {/* Financial Breakdown */}
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2 flex items-center gap-1.5">
                <FileText className="h-3.5 w-3.5 text-primary" /> Financial Breakdown
              </h3>
              <div className="rounded-lg border border-slate-border p-4 bg-white space-y-2">
                <div className="flex justify-between text-body-md">
                  <span className="text-slate-600">Base Freight Fare</span>
                  <span className="font-mono text-on-surface">LKR {formatCurrency(invoice.baseFare)}</span>
                </div>
                <div className="flex justify-between text-body-md">
                  <span className="text-slate-600">Fuel Surcharge (Regulated Index)</span>
                  <span className="font-mono text-on-surface">LKR {formatCurrency(invoice.fuelSurcharge)}</span>
                </div>
                <div className="flex justify-between text-body-md">
                  <span className="text-slate-600">Platform Handling Fee</span>
                  <span className="font-mono text-on-surface">LKR {formatCurrency(invoice.platformFee)}</span>
                </div>
                {invoice.taxAmount > 0 && (
                  <div className="flex justify-between text-body-md">
                    <span className="text-slate-600">Applicable Tax</span>
                    <span className="font-mono text-on-surface">LKR {formatCurrency(invoice.taxAmount)}</span>
                  </div>
                )}
                <div className="border-t border-slate-border pt-2 mt-2 flex justify-between items-center">
                  <span className="font-bold text-on-surface">Total Payable</span>
                  <span className="font-mono text-lg font-bold text-primary">
                    LKR {formatCurrency(invoice.amount)}
                  </span>
                </div>
              </div>
            </div>

            {/* Payment Gateway & Settlement (PayHere LK reference) */}
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2 flex items-center gap-1.5">
                <CreditCard className="h-3.5 w-3.5 text-primary" /> Gateway Transaction Details
              </h3>
              <div className="rounded-lg border border-slate-border p-4 bg-white space-y-2">
                <div className="flex justify-between items-center text-body-md">
                  <span className="text-slate-600">Gateway Provider</span>
                  <span className="font-medium text-on-surface">{invoice.gatewayProvider || 'PayHere LK'}</span>
                </div>
                <div className="flex justify-between items-center text-body-md">
                  <span className="text-slate-600">Gateway Transaction ID</span>
                  <span className="font-mono text-sm font-semibold text-primary">
                    {invoice.gatewayTxn || '— (No txn initiated)'}
                  </span>
                </div>
                <div className="flex justify-between items-center text-body-md">
                  <span className="text-slate-600">Payment Channel</span>
                  <span className="text-slate-700">{invoice.paymentMethod || 'PayHere Online'}</span>
                </div>
                {invoice.paidDate && (
                  <div className="flex justify-between items-center text-body-md">
                    <span className="text-slate-600">Settled On</span>
                    <span className="font-mono text-sm text-emerald-700 font-medium">
                      {formatInvoiceDate(invoice.paidDate)}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Activity Log / Audit Trail */}
            {invoice.timeline && invoice.timeline.length > 0 && (
              <div>
                <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-3 flex items-center gap-1.5">
                  <Clock className="h-3.5 w-3.5 text-primary" /> Activity Log
                </h3>
                <div className="relative pl-6 space-y-4 before:absolute before:left-2 before:top-2 before:bottom-2 before:w-0.5 before:bg-slate-200">
                  {invoice.timeline.map((entry, index) => (
                    <div key={index} className="relative">
                      <div className="absolute -left-6 top-1 h-2.5 w-2.5 rounded-full border-2 border-white bg-primary shadow-xs" />
                      <p className="font-mono text-xs text-slate-500">{entry.timestamp}</p>
                      <p className="text-body-md text-on-surface mt-0.5">{entry.event}</p>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Case Notes / Internal Notes */}
            {invoice.notes && (
              <div>
                <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2">Internal Notes</h3>
                <div className="rounded-lg border border-slate-border bg-slate-50 p-3 text-body-md text-on-surface-variant">
                  {invoice.notes}
                </div>
              </div>
            )}
          </div>

          {/* Footer Actions */}
          <div className="border-t border-slate-border px-6 py-4 bg-slate-50/50 flex items-center justify-between gap-3">
            <button
              type="button"
              onClick={() => window.print()}
              className="inline-flex items-center gap-1.5 rounded-md border border-slate-300 bg-white px-3.5 py-2 text-body-md font-medium text-slate-700 hover:bg-slate-50 transition-colors cursor-pointer"
            >
              <Printer className="h-4 w-4" />
              Print
            </button>
            <div className="flex items-center gap-2">
              <Button variant="secondary" onClick={onClose}>
                Close
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}

export default InvoiceDetailsDrawer
