import { VehicleType } from '../../../lib/enums.js'

/**
 * Vehicle type definitions, capacity weight limitations, and Sri Lankan vehicle patterns.
 * Matches backend FreightLink.Api.Entities.Enums.VehicleType.
 *
 * Weight limitations (matching vehicle type descriptions):
 * - Lorry: General cargo & regional transit (up to 10,000 kg) -> 1 to 10,000 kg
 * - Container: Heavy container & long haul (10,000 – 100,000 kg) -> 10,000 to 100,000 kg
 * - Refrigerated: Cold chain & perishable goods (up to 25,000 kg) -> 1 to 25,000 kg
 * - Flatbed: Heavy machinery & oversized cargo (up to 50,000 kg) -> 1 to 50,000 kg
 * - Tipper: Bulk materials, sand & aggregate (up to 30,000 kg) -> 1 to 30,000 kg
 */

export const VEHICLE_TYPE_CONFIG = Object.freeze({
  [VehicleType.LORRY]: {
    value: VehicleType.LORRY,
    label: 'Lorry',
    desc: 'General cargo & regional transit (up to 10,000 kg)',
    minCapacityKg: 1,
    maxCapacityKg: 10000,
    defaultCapacityKg: 5000,
    capacityHint: 'Allowed: 1 – 10,000 kg',
    placeholderCapacity: '5000',
    placeholderVolume: '18',
  },
  [VehicleType.CONTAINER]: {
    value: VehicleType.CONTAINER,
    label: 'Container',
    desc: 'Heavy container & long haul (10,000 – 100,000 kg)',
    minCapacityKg: 10000,
    maxCapacityKg: 100000,
    defaultCapacityKg: 25000,
    capacityHint: 'Allowed: 10,000 – 100,000 kg',
    placeholderCapacity: '25000',
    placeholderVolume: '65',
  },
  [VehicleType.REFRIGERATED]: {
    value: VehicleType.REFRIGERATED,
    label: 'Refrigerated',
    desc: 'Cold chain & perishable goods (up to 25,000 kg)',
    minCapacityKg: 1,
    maxCapacityKg: 25000,
    defaultCapacityKg: 8000,
    capacityHint: 'Allowed: 1 – 25,000 kg',
    placeholderCapacity: '8000',
    placeholderVolume: '25',
  },
  [VehicleType.FLAT_BED]: {
    value: VehicleType.FLAT_BED,
    label: 'Flatbed',
    desc: 'Heavy machinery & oversized cargo (up to 50,000 kg)',
    minCapacityKg: 1,
    maxCapacityKg: 50000,
    defaultCapacityKg: 20000,
    capacityHint: 'Allowed: 1 – 50,000 kg',
    placeholderCapacity: '20000',
    placeholderVolume: '40',
  },
  [VehicleType.TIPPER]: {
    value: VehicleType.TIPPER,
    label: 'Tipper',
    desc: 'Bulk materials, sand & aggregate (up to 30,000 kg)',
    minCapacityKg: 1,
    maxCapacityKg: 30000,
    defaultCapacityKg: 15000,
    capacityHint: 'Allowed: 1 – 30,000 kg',
    placeholderCapacity: '15000',
    placeholderVolume: '20',
  },
})

export const VEHICLE_CLASS_CONFIG = VEHICLE_TYPE_CONFIG
export const VEHICLE_TYPES = Object.values(VEHICLE_TYPE_CONFIG)

/**
 * Valid Sri Lankan DMT province prefixes:
 * WP (Western), CP (Central), SP (Southern), NP (Northern), EP (Eastern),
 * NW / NWP (North Western), NC / NCP (North Central), UP / UVA (Uva), SG / SAB (Sabaragamuwa).
 */
export const SRI_LANKAN_PROVINCES = Object.freeze([
  'WP',
  'CP',
  'SP',
  'NP',
  'EP',
  'NW',
  'NC',
  'UP',
  'SG',
  'NWP',
  'NCP',
  'SAB',
  'UVA',
])

/**
 * Regex matching authentic Sri Lankan Department of Motor Traffic (DMT) registration numbers:
 * 1. Provincial letter plates: e.g. "WP CAB-1234", "WP-CAB-1234", "WP-CAD-1020", "CP-CONT-9988", "WP-DA-9988"
 * 2. Series without province prefix: e.g. "CAB-1234", "DA-9988", "GA-5678"
 * 3. Vintage Sri series: e.g. "228-1234", "40-1234", "WP-228-1234"
 */
export const SRI_LANKAN_VEHICLE_REG_REGEX =
  /^((?:(WP|CP|SP|NP|EP|NW|NC|UP|SG|NWP|NCP|SAB|UVA)[-\s]?)?[A-Z]{2,4}[-\s]?\d{4}|(?:(WP|CP|SP|NP|EP|NW|NC|UP|SG|NWP|NCP|SAB|UVA)[-\s]?)?\d{1,3}[-\s]\d{4})$/i
