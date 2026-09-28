import { BarChart3, RotateCcw } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import { useAnalyticsSummaryQuery } from '../api/analyticsApi.js'
import { formatCurrency } from '../../loads/lib/format.js'

// System-wide analytics for Admin (docs/README.md Section 5: Admin's role is agency
// KYC/verification + system-wide analytics only â€” no AI-match approval authority, hence no
// links back into /agent-workflows anywhere on this page). Read-only snapshot from
// GET /admin/analytics/summary; nothing here is editable.
function CategoryCard({ title, counts }) {
  return (
    <Card>
      <p className="text-label-caps text-on-surface-variant">{title}</p>
      <p className="mt-1 text-headline-lg text-primary">{counts.total.toLocaleString()}</p>
      {counts.byLabel.length > 0 && (
        <dl className="mt-3 flex flex-wrap gap-x-4 gap-y-1">
          {counts.byLabel.map((entry) => (
            <div key={entry.label} className="flex items-center gap-1 text-body-sm">
              <dt className="text-on-surface-variant">{entry.label}</dt>
              <dd className="font-semibold text-on-surface">{entry.count.toLocaleString()}</dd>
            </div>
          ))}
        </dl>
      )}
    </Card>
  )
}

function AdminAnalyticsPage() {
  const summaryQuery = useAnalyticsSummaryQuery()

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <BarChart3 className="h-6 w-6 text-primary" strokeWidth={1.5} />
            <h1 className="text-headline-lg text-on-surface">System Analytics</h1>
          </div>
          <p className="mt-1 text-body-md text-on-surface-variant">
            A read-only, on-demand snapshot across every component. Nothing here is editable.
          </p>
        </div>
        <Button variant="secondary" onClick={() => summaryQuery.refetch()} disabled={summaryQuery.isFetching}>
          <RotateCcw className={`h-4 w-4 ${summaryQuery.isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>
      </div>

      {summaryQuery.isLoading && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-32 w-full" />
          ))}
        </div>
      )}

      {summaryQuery.isError && (
        <Card>
          <ErrorState
            description={summaryQuery.error?.message ?? 'Could not load system analytics.'}
            onRetry={summaryQuery.refetch}
          />
        </Card>
      )}

      {summaryQuery.data && (
        <>
          <p className="text-body-sm text-on-surface-variant">
            Generated {new Date(summaryQuery.data.generatedAt).toLocaleString()}
          </p>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <CategoryCard title="Loads" counts={summaryQuery.data.loads} />
            <CategoryCard title="Agencies" counts={summaryQuery.data.agencies} />
            <CategoryCard title="Trips" counts={summaryQuery.data.trips} />
            <CategoryCard title="Assignments" counts={summaryQuery.data.assignments} />
            <CategoryCard title="Disputes" counts={summaryQuery.data.disputes} />
            <CategoryCard title="Users by Role" counts={summaryQuery.data.usersByRole} />
          </div>

          <Card>
            <p className="text-label-caps text-on-surface-variant">Invoices &amp; Revenue</p>
            <div className="mt-2 grid grid-cols-1 gap-4 sm:grid-cols-3">
              <div>
                <p className="text-body-sm text-on-surface-variant">Total Invoices</p>
                <p className="text-headline-md text-on-surface">{summaryQuery.data.invoices.counts.total.toLocaleString()}</p>
              </div>
              <div>
                <p className="text-body-sm text-on-surface-variant">Total Invoiced</p>
                <p className="text-headline-md text-on-surface">{formatCurrency(summaryQuery.data.invoices.totalInvoicedAmount)}</p>
              </div>
              <div>
                <p className="text-body-sm text-on-surface-variant">Total Paid</p>
                <p className="text-headline-md text-status-green-text">{formatCurrency(summaryQuery.data.invoices.totalPaidAmount)}</p>
              </div>
            </div>
            {summaryQuery.data.invoices.counts.byLabel.length > 0 && (
              <dl className="mt-4 flex flex-wrap gap-x-4 gap-y-1 border-t border-slate-border pt-3">
                {summaryQuery.data.invoices.counts.byLabel.map((entry) => (
                  <div key={entry.label} className="flex items-center gap-1 text-body-sm">
                    <dt className="text-on-surface-variant">{entry.label}</dt>
                    <dd className="font-semibold text-on-surface">{entry.count.toLocaleString()}</dd>
                  </div>
                ))}
              </dl>
            )}
          </Card>
        </>
      )}
    </div>
  )
}

export default AdminAnalyticsPage
