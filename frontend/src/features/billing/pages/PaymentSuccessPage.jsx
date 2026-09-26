import { useParams, Link } from 'react-router-dom'
import { CheckCircle2, ArrowRight, FileText, ShieldCheck } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'

export default function PaymentSuccessPage() {
  const { id } = useParams()

  return (
    <div className="flex min-h-[70vh] items-center justify-center p-4">
      <Card className="max-w-md w-full text-center p-8 space-y-6 shadow-xl border-emerald-200">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-emerald-100 text-emerald-600 shadow-xs">
          <CheckCircle2 className="h-10 w-10" />
        </div>

        <div className="space-y-2">
          <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-mono font-bold text-emerald-800">
            PayHere Sandbox Test
          </span>
          <h1 className="text-headline-md text-on-surface">Payment Submitted!</h1>
          <p className="text-sm text-on-surface-variant leading-relaxed">
            Your transaction was received by the PayHere payment gateway. Once the IPN webhook notification completes verification, your invoice status updates to <span className="font-semibold text-emerald-700">Paid</span>.
          </p>
        </div>

        {id && (
          <div className="rounded-lg bg-slate-50 border border-slate-200 p-3.5 text-xs font-mono text-slate-600">
            <span className="block text-slate-400 text-[10px] uppercase font-sans font-semibold mb-1">
              Order Reference
            </span>
            <span className="font-bold text-slate-900">{id}</span>
          </div>
        )}

        <div className="flex flex-col gap-2 pt-2">
          <Button as={Link} to="/billing" variant="primary" className="w-full">
            <FileText className="h-4 w-4 mr-1.5" />
            Return to Invoices & Billing
          </Button>
        </div>
      </Card>
    </div>
  )
}
