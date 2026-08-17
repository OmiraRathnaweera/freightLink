import { Link } from 'react-router-dom'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { cx } from '../../../lib/cx.js'
import { getLoadStatusTone } from '../lib/statusTone.js'
import { formatCurrency, formatDateTime, formatWeight } from '../lib/format.js'
import RowActionsMenu from './RowActionsMenu.jsx'

// Header cell for the three columns the backend can sort on
// (createdAt/pickupWindowStart/weightKg — docs/load-management-api.md
// Section 3.3). Sorting is server-driven: clicking calls onSortChange and
// LoadsPage re-issues GET /loads with the new sortBy/sortDir.
function SortableHeader({ column, label, sortBy, sortDir, onSortChange }) {
  const active = sortBy === column
  const Icon = active ? (sortDir === 'asc' ? ArrowUp : ArrowDown) : ArrowUpDown

  return (
    <th className="py-table-cell-py pr-table-cell-px font-normal">
      <button
        type="button"
        onClick={() => onSortChange(column)}
        className={cx(
          'inline-flex items-center gap-1 text-label-caps transition-colors',
          active ? 'text-on-surface' : 'text-on-surface-variant hover:text-on-surface',
        )}
      >
        {label}
        <Icon className="h-3.5 w-3.5" strokeWidth={1.5} />
      </button>
    </th>
  )
}

// Load Control data table — a feature component since its columns
// (LoadListItemDto, docs/load-management-api.md Section 3.3) and
// StatusBadge-tone mapping are specific to the Loads domain
// (.claude/rules/frontend-design.md #1).
function LoadsTable({ loads, sortBy, sortDir, onSortChange }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-body-md">
        <thead>
          <tr className="text-label-caps text-on-surface-variant">
            <th className="py-table-cell-py pr-table-cell-px font-normal">Reference</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Cargo</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Route</th>
            <SortableHeader column="weightKg" label="Weight" sortBy={sortBy} sortDir={sortDir} onSortChange={onSortChange} />
            <SortableHeader
              column="pickupWindowStart"
              label="Pickup Window"
              sortBy={sortBy}
              sortDir={sortDir}
              onSortChange={onSortChange}
            />
            <th className="py-table-cell-py pr-table-cell-px font-normal">Est. Price</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Status</th>
            <SortableHeader column="createdAt" label="Created" sortBy={sortBy} sortDir={sortDir} onSortChange={onSortChange} />
            <th className="py-table-cell-py font-normal" aria-hidden="true" />
          </tr>
        </thead>
        <tbody>
          {loads.map((load, index) => (
            <tr key={load.loadId} className={cx('group', index % 2 === 1 && 'bg-slate-50')}>
              <td className="py-table-cell-py pr-table-cell-px">
                <Link to={`/loads/${load.loadId}`} className="text-data-mono text-primary hover:underline">
                  {load.referenceCode}
                </Link>
              </td>
              <td className="max-w-xs truncate py-table-cell-py pr-table-cell-px text-on-surface" title={load.cargoDescription}>
                {load.cargoDescription}
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface">
                {load.pickupAddress} → {load.dropoffAddress}
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">{formatWeight(load.weightKg)}</td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface-variant">
                {formatDateTime(load.pickupWindowStart)} – {formatDateTime(load.pickupWindowEnd)}
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface">
                {formatCurrency(load.estimatedPrice)}
              </td>
              <td className="py-table-cell-py pr-table-cell-px">
                <StatusBadge tone={getLoadStatusTone(load.status)}>{load.status}</StatusBadge>
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface-variant">{formatDateTime(load.createdAt)}</td>
              <td className="py-table-cell-py text-right">
                <RowActionsMenu loadId={load.loadId} status={load.status} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default LoadsTable
