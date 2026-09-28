import { Calculator, RotateCcw } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import { useEstimateLoadPriceMutation } from '../api/loadsApi.js'
import { formatCurrency } from '../lib/format.js'
import { getLoadErrorMessage } from '../lib/errorMessages.js'

// POST /loads/{id}/estimate â€” a rough, pre-matching quote from the load's own
// pickup/dropoff coordinates (haversine distance, no external routing call).
// Deliberately separate from the "Estimated Price" field on the Cargo
// Specifications card: that field is Agent 3's own computed price once
// matching has run, while this is the shipper's own quick, on-demand quote
// and is never persisted onto the load.
function PriceEstimateCard({ loadId }) {
  const estimateMutation = useEstimateLoadPriceMutation(loadId)

  return (
    <Card>
      <div className="flex items-center justify-between gap-3">
        <h3 className="text-headline-md text-primary">Quick Price Estimate</h3>
        <Button
          variant="secondary"
          onClick={() => estimateMutation.mutate()}
          disabled={estimateMutation.isPending}
        >
          <Calculator className="h-4 w-4" />
          {estimateMutation.isPending ? (
            <>
              <RotateCcw className="h-4 w-4 animate-spin" /> Estimating...
            </>
          ) : (
            'Get Estimate'
          )}
        </Button>
      </div>
      <p className="mt-1 text-body-sm text-on-surface-variant">
        A rough, straight-line-distance quote you can request any time before matching. This is not saved anywhere.
      </p>

      {estimateMutation.isError && (
        <div className="mt-3 rounded-md border border-status-red-text bg-status-red-bg px-3 py-2 text-body-sm text-status-red-text">
          {getLoadErrorMessage(estimateMutation.error)}
        </div>
      )}

      {estimateMutation.isSuccess && (
        <dl className="mt-3 grid grid-cols-2 gap-3 rounded-md border border-slate-border bg-surface-container-low p-3 sm:grid-cols-4">
          <div>
            <dt className="text-label-caps text-on-surface-variant">Estimated Price</dt>
            <dd className="text-data-mono text-on-surface">{formatCurrency(estimateMutation.data.estimatedPrice)}</dd>
          </div>
          <div>
            <dt className="text-label-caps text-on-surface-variant">Distance</dt>
            <dd className="text-data-mono text-on-surface">{estimateMutation.data.distanceKm} km</dd>
          </div>
          <div>
            <dt className="text-label-caps text-on-surface-variant">Rate / km</dt>
            <dd className="text-data-mono text-on-surface">{formatCurrency(estimateMutation.data.ratePerKm)}</dd>
          </div>
          <div>
            <dt className="text-label-caps text-on-surface-variant">Rate / kg</dt>
            <dd className="text-data-mono text-on-surface">{formatCurrency(estimateMutation.data.ratePerKg)}</dd>
          </div>
        </dl>
      )}
    </Card>
  )
}

export default PriceEstimateCard
