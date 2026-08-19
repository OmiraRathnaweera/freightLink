import L from 'leaflet'

// Shared pickup/dropoff pin styling — navy for pickup, green for dropoff —
// used by both the read-only RouteMapCard (Load Detail) and the editable
// DualLocationPicker (Post/Edit Load forms), so the color coding stays
// consistent wherever a load's route is drawn.
const PIN_SVG = (colorClassName) => `
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" stroke-width="1.5" class="${colorClassName}">
    <path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z" fill="currentColor" fill-opacity="0.15" />
    <circle cx="12" cy="10" r="3" fill="currentColor" />
  </svg>
`

export const pickupIcon = L.divIcon({ html: PIN_SVG('text-primary'), className: '', iconSize: [30, 30], iconAnchor: [15, 28] })
export const dropoffIcon = L.divIcon({
  html: PIN_SVG('text-status-green-text'),
  className: '',
  iconSize: [30, 30],
  iconAnchor: [15, 28],
})
