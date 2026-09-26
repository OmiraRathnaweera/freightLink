import { Eye, Edit3, Send, Ban, MoreHorizontal, CreditCard } from 'lucide-react'
import { useState, useRef, useEffect } from 'react'
import InvoiceStatusBadge from './InvoiceStatusBadge.jsx'
import { formatCurrency, formatInvoiceDate } from '../lib/formatters.js'
import { InvoiceStatus } from '../lib/invoiceStatus.js'
import { cx } from '../../../lib/cx.js'

function ActionMenu({
  invoice,
  onView,
  onEdit,
  onIssue,
  onVoid,
  onPay,
  isAgent = true,
  isAdmin = false,
  isShipper = false,
}) {
  const [isOpen, setIsOpen] = useState(false)
  const menuRef = useRef(null)

  const isDraft = invoice.status === InvoiceStatus.DRAFT || invoice.status === 'Draft'
  const isIssued = invoice.status === InvoiceStatus.ISSUED || invoice.status === 'Issued'
  const isVoidable =
    invoice.status === InvoiceStatus.DRAFT ||
    invoice.status === InvoiceStatus.ISSUED ||
    invoice.status === InvoiceStatus.PAYMENT_PENDING ||
    invoice.status === InvoiceStatus.FAILED ||
    invoice.status === 'Draft' ||
    invoice.status === 'Issued'

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
        className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-slate-500 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-white"
        title="Invoice Actions"
      >
        <MoreHorizontal className="h-4 w-4" />
      </button>

      {isOpen && (
        <div
          className="absolute right-0 z-30 mt-1 w-44 origin-top-right rounded-xl bg-white p-1.5 shadow-xl ring-1 ring-slate-200 dark:bg-slate-900 dark:ring-slate-800 animate-in fade-in duration-100"
          onClick={(e) => e.stopPropagation()}
        >
          {/* View Details: Allowed for all roles */}
          <button
            type="button"
            onClick={() => {
              setIsOpen(false)
              onView(invoice)
            }}
            className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            <Eye className="h-3.5 w-3.5 text-slate-500" />
            View Details
          </button>

          {/* Pay Invoice: Strictly for Shipper on Issued invoices */}
          {isShipper && isIssued && onPay && (
            <button
              type="button"
              onClick={() => {
                setIsOpen(false)
                onPay(invoice)
              }}
              className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-semibold text-emerald-700 hover:bg-emerald-50 dark:text-emerald-400 dark:hover:bg-emerald-950/40"
            >
              <CreditCard className="h-3.5 w-3.5 text-emerald-600" />
              Pay Invoice
            </button>
          )}

          {/* Edit Draft: Strictly for Agent role on Draft invoices */}
          {isAgent && isDraft && onEdit && (
            <button
              type="button"
              onClick={() => {
                setIsOpen(false)
                onEdit(invoice)
              }}
              className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-blue-600 hover:bg-blue-50 dark:text-blue-400 dark:hover:bg-blue-950/40"
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
              className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-emerald-600 hover:bg-emerald-50 dark:text-emerald-400 dark:hover:bg-emerald-950/40"
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
              className="flex w-full items-center gap-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-rose-600 hover:bg-rose-50 dark:text-rose-400 dark:hover:bg-rose-950/40"
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
  onPayInvoice,
  isAgent = true,
  isAdmin = false,
  isShipper = false,
}) {
  return (
    <div>
      {/* Desktop Table View */}
      <div className="hidden md:block overflow-x-auto">
        <table className="w-full text-left text-body-md border-collapse">
          <thead>
            <tr className="border-b border-slate-200 dark:border-slate-800 text-xs uppercase tracking-wider text-slate-500 bg-slate-50/60 dark:bg-slate-800/40">
              <th className="py-3 px-4 font-semibold">Invoice #</th>
              <th className="py-3 px-4 font-semibold">Recipient</th>
              <th className="py-3 px-4 font-semibold">Linked Entity</th>
              <th className="py-3 px-4 font-semibold">Status</th>
              <th className="py-3 px-4 font-semibold">Issue Date</th>
              <th className="py-3 px-4 font-semibold text-right">Total</th>
              <th className="py-3 px-4 text-right font-semibold">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-800/60">
            {invoices.map((inv, index) => {
              const recipientDisplay = inv.recipientName || inv.shipperName || 'Direct Customer'
              const linkedDisplay = inv.tripId || inv.linkedEntityId || inv.loadRef || 'Standalone'

              return (
                <tr
                  key={inv.id || inv.invoiceId}
                  onClick={() => onSelectInvoice(inv)}
                  className={cx(
                    'group transition-colors cursor-pointer hover:bg-slate-50/80 dark:hover:bg-slate-800/50',
                    index % 2 === 1 ? 'bg-slate-50/20 dark:bg-slate-900/40' : 'bg-white dark:bg-slate-900',
                  )}
                >
                  {/* Invoice # */}
                  <td className="py-3.5 px-4 font-bold text-slate-900 dark:text-white group-hover:text-brand-600 dark:group-hover:text-brand-400">
                    {inv.invoiceNumber}
                  </td>

                  {/* Recipient */}
                  <td className="py-3.5 px-4 font-medium text-slate-700 dark:text-slate-200 max-w-[200px] truncate" title={recipientDisplay}>
                    <div className="flex flex-col">
                      <span>{recipientDisplay}</span>
                      {inv.recipientRole && (
                        <span className="text-[10px] text-slate-400 capitalize">{inv.recipientRole}</span>
                      )}
                    </div>
                  </td>

                  {/* Linked Entity */}
                  <td className="py-3.5 px-4 font-mono text-xs text-slate-600 dark:text-slate-400">
                    {linkedDisplay !== 'Standalone' ? (
                      <span className="inline-block rounded border border-slate-200 bg-slate-100/70 px-2 py-0.5 font-medium text-slate-700 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300">
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
                  <td className="py-3.5 px-4 text-slate-600 dark:text-slate-400 font-mono text-xs whitespace-nowrap">
                    {formatInvoiceDate(inv.issuedAt || inv.issueDate || inv.createdAt)}
                  </td>

                  {/* Total */}
                  <td className="py-3.5 px-4 font-mono font-bold text-slate-900 dark:text-white text-right">
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
                      onPay={onPayInvoice}
                      isAgent={isAgent}
                      isAdmin={isAdmin}
                      isShipper={isShipper}
                    />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Mobile Card Layout */}
      <div className="md:hidden divide-y divide-slate-100 dark:divide-slate-800">
        {invoices.map((inv) => (
          <div
            key={inv.id || inv.invoiceId}
            onClick={() => onSelectInvoice(inv)}
            className="p-4 hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer space-y-2.5"
          >
            <div className="flex items-center justify-between">
              <span className="font-bold text-slate-900 dark:text-white">{inv.invoiceNumber}</span>
              <InvoiceStatusBadge status={inv.status} />
            </div>

            <div className="flex items-center justify-between text-xs text-slate-600 dark:text-slate-400">
              <span>{inv.recipientName || inv.shipperName || 'Direct Customer'}</span>
              <span className="font-bold text-slate-900 dark:text-white font-mono">
                {formatCurrency(inv.totalAmount || inv.amount, inv.currency || 'LKR')}
              </span>
            </div>

            <div className="flex items-center justify-between pt-1 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400">
              <span>{formatInvoiceDate(inv.issuedAt || inv.issueDate || inv.createdAt)}</span>
              <div className="flex items-center gap-1" onClick={(e) => e.stopPropagation()}>
                <ActionMenu
                  invoice={inv}
                  onView={onSelectInvoice}
                  onEdit={onEditInvoice}
                  onIssue={onIssueInvoice}
                  onVoid={onVoidInvoice}
                  onPay={onPayInvoice}
                  isAgent={isAgent}
                  isAdmin={isAdmin}
                  isShipper={isShipper}
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
