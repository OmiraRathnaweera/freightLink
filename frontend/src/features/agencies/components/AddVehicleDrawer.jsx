import { useState } from 'react'
import { AlertCircle, Loader2, Truck, X } from 'lucide-react'
import { toast } from 'sonner'
import Button from '../../../components/Button.jsx'
import Input from '../../../components/Input.jsx'
import { useEscapeKey } from '../../../hooks/useEscapeKey.js'
import { useAddVehicleMutation } from '../api/agencyApi.js'

const VEHICLE_TYPES = [
  { value: 'MiniTruck', label: 'Mini Truck', desc: 'Light urban cargo (up to 2,500 kg)' },
  { value: 'MediumLorry', label: 'Medium Lorry', desc: 'Regional transit (2,500 – 10,000 kg)' },
  { value: 'ContainerTruck', label: 'Container Truck', desc: 'Heavy container & long haul (10,000+ kg)' },
]

/**
 * Slide-over right drawer / modal for adding a new vehicle to an agency's fleet.
 *
 * @param {string} agencyId
 * @param {boolean} isOpen
 * @param {() => void} onClose
 */
function AddVehicleDrawer({ agencyId, isOpen, onClose }) {
  const [vehicleType, setVehicleType] = useState('MiniTruck')
  const [registrationNo, setRegistrationNo] = useState('')
  const [capacityKg, setCapacityKg] = useState('')
  const [volumeM3, setVolumeM3] = useState('')
  const [error, setError] = useState(null)

  const addVehicleMutation = useAddVehicleMutation()

  useEscapeKey(isOpen, onClose)

  if (!isOpen) return null

  async function handleSubmit(e) {
    e.preventDefault()
    setError(null)

    const trimmedReg = registrationNo.trim().toUpperCase()
    const numCapacity = Number(capacityKg)
    const numVolume = Number(volumeM3)

    if (!trimmedReg || trimmedReg.length < 3) {
      setError('Registration number must be at least 3 characters.')
      return
    }

    if (!numCapacity || numCapacity <= 0) {
      setError('Capacity must be a positive number greater than 0.')
      return
    }

    if (!numVolume || numVolume <= 0) {
      setError('Volume must be a positive number greater than 0.')
      return
    }

    try {
      await addVehicleMutation.mutateAsync({
        agencyId,
        vehicle: {
          registrationNo: trimmedReg,
          vehicleType,
          capacityKg: numCapacity,
          volumeM3: numVolume,
        },
      })

      toast.success(`Vehicle ${trimmedReg} registered successfully!`)
      setRegistrationNo('')
      setCapacityKg('')
      setVolumeM3('')
      onClose()
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || 'Failed to add vehicle. Please try again.')
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      {/* Backdrop */}
      <div
        onClick={onClose}
        className="fixed inset-0 bg-primary/40 backdrop-blur-xs transition-opacity"
        aria-hidden="true"
      />

      {/* Drawer */}
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="add-vehicle-title"
        className="relative z-50 flex h-full w-full max-w-md flex-col border-l border-slate-border bg-surface-container-lowest shadow-2xl"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-border px-6 py-4">
          <div className="flex items-center space-x-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-md bg-primary-container text-on-primary">
              <Truck className="h-5 w-5" />
            </div>
            <div>
              <h2 id="add-vehicle-title" className="text-body-lg font-bold text-primary">
                Add Fleet Vehicle
              </h2>
              <p className="text-body-xs text-on-surface-variant">Register a new truck or lorry</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-md p-1.5 text-on-surface-variant hover:bg-slate-100 hover:text-on-surface"
            aria-label="Close drawer"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <form onSubmit={handleSubmit} className="flex flex-1 flex-col justify-between overflow-y-auto p-6">
          <div className="space-y-5">
            {error && (
              <div className="flex items-start gap-2 rounded-md border border-status-red-text bg-status-red-bg px-3.5 py-2.5 text-body-sm text-status-red-text">
                <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>{error}</span>
              </div>
            )}

            <div>
              <label className="mb-2 block text-body-md font-semibold text-primary">
                Vehicle Class
              </label>
              <div className="space-y-2">
                {VEHICLE_TYPES.map((type) => (
                  <label
                    key={type.value}
                    className={`flex cursor-pointer items-start space-x-3 rounded-lg border p-3 transition-colors ${
                      vehicleType === type.value
                        ? 'border-primary bg-primary/5 ring-1 ring-primary/20'
                        : 'border-slate-border hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="vehicleType"
                      value={type.value}
                      checked={vehicleType === type.value}
                      onChange={(e) => setVehicleType(e.target.value)}
                      className="mt-1 text-primary focus:ring-primary"
                    />
                    <div>
                      <p className="text-body-md font-medium text-on-surface">{type.label}</p>
                      <p className="text-body-xs text-on-surface-variant">{type.desc}</p>
                    </div>
                  </label>
                ))}
              </div>
            </div>

            <div>
              <label htmlFor="veh-reg" className="mb-1.5 block text-body-md font-semibold text-primary">
                Registration Number <span className="text-status-red-text">*</span>
              </label>
              <Input
                id="veh-reg"
                required
                mono
                value={registrationNo}
                onChange={(e) => setRegistrationNo(e.target.value)}
                placeholder="e.g. WP AB-1234 or CAB-5678"
              />
              <p className="mt-1 text-body-xs text-on-surface-variant">
                Official Department of Motor Traffic registration number
              </p>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label htmlFor="veh-cap" className="mb-1.5 block text-body-md font-semibold text-primary">
                  Capacity (Kg) <span className="text-status-red-text">*</span>
                </label>
                <Input
                  id="veh-cap"
                  type="number"
                  required
                  mono
                  min="1"
                  step="any"
                  value={capacityKg}
                  onChange={(e) => setCapacityKg(e.target.value)}
                  placeholder="2500"
                />
              </div>

              <div>
                <label htmlFor="veh-vol" className="mb-1.5 block text-body-md font-semibold text-primary">
                  Volume (m³) <span className="text-status-red-text">*</span>
                </label>
                <Input
                  id="veh-vol"
                  type="number"
                  required
                  mono
                  min="0.1"
                  step="any"
                  value={volumeM3}
                  onChange={(e) => setVolumeM3(e.target.value)}
                  placeholder="12.5"
                />
              </div>
            </div>
          </div>

          {/* Footer buttons */}
          <div className="mt-8 border-t border-slate-border pt-4">
            <div className="flex items-center justify-end space-x-3">
              <Button type="button" variant="secondary" onClick={onClose}>
                Cancel
              </Button>
              <Button
                type="submit"
                disabled={addVehicleMutation.isPending}
                className="inline-flex items-center gap-1.5"
              >
                {addVehicleMutation.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                {addVehicleMutation.isPending ? 'Registering...' : 'Add to Fleet'}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  )
}

export default AddVehicleDrawer
