import { ChevronRight, Eye } from 'lucide-react'
import InvoiceStatusBadge from './InvoiceStatusBadge.jsx'
import { formatCurrency, formatInvoiceDate } from '../lib/formatters.js'
import { cx } from '../../../lib/cx.js'

function InvoiceListTable({ invoices, onSelectInvoice }) {
  return (
    <div>
      {/* Desktop Table View (hidden on mobile, visible from md up) */}
      <div className="hidden md:block overflow-x-auto">
        <table className="w-full text-left text-body-md border-collapse">
          <thead>
            <tr className="border-b border-slate-border text-label-caps text-on-surface-variant bg-slate-50/50">
              <th className="py-3 px-4 font-semibold text-slate-600">Invoice ID</th>
              <th className="py-3 px-4 font-semibold text-slate-600">Shipper Entity</th>
              <th className="py-3 px-4 font-semibold text-slate-600">Load Ref</th>
              <th className="py-3 px-4 font-semibold text-slate-600">Amount (LKR)</th>
              <th className="py-3 px-4 font-semibold text-slate-600">Gateway TXN</th>
              <th className="py-3 px-4 font-semibold text-slate-600">Status</th>
              <th className="py-3 px-4 font-semibold text-slate-600">Issued Date</th>
              <th className="py-3 px-4 text-right font-semibold text-slate-600">Action</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {invoices.map((inv, index) => (
              <tr
                key={inv.id}
                onClick={() => onSelectInvoice(inv)}
                className={cx(
                  'group transition-colors cursor-pointer hover:bg-slate-50/80',
                  index % 2 === 1 ? 'bg-slate-50/30' : 'bg-white',
                )}
              >
                {/* Invoice ID */}
                <td className="py-3.5 px-4 font-bold text-on-surface group-hover:text-primary">
                  {inv.invoiceNumber}
                </td>

                {/* Shipper Entity */}
                <td className="py-3.5 px-4 font-medium text-on-surface max-w-[220px] truncate" title={inv.shipperName}>
                  {inv.shipperName}
                </td>

                {/* Load Ref */}
                <td className="py-3.5 px-4">
                  <span className="inline-block rounded border border-slate-200 bg-slate-100/70 px-2 py-0.5 font-mono text-xs font-medium text-slate-700">
                    {inv.loadRef}
                  </span>
                </td>

                {/* Amount (LKR) */}
                <td className="py-3.5 px-4 font-mono font-bold text-on-surface">
                  {formatCurrency(inv.amount)}
                </td>

                {/* Gateway TXN */}
                <td className="py-3.5 px-4 font-mono text-xs text-slate-500">
                  {inv.gatewayTxn ? (
                    <span className="font-semibold text-slate-700">{inv.gatewayTxn}</span>
                  ) : (
                    <span className="text-slate-400">—</span>
                  )}
                </td>

                {/* Status Badge */}
                <td className="py-3.5 px-4">
                  <InvoiceStatusBadge status={inv.status} />
                </td>

                {/* Issued Date */}
                <td className="py-3.5 px-4 text-slate-600 font-mono text-xs whitespace-nowrap">
                  {formatInvoiceDate(inv.issueDate)}
                </td>

                {/* View Details Action */}
                <td className="py-3.5 px-4 text-right">
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation()
                      onSelectInvoice(inv)
                    }}
                    className="inline-flex h-8 w-8 items-center justify-center rounded text-slate-400 hover:bg-slate-100 hover:text-primary transition-colors cursor-pointer"
                    title="View Invoice Details"
                    aria-label={`View ${inv.invoiceNumber}`}
                  >
                    <Eye className="h-4 w-4" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Mobile Card Layout (visible below md) */}
      <div className="md:hidden divide-y divide-slate-100">
        {invoices.map((inv) => (
          <div
            key={inv.id}
            onClick={() => onSelectInvoice(inv)}
            className="p-4 hover:bg-slate-50 transition-colors cursor-pointer space-y-2.5 active:bg-slate-100"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="font-bold text-body-md text-on-surface">{inv.invoiceNumber}</span>
                <span className="rounded border border-slate-200 bg-slate-50 px-1.5 py-0.5 font-mono text-[11px] text-slate-600">
                  {inv.loadRef}
                </span>
              </div>
              <InvoiceStatusBadge status={inv.status} />
            </div>

            <div className="flex justify-between items-baseline">
              <div className="text-body-md text-on-surface font-medium truncate max-w-[200px]">
                {inv.shipperName}
              </div>
              <div className="font-mono font-bold text-body-md text-primary">
                LKR {formatCurrency(inv.amount)}
              </div>
            </div>

            <div className="flex items-center justify-between text-xs text-slate-500 pt-1">
              <span>Issued: {formatInvoiceDate(inv.issueDate)}</span>
              <span className="flex items-center gap-1 text-primary font-medium">
                View <ChevronRight className="h-3.5 w-3.5" />
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

export default InvoiceListTable
