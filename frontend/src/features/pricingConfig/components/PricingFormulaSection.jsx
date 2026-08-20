import { useState } from 'react'
import { toast } from 'sonner'
import { Calculator, History, Trash2 } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import ErrorState from '../../../components/ErrorState.jsx'
import Skeleton from '../../../components/Skeleton.jsx'
import { cx } from '../../../lib/cx.js'
import {
  useCurrentFormulaConfigQuery,
  useFormulaConfigHistoryQuery,
  useDeleteFormulaConfigMutation,
} from '../api/pricingConfigApi.js'
import { getPricingRowTone, getPricingRowLabel } from '../lib/statusTone.js'
import { getPricingConfigErrorMessage } from '../lib/errorMessages.js'
import { formatCurrency, formatDateTime, formatPercent } from '../lib/format.js'
import { useConfirmDelete } from '../hooks/useConfirmDelete.js'
import PricingFormulaForm from './PricingFormulaForm.jsx'

// History rows come back newest-EffectiveFrom-first (backend contract) —
// the first non-deleted row in that order is the "current" one.
function withCurrentFlag(rows) {
  const currentIndex = rows.findIndex((row) => !row.deletedAt)
  return rows.map((row, index) => ({ ...row, isCurrent: index === currentIndex }))
}

// Unlike FuelRateSection/VehicleEfficiencySection, PricingFormulaConfig has
// no dimension key (no fuel type / vehicle class) — there's only ever one
// current row total, so this renders a single detail panel instead of a
// "list of current rows across all keys" table, and history needs no
// type/class selector.
function PricingFormulaSection() {
  const [showHistory, setShowHistory] = useState(false)
  const { pendingId, requestDelete, cancel } = useConfirmDelete()

  const currentQuery = useCurrentFormulaConfigQuery()
  const historyQuery = useFormulaConfigHistoryQuery({ enabled: showHistory })
  const deleteMutation = useDeleteFormulaConfigMutation()

  const isMissing = currentQuery.isError && currentQuery.error?.code === 'PRICING_CONFIG_MISSING'

  async function handleConfirmDelete(id) {
    try {
      await deleteMutation.mutateAsync(id)
      toast.success('Pricing formula configuration deleted')
      cancel()
    } catch (error) {
      toast.error(getPricingConfigErrorMessage(error))
    }
  }

  const historyRows = historyQuery.data ? withCurrentFlag(historyQuery.data) : []

  return (
    <Card>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-headline-md text-primary">Pricing Formula</h3>
        <Button
          variant="secondary"
          onClick={() => setShowHistory((prev) => !prev)}
          className="inline-flex items-center gap-2"
        >
          <History className="h-4 w-4" strokeWidth={1.5} />
          {showHistory ? 'Hide History' : 'View History'}
        </Button>
      </div>

      {currentQuery.isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 3 }).map((_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </div>
      ) : isMissing ? (
        <EmptyState
          icon={Calculator}
          title="No pricing formula configured yet"
          description="Add the first configuration below."
        />
      ) : currentQuery.isError ? (
        <ErrorState description={getPricingConfigErrorMessage(currentQuery.error)} onRetry={currentQuery.refetch} />
      ) : (
        <div>
          <div className="mb-3 flex items-center justify-between gap-2">
            <StatusBadge tone="green">Current</StatusBadge>
            {pendingId === currentQuery.data.pricingFormulaConfigId ? (
              <div className="flex items-center gap-2">
                <Button variant="secondary" onClick={cancel}>
                  Cancel
                </Button>
                <Button
                  variant="status"
                  status="red"
                  onClick={() => handleConfirmDelete(currentQuery.data.pricingFormulaConfigId)}
                  disabled={deleteMutation.isPending}
                >
                  Confirm Delete
                </Button>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => requestDelete(currentQuery.data.pricingFormulaConfigId)}
                aria-label="Delete current pricing formula configuration"
                className="inline-flex h-8 w-8 items-center justify-center rounded text-on-surface-variant transition-colors hover:bg-slate-100 hover:text-status-red-text"
              >
                <Trash2 className="h-4 w-4" strokeWidth={1.5} />
              </button>
            )}
          </div>
          <dl className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div>
              <dt className="text-label-caps text-on-surface-variant">Base Fare</dt>
              <dd className="text-data-mono text-on-surface">{formatCurrency(currentQuery.data.baseFare)}</dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Rate per Kg</dt>
              <dd className="text-data-mono text-on-surface">{formatCurrency(currentQuery.data.ratePerKg)}</dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Driver Cost per Km</dt>
              <dd className="text-data-mono text-on-surface">{formatCurrency(currentQuery.data.driverCostPerKm)}</dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Maintenance Allowance per Km</dt>
              <dd className="text-data-mono text-on-surface">
                {formatCurrency(currentQuery.data.maintenanceAllowancePerKm)}
              </dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Margin</dt>
              <dd className="text-data-mono text-on-surface">{formatPercent(currentQuery.data.marginPercent)}</dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Effective From</dt>
              <dd className="text-data-mono text-on-surface">{formatDateTime(currentQuery.data.effectiveFrom)}</dd>
            </div>
            <div className="sm:col-span-2">
              <dt className="text-label-caps text-on-surface-variant">Source</dt>
              <dd className="text-on-surface">{currentQuery.data.source}</dd>
            </div>
            <div>
              <dt className="text-label-caps text-on-surface-variant">Set By</dt>
              <dd className="text-on-surface">{currentQuery.data.setByUserName}</dd>
            </div>
          </dl>
        </div>
      )}

      {showHistory && (
        <div className="mt-6 border-t border-slate-border pt-6">
          {historyQuery.isLoading ? (
            <Skeleton className="h-10 w-full" />
          ) : historyQuery.isError ? (
            <ErrorState description={getPricingConfigErrorMessage(historyQuery.error)} onRetry={historyQuery.refetch} />
          ) : historyRows.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-body-md">
                <thead>
                  <tr className="text-label-caps text-on-surface-variant">
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Base Fare</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Rate/Kg</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Driver Cost/Km</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Maintenance/Km</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Margin</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Source</th>
                    <th className="py-table-cell-py pr-table-cell-px font-normal">Effective From</th>
                    <th className="py-table-cell-py font-normal">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {historyRows.map((config, index) => (
                    <tr
                      key={config.pricingFormulaConfigId}
                      className={cx('group', index % 2 === 1 && 'bg-slate-50', config.deletedAt && 'opacity-70')}
                    >
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatCurrency(config.baseFare)}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatCurrency(config.ratePerKg)}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatCurrency(config.driverCostPerKm)}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatCurrency(config.maintenanceAllowancePerKm)}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatPercent(config.marginPercent)}
                      </td>
                      <td
                        className="max-w-xs truncate py-table-cell-py pr-table-cell-px text-on-surface"
                        title={config.source}
                      >
                        {config.source}
                      </td>
                      <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                        {formatDateTime(config.effectiveFrom)}
                      </td>
                      <td className="py-table-cell-py">
                        <StatusBadge tone={getPricingRowTone(config)}>{getPricingRowLabel(config)}</StatusBadge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-body-md text-on-surface-variant">No history recorded yet.</p>
          )}
        </div>
      )}

      <div className="mt-6 border-t border-slate-border pt-6">
        <PricingFormulaForm />
      </div>
    </Card>
  )
}

export default PricingFormulaSection
