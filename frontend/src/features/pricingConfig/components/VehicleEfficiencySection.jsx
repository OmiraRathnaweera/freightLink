import { useState } from 'react'
import { toast } from 'sonner'
import { History, Trash2, Truck } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import { VehicleClass } from '../../../lib/enums.js'
import { cx } from '../../../lib/cx.js'
import {
  useVehicleEfficiencyQuery,
  useVehicleEfficiencyHistoryQuery,
  useDeleteVehicleEfficiencyMutation,
} from '../api/pricingConfigApi.js'
import { getPricingRowTone, getPricingRowLabel } from '../lib/statusTone.js'
import { getPricingConfigErrorMessage } from '../lib/errorMessages.js'
import { formatDateTime } from '../lib/format.js'
import { useConfirmDelete } from '../hooks/useConfirmDelete.js'
import VehicleEfficiencyForm from './VehicleEfficiencyForm.jsx'

function withCurrentFlag(rows) {
  const currentIndex = rows.findIndex((row) => !row.deletedAt)
  return rows.map((row, index) => ({ ...row, isCurrent: index === currentIndex }))
}

function formatPayloadBand(minPayloadKg, maxPayloadKg) {
  if (maxPayloadKg == null) return `${minPayloadKg.toLocaleString('en-LK')}+ kg`
  return `${minPayloadKg.toLocaleString('en-LK')}–${maxPayloadKg.toLocaleString('en-LK')} kg`
}

function formatVolumeBand(minVolumeM3, maxVolumeM3) {
  if (maxVolumeM3 == null) return `${minVolumeM3.toLocaleString('en-LK')}+ m³`
  return `${minVolumeM3.toLocaleString('en-LK')}–${maxVolumeM3.toLocaleString('en-LK')} m³`
}

function VehicleEfficiencySection() {
  const [showHistory, setShowHistory] = useState(false)
  const [historyVehicleClass, setHistoryVehicleClass] = useState('')
  const { pendingId, requestDelete, cancel } = useConfirmDelete()

  const efficiencyQuery = useVehicleEfficiencyQuery()
  const historyQuery = useVehicleEfficiencyHistoryQuery(historyVehicleClass, {
    enabled: showHistory && Boolean(historyVehicleClass),
  })
  const deleteMutation = useDeleteVehicleEfficiencyMutation()

  async function handleConfirmDelete(id) {
    try {
      await deleteMutation.mutateAsync(id)
      toast.success('Efficiency tier deleted')
      cancel()
    } catch (error) {
      toast.error(getPricingConfigErrorMessage(error))
    }
  }

  const historyRows = historyQuery.data ? withCurrentFlag(historyQuery.data) : []

  return (
    <Card>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-headline-md text-primary">Vehicle Class Efficiency</h3>
        <Button
          variant="secondary"
          onClick={() => setShowHistory((prev) => !prev)}
          className="inline-flex items-center gap-2"
        >
          <History className="h-4 w-4" strokeWidth={1.5} />
          {showHistory ? 'Hide History' : 'View History'}
        </Button>
      </div>

      {efficiencyQuery.isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 3 }).map((_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </div>
      ) : efficiencyQuery.isError ? (
        <ErrorState
          description={getPricingConfigErrorMessage(efficiencyQuery.error)}
          onRetry={efficiencyQuery.refetch}
        />
      ) : efficiencyQuery.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-body-md">
            <thead>
              <tr className="text-label-caps text-on-surface-variant">
                <th className="py-table-cell-py pr-table-cell-px font-normal">Vehicle Class</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Payload Band</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Volume Band</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Fuel Consumption</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Source</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Effective From</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Status</th>
                <th className="py-table-cell-py font-normal" aria-hidden="true" />
              </tr>
            </thead>
            <tbody>
              {efficiencyQuery.data.map((tier, index) => (
                <tr key={tier.vehicleClassEfficiencyId} className={cx('group', index % 2 === 1 && 'bg-slate-50')}>
                  <td className="py-table-cell-py pr-table-cell-px text-on-surface">{tier.classLabel}</td>
                  <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                    {formatPayloadBand(tier.minPayloadKg, tier.maxPayloadKg)}
                  </td>
                  <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                    {formatVolumeBand(tier.minVolumeM3, tier.maxVolumeM3)}
                  </td>
                  <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                    {tier.fuelConsumptionLPer100Km} L/100km
                  </td>
                  <td
                    className="max-w-xs truncate py-table-cell-py pr-table-cell-px text-on-surface"
                    title={tier.source}
                  >
                    {tier.source}
                  </td>
                  <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                    {formatDateTime(tier.effectiveFrom)}
                  </td>
                  <td className="py-table-cell-py pr-table-cell-px">
                    <StatusBadge tone="green">Current</StatusBadge>
                  </td>
                  <td className="py-table-cell-py text-right">
                    {pendingId === tier.vehicleClassEfficiencyId ? (
                      <div className="flex items-center justify-end gap-2">
                        <Button variant="secondary" onClick={cancel}>
                          Cancel
                        </Button>
                        <Button
                          variant="status"
                          status="red"
                          onClick={() => handleConfirmDelete(tier.vehicleClassEfficiencyId)}
                          disabled={deleteMutation.isPending}
                        >
                          Confirm Delete
                        </Button>
                      </div>
                    ) : (
                      <button
                        type="button"
                        onClick={() => requestDelete(tier.vehicleClassEfficiencyId)}
                        aria-label={`Delete efficiency tier for ${tier.classLabel}`}
                        className="inline-flex h-8 w-8 items-center justify-center rounded text-on-surface-variant transition-colors hover:bg-slate-100 hover:text-status-red-text"
                      >
                        <Trash2 className="h-4 w-4" strokeWidth={1.5} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <EmptyState icon={Truck} title="No efficiency tiers yet" description="Add the first tier below." />
      )}

      {showHistory && (
        <div className="mt-6 border-t border-slate-border pt-6">
          <div className="mb-4">
            <label htmlFor="vehicle-history-class" className="mb-1 block text-label-caps text-on-surface-variant">
              View history for
            </label>
            <select
              id="vehicle-history-class"
              value={historyVehicleClass}
              onChange={(event) => setHistoryVehicleClass(event.target.value)}
              className="w-full max-w-xs rounded-md border border-slate-300 bg-white px-3 py-2 text-[15px] text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border"
            >
              <option value="">Select vehicle class…</option>
              {Object.values(VehicleClass).map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
          </div>

          {!historyVehicleClass ? null : historyQuery.isLoading ? (
            <Skeleton className="h-10 w-full" />
          ) : historyQuery.isError ? (
            <ErrorState description={getPricingConfigErrorMessage(historyQuery.error)} onRetry={historyQuery.refetch} />
          ) : historyRows.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-body-md">
                <thead>
                  <tr className="text-label-caps text-on-surface-variant">
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Payload Band</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Volume Band</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Fuel Consumption</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Source</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Effective From</th>
                    <th className="py-table-cell-py font-normal">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {historyRows.map((tier, index) => (
                    <tr
                      key={tier.vehicleClassEfficiencyId}
                      className={cx('group', index % 2 === 1 && 'bg-slate-50', tier.deletedAt && 'opacity-70')}
                    >
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatPayloadBand(tier.minPayloadKg, tier.maxPayloadKg)}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatVolumeBand(tier.minVolumeM3, tier.maxVolumeM3)}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {tier.fuelConsumptionLPer100Km} L/100km
                      </td>
                      <td
                        className="max-w-xs truncate py-table-cell-py pr-table-cell-px text-on-surface"
                        title={tier.source}
                      >
                        {tier.source}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatDateTime(tier.effectiveFrom)}
                      </td>
                      <td className="py-table-cell-py">
                        <StatusBadge tone={getPricingRowTone(tier)}>{getPricingRowLabel(tier)}</StatusBadge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-body-md text-on-surface-variant">No history recorded for this vehicle class yet.</p>
          )}
        </div>
      )}

      <div className="mt-6 border-t border-slate-border pt-6">
        <VehicleEfficiencyForm />
      </div>
    </Card>
  )
}

export default VehicleEfficiencySection
