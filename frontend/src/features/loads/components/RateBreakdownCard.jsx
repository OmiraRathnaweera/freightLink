import Card from '../../../components/Card.jsx'

function formatLkr(amount) {
  return `LKR ${amount.toLocaleString('en-LK', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
}

// Right-column cost card reused by Post a Load ("Live Estimate") and Edit
// Load ("Estimated Cost Breakdown") — same shape, different title/data.
function RateBreakdownCard({ title, distanceKm, lineItems, total, note }) {
  return (
    <Card>
      <h3 className="mb-4 text-headline-md text-primary">{title}</h3>
      {distanceKm != null && (
        <div className="mb-3 flex items-center justify-between border-b border-slate-border pb-3 text-body-md">
          <span className="text-on-surface-variant">Distance</span>
          <span className="text-data-mono text-on-surface">{distanceKm} km</span>
        </div>
      )}
      <div className="space-y-2">
        {lineItems.map((item) => (
          <div key={item.label} className="flex items-center justify-between text-body-md">
            <span className="text-on-surface-variant">{item.label}</span>
            <span className="text-data-mono text-on-surface">{formatLkr(item.amount)}</span>
          </div>
        ))}
      </div>
      <div className="mt-4 flex items-center justify-between border-t border-slate-border pt-4">
        <span className="text-headline-md text-primary">Total</span>
        <span className="text-data-mono text-lg text-primary">{formatLkr(total)}</span>
      </div>
      {note && <p className="mt-3 text-body-md text-on-surface-variant">{note}</p>}
    </Card>
  )
}

export default RateBreakdownCard
