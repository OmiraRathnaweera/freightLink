import { useState } from 'react'
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { AlertCircle, ArrowLeft, Send } from 'lucide-react'

import Button from '../../../components/Button.jsx'
import Card from '../../../components/Card.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import Textarea from '../../../components/Textarea.jsx'
import { useAppSelector } from '../../../hooks/useAppSelector.js'
import { UserRole } from '../../../lib/enums.js'
import { useRaiseDisputeMutation } from '../api/disputesApi.js'

const CATEGORIES = ['Damage', 'Delay', 'Billing', 'Other']
const DUPLICATE_ERROR = 'DISPUTE_ALREADY_EXISTS_FOR_TRIP_AND_CATEGORY'

function getApiMessage(error) {
  const payload = error?.response?.data
  if (payload?.error?.code === DUPLICATE_ERROR || payload?.code === DUPLICATE_ERROR) {
    return 'You already have an active dispute in this category for this trip.'
  }
  return payload?.error?.message || payload?.message || error?.message || 'Unable to raise the dispute. Please try again.'
}

export default function RaiseDisputePage() {
  const role = useAppSelector((state) => state.auth?.role)
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const tripId = searchParams.get('tripId')
  const [category, setCategory] = useState('Damage')
  const [description, setDescription] = useState('')
  const [error, setError] = useState('')
  const raiseMutation = useRaiseDisputeMutation()

  if (role !== UserRole.SHIPPER && role !== UserRole.AGENCY_STAFF) {
    return <Navigate to="/unauthorized" replace />
  }

  const submit = async (event) => {
    event.preventDefault()
    const trimmed = description.trim()
    if (!tripId) {
      setError('A trip is required to raise a dispute. Open the relevant invoice and try again.')
      return
    }
    if (trimmed.length < 10 || trimmed.length > 2000) {
      setError('Description must be between 10 and 2,000 characters.')
      return
    }

    setError('')
    try {
      const dispute = await raiseMutation.mutateAsync({ tripId, category, description: trimmed })
      navigate(`/my-disputes/${dispute.disputeId}`, { replace: true })
    } catch (requestError) {
      setError(getApiMessage(requestError))
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <PageHeader
        eyebrow="Claims"
        title="Raise a Trip Dispute"
        description={tripId ? `Trip reference: ${tripId}` : 'This action must start from an invoice linked to a trip.'}
        actions={
          <Button variant="secondary" onClick={() => navigate(-1)}>
            <ArrowLeft className="mr-1.5 h-4 w-4" /> Back
          </Button>
        }
      />

      <Card>
        <form className="space-y-5" onSubmit={submit} noValidate>
          <div>
            <label className="mb-1.5 block text-sm font-semibold text-on-surface" htmlFor="dispute-category">Category</label>
            <select
              id="dispute-category"
              value={category}
              disabled={raiseMutation.isPending || !tripId}
              onChange={(event) => setCategory(event.target.value)}
              className="w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-800 focus:border-primary focus:outline-none"
            >
              {CATEGORIES.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </div>
          <div>
            <div className="mb-1.5 flex items-center justify-between">
              <label className="block text-sm font-semibold text-on-surface" htmlFor="dispute-description">What happened?</label>
              <span className="text-xs font-mono text-on-surface-variant">{description.trim().length}/2000</span>
            </div>
            <Textarea
              id="dispute-description"
              value={description}
              disabled={raiseMutation.isPending || !tripId}
              onChange={(event) => setDescription(event.target.value)}
              rows={7}
              maxLength={2000}
              placeholder="Describe the issue with enough detail for an Admin review."
            />
            <p className="mt-1 text-xs text-on-surface-variant">Required: 10–2,000 characters. One active dispute is allowed per trip and category.</p>
          </div>
          {error && (
            <p role="alert" className="flex items-start gap-2 rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-status-red-text">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {error}
            </p>
          )}
          <div className="flex justify-end border-t border-slate-border pt-4">
            <Button type="submit" variant="primary" disabled={raiseMutation.isPending || !tripId}>
              <Send className="mr-1.5 h-4 w-4" /> {raiseMutation.isPending ? 'Submitting…' : 'Submit dispute'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
