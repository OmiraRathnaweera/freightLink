import { useFormikContext } from 'formik'
import DualLocationPicker from '../map/DualLocationPicker.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'

/**
 * Formik wiring around the Formik-agnostic `DualLocationPicker`. Reads/
 * writes six flat Formik fields (pickup + dropoff, each address/lat/lng) —
 * the same fields the backend contract and `validationSchemas.js` already
 * expect, so no payload or schema changes are needed.
 *
 * @param {string} pickupAddressName
 * @param {string} pickupLatName
 * @param {string} pickupLngName
 * @param {string} dropoffAddressName
 * @param {string} dropoffLatName
 * @param {string} dropoffLngName
 */
function FormikDualLocationField({
  pickupAddressName,
  pickupLatName,
  pickupLngName,
  dropoffAddressName,
  dropoffLatName,
  dropoffLngName,
}) {
  const { values, touched, setValues, setTouched } = useFormikContext()
  const pickupAddressField = useFieldMeta(pickupAddressName)
  const pickupLatField = useFieldMeta(pickupLatName)
  const pickupLngField = useFieldMeta(pickupLngName)
  const dropoffAddressField = useFieldMeta(dropoffAddressName)
  const dropoffLatField = useFieldMeta(dropoffLatName)
  const dropoffLngField = useFieldMeta(dropoffLngName)

  // Precedence per side: address error, then lat, then lng — the cross-field
  // "pickup and dropoff cannot be identical" check (validationSchemas.js)
  // attaches its error to dropoffLat, so it naturally surfaces on the
  // dropoff side.
  const pickupError =
    (pickupAddressField.showError && pickupAddressField.meta.error) ||
    (pickupLatField.showError && pickupLatField.meta.error) ||
    (pickupLngField.showError && pickupLngField.meta.error) ||
    undefined
  const dropoffError =
    (dropoffAddressField.showError && dropoffAddressField.meta.error) ||
    (dropoffLatField.showError && dropoffLatField.meta.error) ||
    (dropoffLngField.showError && dropoffLngField.meta.error) ||
    undefined

  // A single merged setValues per side (rather than three separate
  // setFieldValue calls) so exactly one validation pass runs against the
  // fully-updated address/lat/lng — see FormikLocationField's history for
  // why three separate calls raced Formik's async per-call validation.
  function handlePickupChange({ address, lat, lng }) {
    setValues({ ...values, [pickupAddressName]: address, [pickupLatName]: lat, [pickupLngName]: lng })
    setTouched({ ...touched, [pickupAddressName]: true, [pickupLatName]: true, [pickupLngName]: true }, false)
  }

  function handleDropoffChange({ address, lat, lng }) {
    setValues({ ...values, [dropoffAddressName]: address, [dropoffLatName]: lat, [dropoffLngName]: lng })
    setTouched({ ...touched, [dropoffAddressName]: true, [dropoffLatName]: true, [dropoffLngName]: true }, false)
  }

  return (
    <DualLocationPicker
      pickupId={pickupAddressField.id}
      dropoffId={dropoffAddressField.id}
      pickupAddress={pickupAddressField.field.value}
      pickupLat={pickupLatField.field.value}
      pickupLng={pickupLngField.field.value}
      dropoffAddress={dropoffAddressField.field.value}
      dropoffLat={dropoffLatField.field.value}
      dropoffLng={dropoffLngField.field.value}
      onPickupChange={handlePickupChange}
      onDropoffChange={handleDropoffChange}
      pickupError={pickupError}
      dropoffError={dropoffError}
    />
  )
}

export default FormikDualLocationField
