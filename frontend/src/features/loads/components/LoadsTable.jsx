import { Link } from 'react-router-dom'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { cx } from '../../../lib/cx.js'
import { LoadStatus } from '../../../lib/enums.js'
import { getLoadStatusTone } from '../lib/statusTone.js'
import { formatDate, formatWeight } from '../lib/format.js'
import RowActionsMenu from './RowActionsMenu.jsx'

// My Loads dashboard table — a feature component since its columns (Load
// ID/Route/Weight/Status/Posted Date) and StatusBadge-tone mapping are
// specific to the Loads domain (.claude/rules/frontend-design.md #1).
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
                  <StatusBadge tone={getLoadStatusTone(load.status)} pill={load.status === LoadStatus.IN_TRANSIT}>
                    {load.status}
                  </StatusBadge>
                  {load.attempt && <span className="text-label-caps text-on-surface-variant">{load.attempt}</span>}
                </div>
              </td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface-variant">{formatDate(load.postedDate)}</td>
              <td className="py-table-cell-py text-right">
                <RowActionsMenu loadId={load.id} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default LoadsTable
