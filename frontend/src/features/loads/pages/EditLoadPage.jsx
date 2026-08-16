import { Link, useParams } from 'react-router-dom'
import { MapPin } from 'lucide-react'
import Card from '../../../components/Card.jsx'
import Input from '../../../components/Input.jsx'
import Textarea from '../../../components/Textarea.jsx'
import Button from '../../../components/Button.jsx'
import EmptyState from '../../../components/EmptyState.jsx'
import { getLoadById } from '../mockData.js' // TODO: replace with real data
import RateBreakdownCard from '../components/RateBreakdownCard.jsx'
import FormField from '../components/FormField.jsx'

// Edit Load — cloned from Stitch screen 8 ("Edit Load — FM-7942"). Real
// route (/loads/:loadId/edit). Fields are uncontrolled (defaultValue, no
// useState) — none of them need live reactivity yet, and form
// validation/submission is explicitly out of scope for now
// (.claude/rules/frontend-design.md #3).
function EditLoadPage() {
  const { loadId } = useParams()
  const load = getLoadById(loadId)

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
                <Input defaultValue={load.origin} className="pl-9" />
              </div>
            </FormField>
            <FormField label="Dropoff location">
              <div className="relative">
                <MapPin
                  className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant"
                  strokeWidth={1.5}
                />
                <Input defaultValue={load.destination} className="pl-9" />
              </div>
            </FormField>
          </div>

          <FormField label="Pickup Window">
            <Input defaultValue={load.pickupWindow} />
          </FormField>

          <FormField label="Cargo Description">
            <Textarea defaultValue={load.cargoDescription} rows={3} />
          </FormField>

          <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
            <FormField label="Total Weight (kg)">
              <Input mono type="number" defaultValue={load.weightKg} />
            </FormField>
            <FormField label="Volume (m³)">
              <Input mono type="number" defaultValue={load.volumeM3} />
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
