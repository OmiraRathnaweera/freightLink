import { useState } from 'react'
import { Link } from 'react-router-dom'
import { PackageOpen } from 'lucide-react'
import PageHeader from '../../../components/PageHeader.jsx'
import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import { cx } from '../../../lib/cx.js'
import { LoadStatus } from '../../../lib/enums.js'
import { MOCK_LOADS } from '../mockData.js' // TODO: replace with real data
import LoadsTable from '../components/LoadsTable.jsx'

// My Loads dashboard — cloned from the Stitch "My Loads — Dashboard"
// screen. Real route (/loads).
const TABS = [
  { id: 'all', label: 'All Loads', filter: () => true },
  { id: 'pending', label: 'Pending', filter: (load) => load.status === LoadStatus.DRAFT || load.status === LoadStatus.POSTED },
  { id: 'matched', label: 'Matched', filter: (load) => load.status === LoadStatus.MATCHED },
  { id: 'inTransit', label: 'In Transit', filter: (load) => load.status === LoadStatus.IN_TRANSIT },
]

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
