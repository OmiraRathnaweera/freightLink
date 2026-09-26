import { useParams, Link } from 'react-router-dom'
import { XCircle, ArrowLeft, RotateCcw } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'

export default function PaymentCancelledPage() {
  const { id } = useParams()

  return (
    <div className="flex min-h-[70vh] items-center justify-center p-4">
      <Card className="max-w-md w-full text-center p-8 space-y-6 shadow-xl border-amber-200">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-amber-100 text-amber-600 shadow-xs">
          <XCircle className="h-10 w-10" />
        </div>

        <div className="space-y-2">
          <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-mono font-bold text-amber-800">
            PayHere Checkout Cancelled
          </span>
          <h1 className="text-headline-md text-on-surface">Payment Cancelled</h1>
          <p className="text-sm text-on-surface-variant leading-relaxed">
            The checkout session was cancelled. No charges were processed against your account, and the invoice remains unpaid.
          </p>
        </div>

        {id && (
          <div className="rounded-lg bg-slate-50 border border-slate-200 p-3.5 text-xs font-mono text-slate-600">
            <span className="block text-slate-400 text-[10px] uppercase font-sans font-semibold mb-1">
              Invoice Reference
            </span>
            <span className="font-bold text-slate-900">{id}</span>
          </div>
        )}

        <div className="flex flex-col gap-2 pt-2">
          <Button as={Link} to="/billing" variant="primary" className="w-full">
            <ArrowLeft className="h-4 w-4 mr-1.5" />
            Back to Billing & Invoices
          </Button>
        </div>
      </Card>
    </div>
  )
}
