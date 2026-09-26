/**
 * Vehicle class definitions, capacity weight limitations, and Sri Lankan vehicle patterns.
 *
 * Weight limitations (matching vehicle class descriptions):
 * - Mini Truck: Light urban cargo (up to 2,500 kg) -> 1 to 2,500 kg
 * - Medium Lorry: Regional transit (2,500 – 10,000 kg) -> 2,500 to 10,000 kg
 * - Container Truck: Heavy container & long haul (10,000+ kg) -> 10,000 to 100,000 kg
 */

export const VEHICLE_CLASSES = Object.freeze({
  MINI_TRUCK: 'MiniTruck',
  MEDIUM_LORRY: 'MediumLorry',
  CONTAINER_TRUCK: 'ContainerTruck',
})

export const VEHICLE_CLASS_CONFIG = Object.freeze({
  [VEHICLE_CLASSES.MINI_TRUCK]: {
    value: 'MiniTruck',
    label: 'Mini Truck',
    desc: 'Light urban cargo (up to 2,500 kg)',
    minCapacityKg: 1,
    maxCapacityKg: 2500,
    defaultCapacityKg: 1500,
    capacityHint: 'Allowed: 1 – 2,500 kg',
    placeholderCapacity: '1500',
  },
  [VEHICLE_CLASSES.MEDIUM_LORRY]: {
    value: 'MediumLorry',
    label: 'Medium Lorry',
    desc: 'Regional transit (2,500 – 10,000 kg)',
    minCapacityKg: 2500,
    maxCapacityKg: 10000,
    defaultCapacityKg: 5000,
    capacityHint: 'Allowed: 2,500 – 10,000 kg',
    placeholderCapacity: '5000',
  },
  [VEHICLE_CLASSES.CONTAINER_TRUCK]: {
    value: 'ContainerTruck',
    label: 'Container Truck',
    desc: 'Heavy container & long haul (10,000+ kg)',
    minCapacityKg: 10000,
    maxCapacityKg: 100000,
    defaultCapacityKg: 20000,
    capacityHint: 'Allowed: 10,000 – 100,000 kg',
    placeholderCapacity: '20000',
  },
})

export const VEHICLE_TYPES = Object.values(VEHICLE_CLASS_CONFIG)

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
