import { PackagePlus, ArrowLeftRight, CheckCircle2 } from 'lucide-react'
import Card from '../../../components/Card.jsx'

// Marketing copy for the 3-step pitch — intentionally NOT wired to
// StatusBadge/src/lib/enums.js: "Matching" here is illustrative copy, not
// a real WorkflowStatus/AssignmentStatus value, so it stays a plain chip
// rather than borrowing the badge component's status-enum contract.
const STEPS = [
  {
    number: '01',
    icon: PackagePlus,
    iconBg: 'bg-status-blue-bg',
    iconColor: 'text-status-blue-text',
    title: 'Load Generation',
    description:
      'Shippers input cargo details, metric specifications (kg/CBM), and routing requirements. The system instantly generates standardized manifests.',
    detailLabel: 'INPUT REQUIRED',
    detail: <span className="text-data-mono text-primary">Volume, Weight, Route</span>,
  },
  {
    number: '02',
    icon: ArrowLeftRight,
    iconBg: 'bg-status-amber-bg',
    iconColor: 'text-status-amber-text',
    title: 'Algorithmic Matching',
    description:
      'Available agency fleets are cross-referenced against load requirements. AI-assisted pairing prioritizes route efficiency and agency reliability scores.',
    detailLabel: 'STATUS',
    detail: (
      <span className="rounded bg-status-amber-bg px-2 py-0.5 text-status-badge text-status-amber-text">
        Matching
      </span>
    ),
  },
  {
    number: '03',
    icon: CheckCircle2,
    iconBg: 'bg-status-green-bg',
    iconColor: 'text-status-green-text',
    title: 'Execution & ePOD',
    description:
      'Real-time GPS transit tracking culminates in Electronic Proof of Delivery. Immutable logs ensure transparent invoicing and immediate settlement initiation.',
    detailLabel: 'OUTPUT',
    detail: <span className="text-data-mono text-status-green-text">Verified ePOD</span>,
  },
]

function WorkflowSection() {
  return (
    <section id="how-it-works" className="mx-auto max-w-6xl px-container-margin py-20">
      <div className="mb-16 space-y-4 text-center">
        <h2 className="text-headline-lg text-primary">Operational Workflow</h2>
        <p className="mx-auto max-w-2xl text-body-lg text-secondary">
          A streamlined, deterministic process designed to eliminate friction between cargo posting and final
          delivery confirmation.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        {STEPS.map(({ number, icon: Icon, iconBg, iconColor, title, description, detailLabel, detail }, index) => (
          <Card
            key={number}
            className={`relative overflow-hidden ${index === 1 ? 'md:mt-8' : index === 2 ? 'md:mt-16' : ''}`}
          >
            <div className="absolute -right-4 -top-4 select-none text-9xl font-bold text-surface-container">
              {number}
            </div>
            <div className="relative z-10">
              <div className={`mb-6 flex h-12 w-12 items-center justify-center rounded ${iconBg}`}>
                <Icon className={`h-6 w-6 ${iconColor}`} strokeWidth={1.5} />
              </div>
              <h3 className="mb-3 text-headline-md text-primary">{title}</h3>
              <p className="text-body-md text-secondary">{description}</p>
              <div className="mt-6 flex items-center justify-between border-t border-slate-border pt-4">
                <span className="text-label-caps text-secondary">{detailLabel}</span>
                {detail}
              </div>
            </div>
          </Card>
        ))}
      </div>
    </section>
  )
}

export default WorkflowSection
