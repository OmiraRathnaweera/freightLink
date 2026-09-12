import { useEffect } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, useMap } from 'react-leaflet'
import { toast } from 'sonner'
import './leafletIconFix.js'
import { dropoffIcon, pickupIcon } from './routePinIcons.js'
import LocationSearchField from './LocationSearchField.jsx'
import { reverseGeocode } from '../../lib/api/nominatimApi.js'

const DEFAULT_CENTER = [6.9271, 79.8612] // Colombo
const DEFAULT_ZOOM = 12
const SELECTED_ZOOM = 14

function hasPosition(lat, lng) {
  return typeof lat === 'number' && typeof lng === 'number' && Number.isFinite(lat) && Number.isFinite(lng)
}

// MapContainer only honors `center`/`zoom` on first mount — this keeps the
// live map framing both pins once they're both set, or centered on
// whichever one is set first, without remounting.
function FitBounds({ pickupLat, pickupLng, dropoffLat, dropoffLng }) {
  const map = useMap()
  const pickupSet = hasPosition(pickupLat, pickupLng)
  const dropoffSet = hasPosition(dropoffLat, dropoffLng)

  useEffect(() => {
    if (pickupSet && dropoffSet) {
      map.fitBounds(
        [
          [pickupLat, pickupLng],
          [dropoffLat, dropoffLng],
        ],
        { padding: [32, 32] },
      )
    } else if (pickupSet) {
      map.flyTo([pickupLat, pickupLng], SELECTED_ZOOM)
    } else if (dropoffSet) {
      map.flyTo([dropoffLat, dropoffLng], SELECTED_ZOOM)
    }
  }, [map, pickupSet, dropoffSet, pickupLat, pickupLng, dropoffLat, dropoffLng])

  return null
}

/**
 * One shared OSM/Leaflet map showing both the pickup and dropoff pins for a
 * load, with a search box + editable address field for each above it.
 * Formik-agnostic and controlled — see FormikDualLocationField for the
 * Formik-wired wrapper used in the Post/Edit Load forms.
 *
 * Map clicks don't place a pin here (unlike the single-target LocationPicker
 * this replaced) — with two pins on one map a bare click would be ambiguous
 * about which one it's meant to move. Search (unambiguous — each box only
 * ever touches its own pin) and dragging an already-placed pin (also
 * unambiguous — each marker only carries its own drag handler) cover both
 * initial placement and adjustment without needing an "active pin" concept.
 */
function DualLocationPicker({
  pickupId,
  dropoffId,
  pickupLabel = 'Pickup Location',
  dropoffLabel = 'Dropoff Location',
  pickupAddress,
  pickupLat,
  pickupLng,
  dropoffAddress,
  dropoffLat,
  dropoffLng,
  onPickupChange,
  onDropoffChange,
  pickupError,
  dropoffError,
}) {
  const pickupPositioned = hasPosition(pickupLat, pickupLng)
  const dropoffPositioned = hasPosition(dropoffLat, dropoffLng)

  function handlePickupDrag(latlng) {
    const nextLat = latlng.lat
    const nextLng = latlng.lng
    onPickupChange({ address: pickupAddress, lat: nextLat, lng: nextLng })
    reverseGeocode(nextLat, nextLng)
      .then((result) => onPickupChange({ address: result.displayName, lat: nextLat, lng: nextLng }))
      .catch(() => toast.error('Could not look up an address for this location — enter it manually.'))
  }

  function handleDropoffDrag(latlng) {
    const nextLat = latlng.lat
    const nextLng = latlng.lng
    onDropoffChange({ address: dropoffAddress, lat: nextLat, lng: nextLng })
    reverseGeocode(nextLat, nextLng)
      .then((result) => onDropoffChange({ address: result.displayName, lat: nextLat, lng: nextLng }))
      .catch(() => toast.error('Could not look up an address for this location — enter it manually.'))
  }

  return (
    <div>
      <div className="grid grid-cols-1 gap-form-gap sm:grid-cols-2">
        <LocationSearchField
          id={pickupId}
          label={pickupLabel}
          address={pickupAddress}
          error={pickupError}
          onSelectResult={(result) => onPickupChange({ address: result.displayName, lat: result.lat, lng: result.lng })}
          onAddressChange={(address) => onPickupChange({ address, lat: pickupLat, lng: pickupLng })}
        />
        <LocationSearchField
          id={dropoffId}
          label={dropoffLabel}
          address={dropoffAddress}
          error={dropoffError}
          onSelectResult={(result) => onDropoffChange({ address: result.displayName, lat: result.lat, lng: result.lng })}
          onAddressChange={(address) => onDropoffChange({ address, lat: dropoffLat, lng: dropoffLng })}
        />
      </div>

      <div className="mt-2 overflow-hidden rounded-md border border-slate-border">
        <MapContainer
          center={pickupPositioned ? [pickupLat, pickupLng] : DEFAULT_CENTER}
          zoom={pickupPositioned ? SELECTED_ZOOM : DEFAULT_ZOOM}
          scrollWheelZoom={false}
          className="h-96 w-full"
        >
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <FitBounds pickupLat={pickupLat} pickupLng={pickupLng} dropoffLat={dropoffLat} dropoffLng={dropoffLng} />
          {pickupPositioned && dropoffPositioned && (
            <Polyline
              positions={[
                [pickupLat, pickupLng],
                [dropoffLat, dropoffLng],
              ]}
              pathOptions={{ color: 'var(--color-primary)', weight: 2, dashArray: '6 8' }}
            />
          )}
          {pickupPositioned && (
            <Marker
              position={[pickupLat, pickupLng]}
              icon={pickupIcon}
              draggable
              eventHandlers={{ dragend: (event) => handlePickupDrag(event.target.getLatLng()) }}
            />
          )}
          {dropoffPositioned && (
            <Marker
              position={[dropoffLat, dropoffLng]}
              icon={dropoffIcon}
              draggable
              eventHandlers={{ dragend: (event) => handleDropoffDrag(event.target.getLatLng()) }}
            />
          )}
        </MapContainer>
      </div>

      <div className="mt-1 grid grid-cols-1 gap-form-gap sm:grid-cols-2">
        <p className="text-data-mono text-on-surface-variant">
          {pickupPositioned ? `${pickupLat.toFixed(6)}, ${pickupLng.toFixed(6)}` : ' '}
        </p>
        <p className="text-data-mono text-on-surface-variant">
          {dropoffPositioned ? `${dropoffLat.toFixed(6)}, ${dropoffLng.toFixed(6)}` : ' '}
        </p>
      </div>
    </div>
  )
}

export default DualLocationPicker
