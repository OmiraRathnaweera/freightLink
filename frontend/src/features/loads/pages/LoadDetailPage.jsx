import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, Paperclip } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import { LoadStatus } from '../../../lib/enums.js'
import { getLoadById } from '../mockData.js' // TODO: replace with real data
import { getLoadStatusTone } from '../lib/statusTone.js'
import { formatWeight } from '../lib/format.js'
import WorkflowTimeline from '../components/WorkflowTimeline.jsx'
import ActivityLog from '../components/ActivityLog.jsx'
import RateBreakdownCard from '../components/RateBreakdownCard.jsx'
import RouteMapCard from '../components/RouteMapCard.jsx'

// Load Detail — cloned from the Stitch "Load Detail — Matched State"
// screen. Real route (/loads/:loadId).
const TIMELINE_STEPS = ['Load Created', 'Posted', 'Matched', 'Proposal Sent', 'In Transit', 'Delivered']

function LoadDetailPage() {
  const { loadId } = useParams()
  const load = getLoadById(loadId)

  if (!load) {
    return (
      <div className="space-y-6">
        <Link to="/loads" className="inline-flex items-center gap-1 text-body-md text-secondary hover:text-primary">
          <ArrowLeft className="h-4 w-4" strokeWidth={1.5} /> Back to My Loads
        </Link>
        <Card>
          <EmptyState title={`No load found for "${loadId}"`} description="Check the load ID and try again." />
        </Card>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Link to="/loads" className="mb-2 inline-flex items-center gap-1 text-body-md text-secondary hover:text-primary">
            <ArrowLeft className="h-4 w-4" strokeWidth={1.5} /> Back to My Loads
          </Link>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-headline-lg text-on-surface">Load {load.id}</h1>
            <StatusBadge tone={getLoadStatusTone(LoadStatus.MATCHED)}>Matched</StatusBadge>
          </div>
          <p className="mt-1 text-body-md text-on-surface-variant">
            {load.origin} → {load.destination}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button as={Link} to={`/loads/${load.id}/edit`} variant="secondary">
            Edit Details
          </Button>
          <Button variant="primary">Review AI recommendation</Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-1">
          <Card>
            <h3 className="mb-4 text-headline-md text-primary">Workflow Status</h3>
            <WorkflowTimeline steps={TIMELINE_STEPS} currentIndex={2} />
          </Card>

          <Card>
            <h3 className="mb-4 text-headline-md text-primary">System Activity</h3>
            <ActivityLog entries={load.activityLog} />
          </Card>
        </div>

        <div className="space-y-6 lg:col-span-2">
          <Card>
            <h3 className="mb-4 text-headline-md text-primary">Cargo Specifications</h3>
            <dl className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <dt className="text-label-caps text-on-surface-variant">Weight</dt>
                <dd className="text-data-mono text-on-surface">{formatWeight(load.weightKg)}</dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Volume</dt>
                <dd className="text-data-mono text-on-surface">{load.volumeM3} m³</dd>
              </div>
              <div className="sm:col-span-2">
                <dt className="text-label-caps text-on-surface-variant">Description</dt>
                <dd className="text-body-md text-on-surface">{load.cargoDescription}</dd>
              </div>
              <div className="sm:col-span-2">
                <dt className="text-label-caps text-on-surface-variant">Pickup Window</dt>
                <dd className="text-data-mono text-on-surface">{load.pickupWindow}</dd>
              </div>
            </dl>
          </Card>

          <RateBreakdownCard
            title="Proposed Rate"
            lineItems={[
              { label: 'Base Rate', amount: load.rate.baseRate },
              { label: 'Fuel Surcharge', amount: load.rate.fuelSurcharge },
            ]}
            total={load.rate.total}
          />

          <RouteMapCard origin={load.origin} destination={load.destination} distanceKm={load.distanceKm} />

          <Card>
            <h3 className="mb-4 text-headline-md text-primary">Attached Documents</h3>
            {load.documents.length > 0 ? (
              <ul className="space-y-2">
                {load.documents.map((doc) => (
                  <li key={doc.name} className="flex items-center gap-2 text-body-md text-primary">
                    <Paperclip className="h-4 w-4 text-on-surface-variant" strokeWidth={1.5} />
                    {doc.name}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-body-md text-on-surface-variant">No documents attached yet.</p>
            )}
          </Card>
        </div>
      </div>
    </div>
  )
}

export default LoadDetailPage
