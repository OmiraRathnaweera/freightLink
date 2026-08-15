import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { MapPin } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Input from '../../../components/Input.jsx'
import Textarea from '../../../components/Textarea.jsx'
import Button from '../../../components/Button.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import { getLoadById } from '../data/mockLoads.js'
import RateBreakdownCard from '../components/RateBreakdownCard.jsx'
import FormField from '../components/FormField.jsx'

// Edit Load — cloned from Stitch screen 8 ("Edit Load — FM-7942"). Real
// route (/loads/:loadId/edit). Only one designed state, so no preview
// switcher here (see CLAUDE.md > Cloning Stitch screens — switchers are
// only added where Stitch actually specified multiple states).
function EditLoadPage() {
  const { loadId } = useParams()
  const load = getLoadById(loadId)

  const [origin, setOrigin] = useState(load?.origin ?? '')
  const [destination, setDestination] = useState(load?.destination ?? '')
  const [pickupWindow, setPickupWindow] = useState(load?.pickupWindow ?? '')
  const [cargoDescription, setCargoDescription] = useState(load?.cargoDescription ?? '')
  const [weight, setWeight] = useState(load?.weightKg ?? '')
  const [volume, setVolume] = useState(load?.volumeM3 ?? '')

  if (!load) {
    return (
      <div className="space-y-6">
        <Card>
          <EmptyState title={`No load found for "${loadId}"`} description="Check the load ID and try again." />
        </Card>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-headline-lg text-on-surface">Edit Load {load.id}</h1>
        <div className="flex items-center gap-2">
          <Button as={Link} to={`/loads/${load.id}`} variant="secondary">
            Cancel
          </Button>
          <Button variant="primary">Save changes</Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <Card className="space-y-form-gap lg:col-span-2">
          <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
            <FormField label="Pickup location">
              <div className="relative">
                <MapPin
                  className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant"
                  strokeWidth={1.5}
                />
                <Input value={origin} onChange={(event) => setOrigin(event.target.value)} className="pl-9" />
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

          <FormField label="Pickup Window">
            <Input value={pickupWindow} onChange={(event) => setPickupWindow(event.target.value)} />
          </FormField>

          <FormField label="Cargo Description">
            <Textarea value={cargoDescription} onChange={(event) => setCargoDescription(event.target.value)} rows={3} />
          </FormField>

          <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
            <FormField label="Total Weight (kg)">
              <Input mono type="number" value={weight} onChange={(event) => setWeight(event.target.value)} />
            </FormField>
            <FormField label="Volume (m³)">
              <Input mono type="number" value={volume} onChange={(event) => setVolume(event.target.value)} />
            </FormField>
          </div>
        </Card>

        <RateBreakdownCard
          title="Estimated Cost Breakdown"
          distanceKm={load.distanceKm}
          lineItems={[
            { label: 'Base Rate', amount: load.rate.baseRate },
            { label: 'Weight Surcharge', amount: Math.round(load.rate.fuelSurcharge * 0.6) },
            { label: 'Toll', amount: Math.round(load.rate.fuelSurcharge * 0.4) },
          ]}
          total={load.rate.total}
        />
      </div>
    </div>
  )
}

export default EditLoadPage
