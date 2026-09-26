import { AlertTriangle, Clock, CheckCircle2 } from 'lucide-react'
import StatusBadge from '../../../components/StatusBadge.jsx'
import { DisputeStatus, getStatusTone, getStatusLabel } from '../lib/disputeRules.js'

export default function DisputeStatusBadge({ status, className }) {
  const tone = getStatusTone(status)
  const label = getStatusLabel(status)

  const renderIcon = () => {
    switch (status) {
      case DisputeStatus.RAISED:
        return <AlertTriangle className="mr-1.5 h-3.5 w-3.5 shrink-0" strokeWidth={2} />
      case DisputeStatus.UNDER_REVIEW:
        return <Clock className="mr-1.5 h-3.5 w-3.5 shrink-0" strokeWidth={2} />
      case DisputeStatus.RESOLVED:
        return <CheckCircle2 className="mr-1.5 h-3.5 w-3.5 shrink-0" strokeWidth={2} />
      default:
        return null
    }
  }

  return (
    <StatusBadge tone={tone} className={className}>
      <span className="inline-flex items-center">
        {renderIcon()}
        {label}
      </span>
    </StatusBadge>
  )
}
