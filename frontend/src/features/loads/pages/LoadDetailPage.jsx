import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, Sparkles, Truck } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { useLoadDetailQuery } from '../api/loadsApi.js'
import { getLoadStatusTone } from '../lib/statusTone.js'
import { formatCurrency, formatDateTime, formatShipperName, formatWeight } from '../lib/format.js'
import { getLoadErrorMessage } from '../lib/errorMessages.js'
import { canCancelLoad, canEditLoad } from '../lib/loadPermissions.js'
import RouteMapCard from '../components/RouteMapCard.jsx'
import LoadFilesSection from '../components/LoadFilesSection.jsx'
import LoadStatusHistoryCard from '../components/LoadStatusHistoryCard.jsx'
import CancelLoadDialog from '../components/CancelLoadDialog.jsx'
import AcceptShipmentDialog from '../components/AcceptShipmentDialog.jsx'
import PriceEstimateCard from '../components/PriceEstimateCard.jsx'

// Load detail — GET /api/v1/loads/{id}, full LoadResponseDto
// (docs/load-management-api.md Section 3.6). RateBreakdownCard/
// WorkflowTimeline/ActivityLog from the earlier Stitch-cloned version were
// removed: the real API has no data to back them yet (estimatedPrice is a
// single nullable field, workflowRunId is always null pre-Component D) and
// the project rule is "no fake mock API once backend wiring is in place."
// RouteMapCard renders a real OSM/Leaflet map (fed real pickup/dropoff
// coordinates) — no longer the static schematic placeholder it used to be.
function LoadDetailPage() {
  const { loadId } = useParams()
  const [isCancelOpen, setIsCancelOpen] = useState(false)
  const [isAcceptOpen, setIsAcceptOpen] = useState(false)
  const loadQuery = useLoadDetailQuery(loadId)
  const role = useAppSelector((state) => state.auth.role)

  if (loadQuery.isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  if (loadQuery.isError) {
    return (
      <div className="space-y-6">
        <Link to="/loads" className="inline-flex items-center gap-1 text-body-md text-secondary hover:text-primary">
          <ArrowLeft className="h-4 w-4" strokeWidth={1.5} /> Back to My Loads
        </Link>
        <Card>
          <ErrorState description={getLoadErrorMessage(loadQuery.error)} onRetry={loadQuery.refetch} />
        </Card>
      </div>
    )
  }

  const load = loadQuery.data

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <Link to="/loads" className="mb-2 inline-flex items-center gap-1 text-body-md text-secondary hover:text-primary">
            <ArrowLeft className="h-4 w-4" strokeWidth={1.5} /> Back to My Loads
          </Link>
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-headline-lg text-on-surface">{load.referenceCode}</h1>
            <StatusBadge tone={getLoadStatusTone(load.status)}>{load.status}</StatusBadge>
          </div>
          <p className="mt-1 text-body-md text-on-surface-variant">
            {load.pickupAddress} → {load.dropoffAddress}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {role === UserRole.AGENCY_STAFF && load.status === 'Posted' && (
            <Button
              variant="primary"
              onClick={() => setIsAcceptOpen(true)}
              className="inline-flex items-center gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white"
            >
              <CheckCircle2 className="h-4 w-4" />
              Accept Shipment
            </Button>
          )}
          {role === UserRole.AGENCY_STAFF && load.status === 'Matched' && (
            <Button
              as={Link}
              to={`/trips?dispatch=${load.loadId}`}
              variant="primary"
              className="inline-flex items-center gap-1.5"
            >
              <Truck className="h-4 w-4" />
              Dispatch Driver & Vehicle
            </Button>
          )}
          {(role === UserRole.SHIPPER || role === UserRole.ADMIN) && (load.status === 'Posted' || load.status === 'Matched') && (
            <Button
              as={Link}
              to={`/agent-workflows?loadId=${load.loadId}`}
              variant="primary"
              className="bg-primary hover:bg-primary/90 text-white"
            >
              <Sparkles className="h-4 w-4 text-amber-300" />
              {load.status === 'Matched' ? 'View AI Match' : 'Review AI Match'}
            </Button>
          )}
          {canEditLoad(role, load.status) && (
            <Button as={Link} to={`/loads/${load.loadId}/edit`} variant="secondary">
              Edit Details
            </Button>
          )}
          {canCancelLoad(role, load.status) && (
            <Button variant="status" status="red" onClick={() => setIsCancelOpen(true)}>
              Cancel Load
            </Button>
          )}
        </div>
      </div>

      {(load.status === 'Posted' || load.status === 'Matched') && (
        <div className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-primary/20 bg-gradient-to-r from-primary-fixed/40 via-surface-container-low to-surface-container p-4 shadow-soft">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary text-white shadow-sm">
              <Sparkles className="h-5 w-5 text-amber-300" />
            </div>
            <div>
              <h3 className="font-heading text-sm font-bold text-primary">
                {load.status === 'Matched'
                  ? 'AI Carrier Match Approved & Dispatched'
                  : 'AI Carrier Match Recommendation Ready'}
              </h3>
              <p className="text-xs text-on-surface-variant">
                {load.status === 'Matched'
                  ? 'An operational assignment has been proposed. View the 4-Agent LangGraph telemetry & carrier details.'
                  : 'Agent 3 has ranked carriers, calculated positioning ETA, and computed dynamic pricing. Review and approve the match.'}
              </p>
            </div>
          </div>
          <Button
            as={Link}
            to={`/agent-workflows?loadId=${load.loadId}`}
            variant="secondary"
            className="border-primary/30 text-primary hover:bg-primary hover:text-white transition-colors"
          >
            <span>{load.status === 'Matched' ? 'Open Match Console' : 'Review & Approve Match'}</span>
          </Button>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
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
              <div>
                <dt className="text-label-caps text-on-surface-variant">Pickup Window</dt>
                <dd className="text-data-mono text-on-surface">
                  {formatDateTime(load.pickupWindowStart)} – {formatDateTime(load.pickupWindowEnd)}
                </dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Estimated Price</dt>
                <dd className="text-data-mono text-on-surface">{formatCurrency(load.estimatedPrice)}</dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Created</dt>
                <dd className="text-data-mono text-on-surface">{formatDateTime(load.createdAt)}</dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Last Updated</dt>
                <dd className="text-data-mono text-on-surface">{formatDateTime(load.updatedAt)}</dd>
              </div>
            </dl>
          </Card>

          <RouteMapCard
            origin={load.pickupAddress}
            destination={load.dropoffAddress}
            originLat={load.pickupLat}
            originLng={load.pickupLng}
            destinationLat={load.dropoffLat}
            destinationLng={load.dropoffLng}
          />

          {role === UserRole.SHIPPER && <PriceEstimateCard loadId={load.loadId} />}

          <LoadFilesSection loadId={load.loadId} role={role} />
        </div>

        <div className="space-y-6 lg:col-span-1">
          <LoadStatusHistoryCard history={load.statusHistory} />

          <Card>
            <h3 className="mb-4 text-headline-md text-primary">Pickup</h3>
            <dl className="space-y-3">
              <div>
                <dt className="text-label-caps text-on-surface-variant">Address</dt>
                <dd className="text-body-md text-on-surface">{load.pickupAddress}</dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Coordinates</dt>
                <dd className="text-data-mono text-on-surface">
                  {load.pickupLat}, {load.pickupLng}
                </dd>
              </div>
            </dl>
          </Card>

          <Card>
            <h3 className="mb-4 text-headline-md text-primary">Dropoff</h3>
            <dl className="space-y-3">
              <div>
                <dt className="text-label-caps text-on-surface-variant">Address</dt>
                <dd className="text-body-md text-on-surface">{load.dropoffAddress}</dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Coordinates</dt>
                <dd className="text-data-mono text-on-surface">
                  {load.dropoffLat}, {load.dropoffLng}
                </dd>
              </div>
            </dl>
          </Card>

          <Card>
            <h3 className="mb-4 text-headline-md text-primary">Record</h3>
            <dl className="space-y-3">
              <div>
                <dt className="text-label-caps text-on-surface-variant">Load ID</dt>
                <dd className="break-all text-data-mono text-on-surface">{load.loadId}</dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Shipper</dt>
                <dd className="text-body-md text-on-surface">{formatShipperName(load.shipperName, load.shipperUserId)}</dd>
              </div>
              <div>
                <dt className="text-label-caps text-on-surface-variant">Workflow Run</dt>
                <dd className="text-data-mono text-on-surface">{load.workflowRunId ?? 'Not yet assigned'}</dd>
              </div>
            </dl>
          </Card>
        </div>
      </div>

      {isCancelOpen && <CancelLoadDialog loadId={load.loadId} onClose={() => setIsCancelOpen(false)} />}
      {isAcceptOpen && (
        <AcceptShipmentDialog
          load={load}
          onClose={() => setIsAcceptOpen(false)}
          onAccepted={() => loadQuery.refetch()}
        />
      )}
    </div>
  )
}

export default LoadDetailPage
