import { useEffect, useState } from 'react'
import { MapContainer, Marker, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet'
import { Loader2, MapPin, Navigation } from 'lucide-react'
import { toast } from 'sonner'
import './leafletIconFix.js'
import { pickupIcon } from './routePinIcons.js'
import LocationSearchField from './LocationSearchField.jsx'
import { reverseGeocode } from '../../lib/api/nominatimApi.js'
import Button from '../Button.jsx'
import Input from '../Input.jsx'
import ErrorMessage from '../form/ErrorMessage.jsx'

const DEFAULT_CENTER = [6.9271, 79.8612] // Colombo
const DEFAULT_ZOOM = 12
const SELECTED_ZOOM = 14

function hasPosition(lat, lng) {
  const numLat = Number(lat)
  const numLng = Number(lng)
  return Number.isFinite(numLat) && Number.isFinite(numLng) && !isNaN(numLat) && !isNaN(numLng)
}

function FlyToLocation({ lat, lng }) {
  const map = useMap()
  useEffect(() => {
    if (hasPosition(lat, lng)) {
      map.flyTo([Number(lat), Number(lng)], SELECTED_ZOOM, { duration: 0.8 })
    }
  }, [map, lat, lng])
  return null
}

function MapClickHandler({ onMapClick }) {
  useMapEvents({
    click(e) {
      onMapClick(e.latlng)
    },
  })
  return null
}

/**
 * Interactive OpenStreetMap picker for an Agency Depot Yard location.
 * Provides location search (Nominatim), browser geolocation (real-time GPS),
 * click-to-pin, and draggable marker adjustment, with two-way sync to
 * address and latitude/longitude coordinates.
 *
 * @param {string} [id]
 * @param {string} [label='Depot Yard Location']
 * @param {string} address
 * @param {number|string} lat
 * @param {number|string} lng
 * @param {(loc: { address: string, lat: number|string, lng: number|string }) => void} onChange
 * @param {string} [addressError]
 * @param {string} [latError]
 * @param {string} [lngError]
 */
function YardLocationPicker({
  id = 'yard-location',
  label = 'Depot Yard Location',
  address = '',
  lat = '',
  lng = '',
  onChange,
  addressError,
  latError,
  lngError,
}) {
  const [isLocating, setIsLocating] = useState(false)
  const positioned = hasPosition(lat, lng)
  const numLat = positioned ? Number(lat) : null
  const numLng = positioned ? Number(lng) : null

  function handleGetCurrentLocation() {
    if (!navigator.geolocation) {
      toast.error('Geolocation is not supported by your browser.')
      return
    }

    setIsLocating(true)
    navigator.geolocation.getCurrentPosition(
      async (position) => {
        const nextLat = Number(position.coords.latitude.toFixed(6))
        const nextLng = Number(position.coords.longitude.toFixed(6))
        setIsLocating(false)
        onChange({ address: address || '', lat: nextLat, lng: nextLng })

        try {
          const result = await reverseGeocode(nextLat, nextLng)
          onChange({ address: result.displayName, lat: nextLat, lng: nextLng })
          toast.success('Depot location set to your current GPS position.')
        } catch {
          toast.info('GPS coordinates captured. Please verify the address.')
        }
      },
      () => {
        setIsLocating(false)
        toast.error('Could not get your location. Please check browser permissions or select on map.')
      },
      { enableHighAccuracy: true, timeout: 10000 },
    )
  }

  function handleMapClick(latlng) {
    const nextLat = Number(latlng.lat.toFixed(6))
    const nextLng = Number(latlng.lng.toFixed(6))
    onChange({ address: address || '', lat: nextLat, lng: nextLng })

    reverseGeocode(nextLat, nextLng)
      .then((result) => onChange({ address: result.displayName, lat: nextLat, lng: nextLng }))
      .catch(() => {})
  }

  function handleMarkerDrag(latlng) {
    const nextLat = Number(latlng.lat.toFixed(6))
    const nextLng = Number(latlng.lng.toFixed(6))
    onChange({ address: address || '', lat: nextLat, lng: nextLng })

    reverseGeocode(nextLat, nextLng)
      .then((result) => onChange({ address: result.displayName, lat: nextLat, lng: nextLng }))
      .catch(() => toast.error('Could not look up an address for this location — please enter it manually.'))
  }

  function handleSelectResult(result) {
    onChange({
      address: result.displayName,
      lat: Number(result.lat.toFixed(6)),
      lng: Number(result.lng.toFixed(6)),
    })
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <label htmlFor={id} className="block text-body-md font-semibold text-primary">
            {label}
          </label>
          <p className="text-body-sm text-on-surface-variant">
            Search a place, use GPS, or click/drag the pin on the map to set your depot yard
          </p>
        </div>
        <Button
          type="button"
          variant="secondary"
          onClick={handleGetCurrentLocation}
          disabled={isLocating}
          className="inline-flex items-center gap-1.5 self-start text-xs font-medium sm:self-auto"
        >
          {isLocating ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Navigation className="h-3.5 w-3.5 text-primary" />
          )}
          {isLocating ? 'Locating...' : 'Use Current Location'}
        </Button>
      </div>

      <LocationSearchField
        id={id}
        label="Yard Depot Address"
        address={address}
        error={addressError}
        onSelectResult={handleSelectResult}
        onAddressChange={(newAddress) => onChange({ address: newAddress, lat, lng })}
      />

      <div className="overflow-hidden rounded-md border border-slate-border">
        <div className="relative">
          <MapContainer
            center={positioned ? [numLat, numLng] : DEFAULT_CENTER}
            zoom={positioned ? SELECTED_ZOOM : DEFAULT_ZOOM}
            scrollWheelZoom={false}
            className="h-72 w-full"
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            {positioned && <FlyToLocation lat={numLat} lng={numLng} />}
            <MapClickHandler onMapClick={handleMapClick} />
            {positioned && (
              <Marker
                position={[numLat, numLng]}
                icon={pickupIcon}
                draggable
                eventHandlers={{ dragend: (event) => handleMarkerDrag(event.target.getLatLng()) }}
              >
                <Tooltip permanent direction="top" offset={[0, -28]}>
                  Depot Yard
                </Tooltip>
              </Marker>
            )}
          </MapContainer>

          <div className="pointer-events-none absolute bottom-2 left-2 right-2 flex flex-wrap items-center justify-between gap-1 rounded bg-surface-container-lowest/90 px-3 py-1.5 text-body-xs backdrop-blur-xs">
            <span className="flex items-center gap-1 text-on-surface">
              <MapPin className="h-3.5 w-3.5 text-primary" />
              Click anywhere on map or drag pin to adjust
            </span>
            <span className="font-mono text-on-surface-variant">
              {positioned ? `${numLat.toFixed(6)}, ${numLng.toFixed(6)}` : 'No point selected'}
            </span>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
        <div>
          <label htmlFor={`${id}-lat`} className="mb-1.5 block text-body-md font-semibold text-on-surface">
            Yard Latitude
          </label>
          <Input
            id={`${id}-lat`}
            name="yardLat"
            type="number"
            step="any"
            mono
            placeholder="6.9271"
            value={lat ?? ''}
            onChange={(e) => {
              const val = e.target.value
              onChange({ address, lat: val === '' ? '' : Number(val), lng })
            }}
            error={Boolean(latError)}
          />
          <ErrorMessage>{latError}</ErrorMessage>
        </div>

        <div>
          <label htmlFor={`${id}-lng`} className="mb-1.5 block text-body-md font-semibold text-on-surface">
            Yard Longitude
          </label>
          <Input
            id={`${id}-lng`}
            name="yardLng"
            type="number"
            step="any"
            mono
            placeholder="79.8612"
            value={lng ?? ''}
            onChange={(e) => {
              const val = e.target.value
              onChange({ address, lat, lng: val === '' ? '' : Number(val) })
            }}
            error={Boolean(lngError)}
          />
          <ErrorMessage>{lngError}</ErrorMessage>
        </div>
      </div>
    </div>
  )
}

export default YardLocationPicker
