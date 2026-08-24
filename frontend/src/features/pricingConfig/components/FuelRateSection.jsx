import { useState } from 'react'
import { toast } from 'sonner'
import { Fuel, History, Trash2 } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import { FuelType } from '../../../lib/enums.js'
import { cx } from '../../../lib/cx.js'
import {
  useFuelRatesQuery,
  useFuelRateHistoryQuery,
  useDeleteFuelRateMutation,
} from '../api/pricingConfigApi.js'
import { getPricingRowTone, getPricingRowLabel } from '../lib/statusTone.js'
import { getPricingConfigErrorMessage } from '../lib/errorMessages.js'
import { formatCurrency, formatDateTime } from '../lib/format.js'
import { useConfirmDelete } from '../hooks/useConfirmDelete.js'
import FuelRateForm from './FuelRateForm.jsx'

// History rows come back newest-EffectiveFrom-first (backend contract) —
// the first non-deleted row in that order is the "current" one, everything
// else non-deleted is "superseded", and a DeletedAt row is "deleted".
function withCurrentFlag(rows) {
  const currentIndex = rows.findIndex((row) => !row.deletedAt)
  return rows.map((row, index) => ({ ...row, isCurrent: index === currentIndex }))
}

function FuelRateSection() {
  const [showHistory, setShowHistory] = useState(false)
  const [historyFuelType, setHistoryFuelType] = useState('')
  const { pendingId, requestDelete, cancel } = useConfirmDelete()

  const ratesQuery = useFuelRatesQuery()
  const historyQuery = useFuelRateHistoryQuery(historyFuelType, { enabled: showHistory && Boolean(historyFuelType) })
  const deleteMutation = useDeleteFuelRateMutation()

  async function handleConfirmDelete(id) {
    try {
      await deleteMutation.mutateAsync(id)
      toast.success('Fuel rate deleted')
      cancel()
    } catch (error) {
      toast.error(getPricingConfigErrorMessage(error))
    }
  }

  const historyRows = historyQuery.data ? withCurrentFlag(historyQuery.data) : []

  return (
    <Card>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-headline-md text-primary">Fuel Prices</h3>
        <Button
          variant="secondary"
          onClick={() => setShowHistory((prev) => !prev)}
          className="inline-flex items-center gap-2"
        >
          <History className="h-4 w-4" strokeWidth={1.5} />
          {showHistory ? 'Hide History' : 'View History'}
        </Button>
      </div>

      {ratesQuery.isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 3 }).map((_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </div>
      ) : ratesQuery.isError ? (
        <ErrorState description={getPricingConfigErrorMessage(ratesQuery.error)} onRetry={ratesQuery.refetch} />
      ) : ratesQuery.data.length > 0 ? (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-body-md">
            <thead>
              <tr className="text-label-caps text-on-surface-variant">
                <th className="py-table-cell-py pr-table-cell-px font-normal">Fuel Type</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Price / Litre</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Source</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Effective From</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Set By</th>
                <th className="py-table-cell-py pr-table-cell-px font-normal">Status</th>
                <th className="py-table-cell-py font-normal" aria-hidden="true" />
              </tr>
            </thead>
            <tbody>
              {ratesQuery.data.map((rate, index) => (
                <tr key={rate.fuelPriceRateId} className={cx('group', index % 2 === 1 && 'bg-slate-50')}>
                  <td className="py-table-cell-py pr-table-cell-px text-on-surface">{rate.fuelType}</td>
                  <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                    {formatCurrency(rate.pricePerLitre)}
                  </td>
                  <td className="max-w-xs truncate py-table-cell-py pr-table-cell-px text-on-surface" title={rate.source}>
                    {rate.source}
                  </td>
                  <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                    {formatDateTime(rate.effectiveFrom)}
                  </td>
                  <td className="py-table-cell-py pr-table-cell-px text-on-surface">{rate.setByUserName}</td>
                  <td className="py-table-cell-py pr-table-cell-px">
                    <StatusBadge tone="green">Current</StatusBadge>
                  </td>
                  <td className="py-table-cell-py text-right">
                    {pendingId === rate.fuelPriceRateId ? (
                      <div className="flex items-center justify-end gap-2">
                        <Button variant="secondary" onClick={cancel}>
                          Cancel
                        </Button>
                        <Button
                          variant="status"
                          status="red"
                          onClick={() => handleConfirmDelete(rate.fuelPriceRateId)}
                          disabled={deleteMutation.isPending}
                        >
                          Confirm Delete
                        </Button>
                      </div>
                    ) : (
                      <button
                        type="button"
                        onClick={() => requestDelete(rate.fuelPriceRateId)}
                        aria-label={`Delete fuel rate for ${rate.fuelType}`}
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
        <EmptyState icon={Fuel} title="No fuel rates yet" description="Add the first fuel rate below." />
      )}

      {showHistory && (
        <div className="mt-6 border-t border-slate-border pt-6">
          <div className="mb-4">
            <label htmlFor="fuel-history-type" className="mb-1 block text-label-caps text-on-surface-variant">
              View history for
            </label>
            <select
              id="fuel-history-type"
              value={historyFuelType}
              onChange={(event) => setHistoryFuelType(event.target.value)}
              className="w-full max-w-xs rounded-md border border-slate-300 bg-white px-3 py-2 text-[15px] text-on-surface focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border"
            >
              <option value="">Select fuel type…</option>
              {Object.values(FuelType).map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
          </div>

          {!historyFuelType ? null : historyQuery.isLoading ? (
            <Skeleton className="h-10 w-full" />
          ) : historyQuery.isError ? (
            <ErrorState description={getPricingConfigErrorMessage(historyQuery.error)} onRetry={historyQuery.refetch} />
          ) : historyRows.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-body-md">
                <thead>
                  <tr className="text-label-caps text-on-surface-variant">
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Price / Litre</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Source</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Effective From</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Set By</th>
                    <th className="py-table-cell-py font-normal">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {historyRows.map((rate, index) => (
                    <tr
                      key={rate.fuelPriceRateId}
                      className={cx('group', index % 2 === 1 && 'bg-slate-50', rate.deletedAt && 'opacity-70')}
                    >
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatCurrency(rate.pricePerLitre)}
                      </td>
                      <td
                        className="max-w-xs truncate py-table-cell-py pr-table-cell-px text-on-surface"
                        title={rate.source}
                      >
                        {rate.source}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatDateTime(rate.effectiveFrom)}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-on-surface">{rate.setByUserName}</td>
                      <td className="py-table-cell-py">
                        <StatusBadge tone={getPricingRowTone(rate)}>{getPricingRowLabel(rate)}</StatusBadge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-body-md text-on-surface-variant">No history recorded for this fuel type yet.</p>
          )}
        </div>
      )}

      <div className="mt-6 border-t border-slate-border pt-6">
        <FuelRateForm />
      </div>
    </Card>
  )
}

export default FuelRateSection
