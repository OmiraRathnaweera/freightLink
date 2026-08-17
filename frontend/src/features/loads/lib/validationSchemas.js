import * as Yup from 'yup'

// Client-side mirror of CreateLoadDto/UpdateLoadDto's exact constraints
// (docs/load-management-api.md Section 3.1) — the backend is still the
// real enforcement point, but matching its rules here means most invalid
// submissions never reach the network.

function numberField({ label, min, max }) {
  return Yup.number()
    // Formik's initial value for an empty numeric field is '' (see
    // FormikNumberField) — without this transform, Yup's number cast turns
    // '' into NaN and reports "must be a number" instead of "required".
    .transform((value, originalValue) => (originalValue === '' ? undefined : value))
    .typeError(`${label} must be a number`)
    .min(min, `${label} must be at least ${min}`)
    .max(max, `${label} must be ${max} or less`)
    .required(`${label} is required`)
}

function addressField(label) {
  return Yup.string()
    .trim()
    .min(5, `${label} must be at least 5 characters`)
    .max(500, `${label} must be 500 characters or fewer`)
    .required(`${label} is required`)
}

// Both createLoadSchema and editLoadSchema spread this so the field rules
// are defined exactly once — editLoadSchema just omits `postImmediately`.
const sharedLoadFields = {
  cargoDescription: Yup.string()
    .trim()
    .min(3, 'Cargo description must be at least 3 characters')
    .max(1000, 'Cargo description must be 1000 characters or fewer')
    .required('Cargo description is required'),
  weightKg: numberField({ label: 'Weight', min: 0.01, max: 99999999.99 }),
  volumeM3: numberField({ label: 'Volume', min: 0.001, max: 9999999.999 }),
  pickupAddress: addressField('Pickup address'),
  pickupLat: numberField({ label: 'Pickup latitude', min: -90, max: 90 }),
  pickupLng: numberField({ label: 'Pickup longitude', min: -180, max: 180 }),
  dropoffAddress: addressField('Dropoff address'),
  dropoffLat: numberField({ label: 'Dropoff latitude', min: -90, max: 90 }),
  dropoffLng: numberField({ label: 'Dropoff longitude', min: -180, max: 180 }),
  pickupWindowStart: Yup.string().required('Pickup window start is required'),
  pickupWindowEnd: Yup.string()
    .required('Pickup window end is required')
    .test('after-start', 'Pickup window end must be after the start', function test(value) {
      const { pickupWindowStart } = this.parent
      if (!value || !pickupWindowStart) return true
      return new Date(value) > new Date(pickupWindowStart)
    }),
}

// Mirrors LOAD_PICKUP_DROPOFF_IDENTICAL — rejects pickup/dropoff
// coordinates that are exactly equal, same rule the backend enforces.
function rejectIdenticalPickupDropoff(schema) {
  return schema.test('pickup-dropoff-identical', function test(values) {
    if (!values) return true
    const { pickupLat, pickupLng, dropoffLat, dropoffLng } = values
    if (pickupLat === dropoffLat && pickupLng === dropoffLng) {
      return this.createError({
        path: 'dropoffLat',
        message: 'Pickup and dropoff locations cannot be identical',
      })
    }
    return true
  })
}

export const createLoadSchema = rejectIdenticalPickupDropoff(
  Yup.object({
    ...sharedLoadFields,
    postImmediately: Yup.boolean().default(false),
  }),
)

export const editLoadSchema = rejectIdenticalPickupDropoff(Yup.object(sharedLoadFields))

export const cancelLoadSchema = Yup.object({
  reason: Yup.string()
    .trim()
    .max(500, 'Reason must be 500 characters or fewer')
    .required('A cancellation reason is required'),
})
