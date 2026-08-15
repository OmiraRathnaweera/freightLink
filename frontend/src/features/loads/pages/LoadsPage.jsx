import { useState } from 'react'
import { Link } from 'react-router-dom'
import { PackageOpen, MoreVertical } from 'lucide-react'
import PageHeader from '../../../components/PageHeader.jsx'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import { cx } from '../../../lib/cx.js'
import { LoadStatus } from '../../../lib/enums.js'
import { MOCK_LOADS } from '../data/mockLoads.js'
import { formatDate, formatWeight } from '../lib/format.js'

// My Loads dashboard — cloned from the Stitch "My Loads — Dashboard"
// screen. Real route (/loads).
const TABS = [
  { id: 'all', label: 'All Loads', filter: () => true },
  { id: 'pending', label: 'Pending', filter: (load) => load.status === LoadStatus.DRAFT || load.status === LoadStatus.POSTED },
  { id: 'matched', label: 'Matched', filter: (load) => load.status === LoadStatus.MATCHED },
  { id: 'inTransit', label: 'In Transit', filter: (load) => load.status === LoadStatus.IN_TRANSIT },
]

function LoadsTable({ loads }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-body-md">
        <thead>
          <tr className="text-label-caps text-on-surface-variant">
            <th className="py-table-cell-py pr-table-cell-px font-normal">Load ID</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Route</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Weight</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Status</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Posted Date</th>
            <th className="py-table-cell-py font-normal" aria-hidden="true" />
          </tr>
        </thead>
        <tbody>
          {loads.map((load, index) => (
            <tr key={load.id} className={cx('group', index % 2 === 1 && 'bg-slate-50')}>
              <td className="py-table-cell-py pr-table-cell-px">
                <Link to={`/loads/${load.id}`} className="text-data-mono text-primary hover:underline">
                  {load.id}
                </Link>
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface">
                {load.origin} → {load.destination}
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                {formatWeight(load.weightKg)}
              </td>
              <td className="py-table-cell-py pr-table-cell-px">
                <div className="flex items-center gap-2">
                  <StatusBadge status={load.status} />
                  {load.attempt && <span className="text-label-caps text-on-surface-variant">{load.attempt}</span>}
                </div>
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface-variant">{formatDate(load.postedDate)}</td>
              <td className="py-table-cell-py text-right">
                <Link
                  to={`/loads/${load.id}/edit`}
                  className="inline-flex h-8 w-8 items-center justify-center rounded text-on-surface-variant transition-colors hover:bg-slate-100 hover:text-on-surface"
                  aria-label={`Edit ${load.id}`}
                >
                  <MoreVertical className="h-4 w-4" strokeWidth={1.5} />
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function LoadsPage() {
  const [activeTab, setActiveTab] = useState('all')

  const visibleLoads = MOCK_LOADS.filter(TABS.find((tab) => tab.id === activeTab).filter)

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="LOADS"
        title="My Loads"
        description="Track every load you've posted, from draft to delivery."
        actions={
          <Button as={Link} to="/loads/new" variant="primary">
            Post a Load
          </Button>
        }
      />

      <div className="flex flex-wrap gap-2">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setActiveTab(tab.id)}
            className={cx(
              'rounded-full px-4 py-1.5 text-status-badge transition-colors',
              activeTab === tab.id
                ? 'bg-primary text-on-primary'
                : 'bg-surface-container text-on-surface-variant hover:bg-surface-container-high',
            )}
          >
            {tab.label} ({MOCK_LOADS.filter(tab.filter).length})
          </button>
        ))}
      </div>

      <Card className="p-0">
        {visibleLoads.length > 0 ? (
          <LoadsTable loads={visibleLoads} />
        ) : (
          <EmptyState
            icon={PackageOpen}
            title="No loads in this view"
            description="Try a different tab, or post a new load."
            action={
              <Button as={Link} to="/loads/new" variant="primary">
                Post a Load
              </Button>
            }
          />
        )}
      </Card>
    </div>
  )
}

export default LoadsPage
