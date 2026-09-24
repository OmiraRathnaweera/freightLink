import { useEffect } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip, useMap } from 'react-leaflet'
import '../../../components/map/leafletIconFix.js'
import { dropoffIcon, pickupIcon } from '../../../components/map/routePinIcons.js'
import Card from '../../../components/Card.jsx'

// Real OSM/Leaflet map showing pickup and dropoff together (superseding the
// old static schematic — that placeholder existed only because this project
// had no maps API key configured; OSM/Leaflet needs none, see
// src/components/map/DualLocationPicker.jsx for the same stack used on the
// Post/Edit Load forms).

// MapContainer only honors `center`/`zoom` on first mount — this fits both
// pins in view whenever the coordinates are known, without remounting.
function FitRouteBounds({ pickupLat, pickupLng, dropoffLat, dropoffLng }) {
  const map = useMap()

  useEffect(() => {
    map.fitBounds(
      [
        [pickupLat, pickupLng],
        [dropoffLat, dropoffLng],
      ],
      { padding: [32, 32] },
    )
  }, [map, pickupLat, pickupLng, dropoffLat, dropoffLng])

  return null
}

function RouteMapCard({ origin, destination, originLat, originLng, destinationLat, destinationLng, distanceKm, remainingKm }) {
  return (
    <Card className="relative z-0 isolate overflow-hidden p-0">
      <div className="relative z-0 isolate h-96 w-full">
        <MapContainer center={[originLat, originLng]} zoom={12} scrollWheelZoom={false} className="h-full w-full">
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          <FitRouteBounds pickupLat={originLat} pickupLng={originLng} dropoffLat={destinationLat} dropoffLng={destinationLng} />
          <Polyline
            positions={[
              [originLat, originLng],
              [destinationLat, destinationLng],
            ]}
            pathOptions={{ color: 'var(--color-primary)', weight: 2, dashArray: '6 8' }}
          />
          <Marker position={[originLat, originLng]} icon={pickupIcon}>
            <Tooltip permanent direction="top" offset={[0, -26]}>
              {origin}
            </Tooltip>
          </Marker>
          <Marker position={[destinationLat, destinationLng]} icon={dropoffIcon}>
            <Tooltip permanent direction="top" offset={[0, -26]}>
              {destination}
            </Tooltip>
          </Marker>
        </MapContainer>
      </div>
      {distanceKm != null && (
        <Card.Footer className="flex items-center justify-between">
          <span className="text-body-md text-on-surface-variant">Distance</span>
          <span className="text-data-mono text-on-surface">
            {distanceKm} km{remainingKm != null ? ` · ${remainingKm} km remaining` : ''}
          </span>
        </Card.Footer>
      )}
    </Card>
  )
}

export default RouteMapCard
