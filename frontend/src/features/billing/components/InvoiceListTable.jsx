import { Eye, Edit3, Send, Ban, MoreHorizontal } from 'lucide-react'
import { useState, useRef, useEffect } from 'react'
import InvoiceStatusBadge from './InvoiceStatusBadge.jsx'
import { formatCurrency, formatInvoiceDate } from '../lib/formatters.js'
import { InvoiceStatus } from '../../../lib/enums.js'
import { cx } from '../../../lib/cx.js'

function ActionMenu({
  invoice,
  onView,
  onEdit,
  onIssue,
  onVoid,
  isAgent = true,
}) {
  const [isOpen, setIsOpen] = useState(false)
  const menuRef = useRef(null)

  const isDraft = invoice.status === InvoiceStatus.DRAFT
  const isVoidable =
    invoice.status === InvoiceStatus.DRAFT ||
    invoice.status === InvoiceStatus.ISSUED ||
    invoice.status === InvoiceStatus.PAYMENT_PENDING ||
    invoice.status === InvoiceStatus.FAILED

  useEffect(() => {
    function handleClickOutside(event) {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setIsOpen(false)
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside)
    }
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [isOpen])

  return (
    <div className="relative inline-block text-left" ref={menuRef}>
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation()
          setIsOpen((prev) => !prev)
        }}
        className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900"
        title="Invoice Actions"
      >
        <MoreHorizontal className="h-4 w-4" />
      </button>

      {isOpen && (
        <div
          className="absolute right-0 z-30 mt-1 w-44 origin-top-right rounded-xl bg-white p-1.5 shadow-xl ring-1 ring-slate-200 animate-in fade-in duration-100"
          onClick={(e) => e.stopPropagation()}
        >
          {/* View Details: Allowed for all roles */}
          <button
            type="button"
            onClick={() => {
              setIsOpen(false)
              onView(invoice)
            }}
            className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            <Eye className="h-3.5 w-3.5 text-slate-500" />
            View Details
          </button>

          {/* Edit Draft: Strictly for Agent role on Draft invoices */}
          {isAgent && isDraft && onEdit && (
            <button
              type="button"
              onClick={() => {
                setIsOpen(false)
                onEdit(invoice)
              }}
              className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-blue-600 hover:bg-blue-50"
            >
              <Edit3 className="h-3.5 w-3.5" />
              Edit Draft
            </button>
          )}

          {/* Issue Invoice: Strictly for Agent role on Draft invoices */}
          {isAgent && isDraft && onIssue && (
            <button
              type="button"
              onClick={() => {
                setIsOpen(false)
                onIssue(invoice)
              }}
              className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-emerald-600 hover:bg-emerald-50"
            >
              <Send className="h-3.5 w-3.5" />
              Issue Invoice
            </button>
          )}

          {/* Void Invoice: Strictly for Agent role on cancellable invoices */}
          {isAgent && isVoidable && onVoid && (
            <button
              type="button"
              onClick={() => {
                setIsOpen(false)
                onVoid(invoice)
              }}
              className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-rose-600 hover:bg-rose-50"
            >
              <Ban className="h-3.5 w-3.5" />
              Void Invoice
            </button>
          )}
        </div>
      )}
    </div>
  )
}

function InvoiceListTable({
  invoices,
  onSelectInvoice,
  onEditInvoice,
  onIssueInvoice,
  onVoidInvoice,
  isAgent = true,
}) {
  return (
    <div>
      {/* Desktop Table View */}
      <div className="hidden md:block overflow-x-auto">
        <table className="w-full text-left text-body-md border-collapse">
          <thead>
            <tr className="border-b border-slate-200 text-xs uppercase tracking-wider text-slate-500 bg-slate-50/60">
              <th className="py-3 px-4 font-semibold">Invoice #</th>
              <th className="py-3 px-4 font-semibold">Recipient</th>
              <th className="py-3 px-4 font-semibold">Linked Entity</th>
              <th className="py-3 px-4 font-semibold">Status</th>
              <th className="py-3 px-4 font-semibold">Issue Date</th>
              <th className="py-3 px-4 font-semibold text-right">Total</th>
              <th className="py-3 px-4 text-right font-semibold">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {invoices.map((inv, index) => {
              const recipientDisplay = inv.recipientName || inv.shipperName || 'Direct Customer'
              const linkedDisplay = inv.tripId || inv.linkedEntityId || inv.loadRef || 'Standalone'

              return (
                <tr
                  key={inv.id || inv.invoiceId}
                  onClick={() => onSelectInvoice(inv)}
                  className={cx(
                    'group transition-colors cursor-pointer hover:bg-slate-50/80',
                    index % 2 === 1 ? 'bg-slate-50/20' : 'bg-white',
                  )}
                >
                  {/* Invoice # */}
                  <td className="py-3.5 px-4 font-bold text-slate-900 group-hover:text-primary">
                    {inv.invoiceNumber}
                  </td>

                  {/* Recipient */}
                  <td className="py-3.5 px-4 font-medium text-slate-700 max-w-[200px] truncate" title={recipientDisplay}>
                    <div className="flex flex-col">
                      <span>{recipientDisplay}</span>
                      {inv.recipientRole && (
                        <span className="text-[10px] text-slate-400 capitalize">{inv.recipientRole}</span>
                      )}
                    </div>
                  </td>

                  {/* Linked Entity */}
                  <td className="py-3.5 px-4 font-mono text-xs text-slate-600">
                    {linkedDisplay !== 'Standalone' ? (
                      <span className="inline-block rounded border border-slate-200 bg-slate-100/70 px-2 py-0.5 font-medium text-slate-700">
                        {typeof linkedDisplay === 'string' && linkedDisplay.length > 12 ? linkedDisplay.slice(0, 8) : linkedDisplay}
                      </span>
                    ) : (
                      <span className="text-slate-400 italic">Standalone</span>
                    )}
                  </td>

                  {/* Status Badge */}
                  <td className="py-3.5 px-4">
                    <InvoiceStatusBadge status={inv.status} />
                  </td>

                  {/* Issue Date */}
                  <td className="py-3.5 px-4 text-slate-600 font-mono text-xs whitespace-nowrap">
                    {formatInvoiceDate(inv.issuedAt || inv.issueDate || inv.createdAt)}
                  </td>

                  {/* Total */}
                  <td className="py-3.5 px-4 font-mono font-bold text-slate-900 text-right">
                    {formatCurrency(inv.totalAmount || inv.amount, inv.currency || 'LKR')}
                  </td>

                  {/* Contextual Action Menu */}
                  <td className="py-3.5 px-4 text-right">
                    <ActionMenu
                      invoice={inv}
                      onView={onSelectInvoice}
                      onEdit={onEditInvoice}
                      onIssue={onIssueInvoice}
                      onVoid={onVoidInvoice}
                      isAgent={isAgent}
                    />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Mobile Card Layout */}
      <div className="md:hidden divide-y divide-slate-100">
        {invoices.map((inv) => (
          <div
            key={inv.id || inv.invoiceId}
            onClick={() => onSelectInvoice(inv)}
            className="p-4 hover:bg-slate-50 cursor-pointer space-y-2.5"
          >
            <div className="flex items-center justify-between">
              <span className="font-bold text-slate-900">{inv.invoiceNumber}</span>
              <InvoiceStatusBadge status={inv.status} />
            </div>

            <div className="flex items-center justify-between text-xs text-slate-600">
              <span>{inv.recipientName || inv.shipperName || 'Direct Customer'}</span>
              <span className="font-bold text-slate-900 font-mono">
                {formatCurrency(inv.totalAmount || inv.amount, inv.currency || 'LKR')}
              </span>
            </div>

            <div className="flex items-center justify-between pt-1 border-t border-slate-100 text-[11px] text-slate-400">
              <span>{formatInvoiceDate(inv.issuedAt || inv.issueDate || inv.createdAt)}</span>
              <div className="flex items-center gap-1" onClick={(e) => e.stopPropagation()}>
                <ActionMenu
                  invoice={inv}
                  onView={onSelectInvoice}
                  onEdit={onEditInvoice}
                  onIssue={onIssueInvoice}
                  onVoid={onVoidInvoice}
                  isAgent={isAgent}
                />
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

export default InvoiceListTable
