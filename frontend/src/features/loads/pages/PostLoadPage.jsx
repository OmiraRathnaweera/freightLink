import { useState } from 'react'
import { MapPin, Upload } from 'lucide-react'
import PageHeader from '../../../components/PageHeader.jsx'
import Card from '../../../components/Card.jsx'
import Input from '../../../components/Input.jsx'
import Textarea from '../../../components/Textarea.jsx'
import Button from '../../../components/Button.jsx'
import RateBreakdownCard from '../components/RateBreakdownCard.jsx'
import FormField from '../components/FormField.jsx'

// Post a Load — cloned from the Stitch "Post a Load — Initial Form" screen.
// Real route (/loads/new). The Post Load button is intentionally
// decorative — no backend/apiClient exists yet to actually submit against,
// matching this project's existing placeholder-page convention.
function PostLoadPage() {
  const [origin, setOrigin] = useState('')
  const [destination, setDestination] = useState('Kandy Central')
  const [cargoDescription, setCargoDescription] = useState('')
  const [weight, setWeight] = useState('')
  const [pickupWindow, setPickupWindow] = useState('')

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="LOADS"
        title="Post a Load"
        description="Give agencies the details they need to bid on your shipment."
      />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <Card className="space-y-form-gap lg:col-span-2">
          <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
            <FormField label="Pickup location">
              <div className="relative">
                <MapPin
                  className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant"
                  strokeWidth={1.5}
                />
                <Input
                  value={origin}
                  onChange={(event) => setOrigin(event.target.value)}
                  placeholder="e.g. Colombo Yard"
                  className="pl-9"
                />
              </div>
            </FormField>
            <FormField label="Dropoff location">
              <div className="relative">
                <MapPin
                  className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant"
                  strokeWidth={1.5}
                />
                <Input value={destination} onChange={(event) => setDestination(event.target.value)} className="pl-9" />
              </div>
            </FormField>
          </div>

          <FormField label="Cargo Description">
            <Textarea
              value={cargoDescription}
              onChange={(event) => setCargoDescription(event.target.value)}
              rows={3}
              placeholder="Describe the cargo — type, packaging, handling notes"
            />
          </FormField>

          <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
            <FormField label="Total Weight (kg)">
              <Input mono type="number" value={weight} onChange={(event) => setWeight(event.target.value)} placeholder="15000" />
            </FormField>
            <FormField label="Pickup Window">
              <Input
                value={pickupWindow}
                onChange={(event) => setPickupWindow(event.target.value)}
                placeholder="14 Aug 2026, 08:00–18:00"
              />
            </FormField>
          </div>

          <FormField label="Load Documents">
            <div className="flex flex-col items-center justify-center gap-2 rounded-md border-2 border-dashed border-slate-300 px-6 py-8 text-center">
              <Upload className="h-6 w-6 text-on-surface-variant" strokeWidth={1.5} />
              <p className="text-body-md text-on-surface-variant">Drag and drop files, or click to browse</p>
            </div>
          </FormField>

          <div className="flex justify-end pt-2">
            <Button variant="primary">Post Load</Button>
          </div>
        </Card>

        <RateBreakdownCard
          title="Live Estimate"
          distanceKm={124.2}
          lineItems={[
            { label: 'Base Rate', amount: 32000 },
            { label: 'Fuel Surcharge', amount: 6000 },
          ]}
          total={38000}
          note="Final rate is confirmed once an agency accepts this load."
        />
      </div>
    </div>
  )
}

export default PostLoadPage
