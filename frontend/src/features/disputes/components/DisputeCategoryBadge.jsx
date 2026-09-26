import { AlertOctagon, Clock, CreditCard, HelpCircle } from 'lucide-react'
import { cx } from '../../../lib/cx.js'

export default function DisputeCategoryBadge({ category, className }) {
  const getBadgeStyle = () => {
    switch (category) {
      case 'Damage':
        return {
          icon: AlertOctagon,
          className: 'bg-rose-50 text-rose-700 border border-rose-200',
        }
      case 'Delay':
        return {
          icon: Clock,
          className: 'bg-purple-50 text-purple-700 border border-purple-200',
        }
      case 'Payment Issue':
      case 'Billing':
        return {
          icon: CreditCard,
          className: 'bg-emerald-50 text-emerald-700 border border-emerald-200',
        }
      default:
        return {
          icon: HelpCircle,
          className: 'bg-slate-100 text-slate-700 border border-slate-200',
        }
    }
  }

  const { icon: Icon, className: badgeClasses } = getBadgeStyle()

  return (
    <span
      className={cx(
        'inline-flex items-center gap-1.5 rounded px-2.5 py-1 text-xs font-semibold tracking-wide',
        badgeClasses,
        className,
      )}
    >
      <Icon className="h-3.5 w-3.5 shrink-0" strokeWidth={2} />
      <span>{category}</span>
    </span>
  )
}
