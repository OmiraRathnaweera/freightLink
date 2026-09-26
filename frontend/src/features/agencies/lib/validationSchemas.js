import * as Yup from 'yup'
import { SRI_LANKAN_VEHICLE_REG_REGEX, VEHICLE_CLASS_CONFIG } from './vehicleClasses.js'

/**
 * Validation schema for registering a new fleet vehicle in an agency.
 *
 * Rules:
 * - vehicleType: required enum ('MiniTruck' | 'MediumLorry' | 'ContainerTruck')
 * - registrationNo: required, authentic Sri Lankan DMT vehicle registration pattern
 *   (e.g. "WP CAB-1234", "WP-CAD-1020", "CAB-5678", "228-1234")
 * - capacityKg: required positive number, dynamically validated against the selected
 *   vehicle class weight limitations (from vehicle class descriptions):
 *     * Mini Truck: 1 to 2,500 kg
 *     * Medium Lorry: 2,500 to 10,000 kg
 *     * Container Truck: 10,000 to 100,000 kg
 * - volumeM3: required positive number, max 1,000 m³
 */
export const addVehicleSchema = Yup.object({
  vehicleType: Yup.string()
    .transform((value, originalValue) => (originalValue === '' ? undefined : value))
    .required('Vehicle type is required')
    .oneOf(Object.keys(VEHICLE_CLASS_CONFIG), 'Select a valid vehicle type'),

  registrationNo: Yup.string()
    .trim()
    .transform((value, originalValue) => (originalValue === '' ? undefined : value))
    .required('Registration number is required')
    .max(50, 'Registration number must be 50 characters or fewer')
    .matches(
      SRI_LANKAN_VEHICLE_REG_REGEX,
      'Enter a valid Sri Lankan vehicle registration number (e.g. WP CAB-1234, WP-CAD-1020, or CAB-5678)',
    ),

  capacityKg: Yup.number()
    .transform((value, originalValue) =>
      originalValue === '' || originalValue === null || originalValue === undefined
        ? undefined
        : Number(originalValue),
    )
    .typeError('Capacity must be a valid number')
    .required('Capacity is required')
    .positive('Capacity must be greater than 0')
    .when('vehicleType', ([vehicleType], schema) => {
      const config = VEHICLE_CLASS_CONFIG[vehicleType]
      if (!config) {
        return schema.max(100000, 'Capacity cannot exceed 100,000 kg')
      }
      return schema
        .min(
          config.minCapacityKg,
          `Capacity for ${config.label} must be at least ${config.minCapacityKg.toLocaleString()} kg`,
        )
        .max(
          config.maxCapacityKg,
          `Capacity for ${config.label} cannot exceed ${config.maxCapacityKg.toLocaleString()} kg`,
        )
    }),

  volumeM3: Yup.number()
    .transform((value, originalValue) =>
      originalValue === '' || originalValue === null || originalValue === undefined
        ? undefined
        : Number(originalValue),
    )
    .typeError('Volume must be a valid number')
    .required('Volume is required')
    .positive('Volume must be greater than 0')
    .max(1000, 'Volume cannot exceed 1,000 m³'),
})
