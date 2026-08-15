// Timestamp/Actor/Action/Detail log table, per DESIGN.md > Data Tables
// (striped rows, Archivo uppercase headers) — used across the Matched/
// Needs-Review/In-Transit Load Detail states.
function ActivityLog({ entries }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-body-md">
        <thead>
          <tr className="text-label-caps text-on-surface-variant">
            <th className="py-table-cell-py pr-table-cell-px font-normal">Timestamp</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Actor</th>
            <th className="py-table-cell-py pr-table-cell-px font-normal">Action</th>
            <th className="py-table-cell-py font-normal">Detail</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry, index) => (
            <tr key={`${entry.timestamp}-${index}`} className={index % 2 === 1 ? 'bg-slate-50' : undefined}>
              <td className="py-table-cell-py pr-table-cell-px text-data-mono text-on-surface-variant">{entry.timestamp}</td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface">{entry.actor}</td>
              <td className="py-table-cell-py pr-table-cell-px text-on-surface">{entry.action}</td>
              <td className="py-table-cell-py text-on-surface-variant">{entry.detail}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default ActivityLog
