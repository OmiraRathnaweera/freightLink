import { RefreshCw, TrendingUp, Wallet, AlertTriangle } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import InvoiceStatusBadge from './InvoiceStatusBadge.jsx'
import { formatCurrency, formatInvoiceDate } from '../lib/formatters.js'
import { useInvoiceSummaryQuery } from '../api/invoiceApi.js'

const STATUS_ROWS = [
  { key: 'draft', label: 'Draft' },
  { key: 'issued', label: 'Issued' },
  { key: 'paymentPending', label: 'Payment Pending' },
  { key: 'paid', label: 'Paid' },
  { key: 'failed', label: 'Failed' },
  { key: 'void', label: 'Void' },
]

/**
 * Admin-only, strictly read-only cashflow overview backed by GET /invoices/summary.
 * Renders no action buttons of any kind — write access is blocked server-side, and this
 * page omits the controls at the UI level too, per ADR-021.
 */
function InvoiceCashflowDashboard() {
  const { data: summary, isLoading, isError, error, refetch, isFetching } = useInvoiceSummaryQuery()

  if (isLoading) {
    return (
      <Card className="flex items-center justify-center py-12">
        <RefreshCw className="h-5 w-5 animate-spin text-slate-400" />
      </Card>
    )
  }

  if (isError) {
    return (
      <Card>
        <ErrorState
          title="Unable to load cashflow summary"
          description={error?.message || 'Please check your connection and try again.'}
          onRetry={refetch}
        />
      </Card>
    )
  }

  const countByStatus = summary?.countByStatus || {}
  const recentActivity = summary?.recentActivity || []

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-slate-500">Total Invoiced</p>
            <TrendingUp className="h-3.5 w-3.5 text-slate-400" />
          </div>
          <p className="mt-1 font-mono text-xl font-bold text-slate-900">
            LKR {formatCurrency(summary?.totalInvoiced || 0)}
          </p>
          <p className="mt-1 text-xs text-slate-400">Across all non-void invoices</p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-emerald-700">Total Paid</p>
            <Wallet className="h-3.5 w-3.5 text-emerald-600" />
          </div>
          <p className="mt-1 font-mono text-xl font-bold text-emerald-700">
            LKR {formatCurrency(summary?.totalPaid || 0)}
          </p>
          <p className="mt-1 text-xs text-slate-400">Confirmed settlements</p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center justify-between">
            <p className="text-xs font-bold uppercase tracking-wider text-amber-700">Total Outstanding</p>
            <AlertTriangle className="h-3.5 w-3.5 text-amber-600" />
          </div>
          <p className="mt-1 font-mono text-xl font-bold text-amber-800">
            LKR {formatCurrency(summary?.totalOutstanding || 0)}
          </p>
          <p className="mt-1 text-xs text-slate-400">Invoiced minus paid</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <Card className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-3">
            Invoices by Status
          </h3>
          <div className="space-y-2">
            {STATUS_ROWS.map((row) => (
              <div key={row.key} className="flex items-center justify-between text-sm">
                <span className="text-slate-600">{row.label}</span>
                <span className="font-mono font-semibold text-slate-900">
                  {countByStatus[row.key] ?? 0}
                </span>
              </div>
            ))}
          </div>
        </Card>

        <Card className="p-4">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500">Recent Activity</h3>
            <button
              type="button"
              onClick={() => refetch()}
              disabled={isFetching}
              className="text-slate-400 hover:text-slate-600"
              title="Refresh"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
            </button>
          </div>
          {recentActivity.length === 0 ? (
            <p className="text-sm text-slate-400">No invoice activity yet.</p>
          ) : (
            <div className="space-y-2.5">
              {recentActivity.map((row) => (
                <div key={row.invoiceId} className="flex items-center justify-between text-sm">
                  <div className="flex flex-col">
                    <span className="font-medium text-slate-800">{row.invoiceNumber}</span>
                    <span className="text-[11px] text-slate-400">{formatInvoiceDate(row.updatedAt)}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-xs text-slate-600">
                      {formatCurrency(row.amount, true)}
                    </span>
                    <InvoiceStatusBadge status={row.status} />
                  </div>
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
    </div>
  )
}

export default InvoiceCashflowDashboard
