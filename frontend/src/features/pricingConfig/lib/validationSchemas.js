import * as Yup from 'yup'

// Minimal client-side checks only (required fields, numeric-only) — the
// backend's DataAnnotations + service-layer checks (payload-band overlap,
// positive-price CHECK constraints, etc.) remain the source of truth, per
// this feature's scope.

function numberField({ label, min, max }) {
  let schema = Yup.number()
    // Formik's initial value for an empty numeric field is '' (see
    // FormikNumberField) — without this transform, Yup's number cast turns
    // '' into NaN and reports "must be a number" instead of "required".
    .transform((value, originalValue) => (originalValue === '' ? undefined : value))
    .typeError(`${label} must be a number`)
    .min(min, `${label} must be at least ${min}`)
  if (max !== undefined) {
    schema = schema.max(max, `${label} must be at most ${max}`)
  }
  return schema.required(`${label} is required`)
}

export const fuelRateSchema = Yup.object({
  fuelType: Yup.string().required('Fuel type is required'),
  pricePerLitre: numberField({ label: 'Price per litre', min: 0.01 }),
  source: Yup.string().trim().max(500, 'Source must be 500 characters or fewer').required('Source is required'),
  effectiveFrom: Yup.string().required('Effective date is required'),
})

export const vehicleEfficiencySchema = Yup.object({
  classLabel: Yup.string().required('Vehicle class is required'),
  minPayloadKg: numberField({ label: 'Min payload', min: 0 }),
  // Left blank for the open-ended top tier (e.g. ContainerTruck).
  maxPayloadKg: Yup.number()
    .transform((value, originalValue) => (originalValue === '' ? undefined : value))
    .typeError('Max payload must be a number')
    .min(0, 'Max payload must be at least 0')
    .test('greater-than-min', 'Max payload must be greater than min payload', function test(value) {
      if (value === undefined) return true
      const { minPayloadKg } = this.parent
      if (minPayloadKg === undefined || minPayloadKg === '') return true
      return value > minPayloadKg
    })
    .optional(),
  minVolumeM3: numberField({ label: 'Min volume', min: 0 }),
  // Left blank for the open-ended top tier, same as maxPayloadKg.
  maxVolumeM3: Yup.number()
    .transform((value, originalValue) => (originalValue === '' ? undefined : value))
    .typeError('Max volume must be a number')
    .min(0, 'Max volume must be at least 0')
    .test('greater-than-min', 'Max volume must be greater than min volume', function test(value) {
      if (value === undefined) return true
      const { minVolumeM3 } = this.parent
      if (minVolumeM3 === undefined || minVolumeM3 === '') return true
      return value > minVolumeM3
    })
    .optional(),
  fuelConsumptionLPer100Km: numberField({ label: 'Fuel consumption', min: 0.01 }),
  source: Yup.string().trim().max(500, 'Source must be 500 characters or fewer').required('Source is required'),
  effectiveFrom: Yup.string().required('Effective date is required'),
})

export const pricingFormulaSchema = Yup.object({
  baseFare: numberField({ label: 'Base fare', min: 0 }),
  ratePerKg: numberField({ label: 'Rate per kg', min: 0 }),
  driverCostPerKm: numberField({ label: 'Driver cost per km', min: 0 }),
  maintenanceAllowancePerKm: numberField({ label: 'Maintenance allowance per km', min: 0 }),
  // A fraction, not a whole percent (0.15 = 15%) — see PricingFormulaForm's
  // helper text — so it can't go past 1 (100%).
  marginPercent: numberField({ label: 'Margin', min: 0, max: 1 }),
  source: Yup.string().trim().max(500, 'Source must be 500 characters or fewer').required('Source is required'),
  effectiveFrom: Yup.string().required('Effective date is required'),
})
