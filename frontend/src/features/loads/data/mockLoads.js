import { LoadStatus } from '../../../lib/enums.js'

// Static mock data — there's no backend wiring yet (src/lib/apiClient.js is
// empty, no loadsSlice exists), so these pages render against this instead
// of a real API. Reuses the sample data from the Stitch "My Loads —
// Dashboard" screen (screen 12), with one fix: that screen's export had two
// different rows both labeled "FM-7935" — renamed the second to FM-7934
// here rather than treating it as trustworthy sample data.
//
// Every load gets a full detail payload (not just FM-7942, the one Stitch
// carried across its Detail screens) so any dashboard row leads to a real,
// working /loads/:loadId page.
export const MOCK_LOADS = [
  {
    id: 'FM-7942',
    origin: 'Colombo Yard',
    destination: 'Kandy Central',
    distanceKm: 124.2,
    weightKg: 4500,
    volumeM3: 18,
    status: LoadStatus.IN_TRANSIT,
    postedDate: '2026-08-14',
    cargoDescription: 'Palletized dry goods, 12 pallets, stackable, no hazardous materials.',
    pickupWindow: '14 Aug 2026, 08:00–18:00',
    documents: [
      { name: 'Manifest_7942.pdf', type: 'Manifest' },
      { name: 'Customs_Decl.docx', type: 'Other' },
    ],
    rate: { baseRate: 72000, fuelSurcharge: 13000, total: 85000 },
    activityLog: [
      { timestamp: '2026-08-14 08:12', source: 'SYS_RATE', actor: 'System', action: 'Load created', detail: 'Posted by shipper' },
      { timestamp: '2026-08-14 09:03', source: 'SVC_MATCH', actor: 'Matching Agent', action: 'Agency matched', detail: 'Ceylon Express Logistics' },
      { timestamp: '2026-08-14 10:41', source: 'DRV_APP', actor: 'Nimal Perera', action: 'Picked up', detail: 'Colombo Yard' },
      { timestamp: '2026-08-14 13:15', source: 'SVC_GPS', actor: 'System', action: 'In transit', detail: '26 km remaining' },
    ],
  },
  {
    id: 'FM-7941',
    origin: 'Galle',
    destination: 'Colombo',
    distanceKm: 116,
    weightKg: 12000,
    volumeM3: 32,
    status: LoadStatus.MATCHED,
    postedDate: '2026-08-13',
    attempt: 'Attempt 2 of 3',
    cargoDescription: 'Rubber sheet bales, 40 units, moisture-sensitive.',
    pickupWindow: '15 Aug 2026, 06:00–12:00',
    documents: [{ name: 'Manifest_7941.pdf', type: 'Manifest' }],
    rate: { baseRate: 58000, fuelSurcharge: 9500, total: 67500 },
    activityLog: [
      { timestamp: '2026-08-13 07:20', source: 'SYS_RATE', actor: 'System', action: 'Load created', detail: 'Posted by shipper' },
      { timestamp: '2026-08-13 08:55', source: 'SVC_MATCH', actor: 'Matching Agent', action: 'Agency matched', detail: 'Wayamba Carriers (attempt 2)' },
    ],
  },
  {
    id: 'FM-7938',
    origin: 'Negombo',
    destination: 'Kurunegala',
    distanceKm: 62,
    weightKg: 2500,
    volumeM3: 10,
    status: LoadStatus.DELIVERED,
    postedDate: '2026-08-10',
    cargoDescription: 'Fresh seafood, refrigerated transport required.',
    pickupWindow: '10 Aug 2026, 04:00–07:00',
    documents: [{ name: 'Manifest_7938.pdf', type: 'Manifest' }, { name: 'Delivery_POD_7938.jpg', type: 'CargoPhoto' }],
    rate: { baseRate: 21000, fuelSurcharge: 4200, total: 25200 },
    activityLog: [
      { timestamp: '2026-08-10 04:10', source: 'DRV_APP', actor: 'Ruwan Silva', action: 'Picked up', detail: 'Negombo' },
      { timestamp: '2026-08-10 06:35', source: 'DRV_APP', actor: 'Ruwan Silva', action: 'Delivered', detail: 'Kurunegala — ePOD captured' },
    ],
  },
  {
    id: 'FM-7935',
    origin: 'Colombo',
    destination: 'Trincomalee',
    distanceKm: 257,
    weightKg: 8000,
    volumeM3: 24,
    status: LoadStatus.DRAFT,
    postedDate: '2026-08-15',
    cargoDescription: 'Construction hardware, palletized, no special handling.',
    pickupWindow: '18 Aug 2026, 07:00–11:00',
    documents: [],
    rate: { baseRate: 61000, fuelSurcharge: 11000, total: 72000 },
    activityLog: [{ timestamp: '2026-08-15 09:00', source: 'SYS_RATE', actor: 'System', action: 'Draft saved', detail: 'Not yet posted' }],
  },
  {
    id: 'FM-7934',
    origin: 'Kandy',
    destination: 'Colombo',
    distanceKm: 115,
    weightKg: 5500,
    volumeM3: 20,
    status: LoadStatus.POSTED,
    postedDate: '2026-08-12',
    cargoDescription: 'Tea chests, 90 units, standard palletized freight.',
    pickupWindow: '13 Aug 2026, 09:00–14:00',
    documents: [{ name: 'Manifest_7934.pdf', type: 'Manifest' }],
    rate: { baseRate: 46000, fuelSurcharge: 8000, total: 54000 },
    activityLog: [
      { timestamp: '2026-08-12 10:05', source: 'SYS_RATE', actor: 'System', action: 'Load created', detail: 'Posted by shipper' },
      { timestamp: '2026-08-12 15:40', source: 'SVC_MATCH', actor: 'Matching Agent', action: 'All agencies declined', detail: '3 of 3 declined' },
    ],
  },
]

export function getLoadById(loadId) {
  return MOCK_LOADS.find((load) => load.id === loadId)
}
