import { useFormikContext } from 'formik'
import YardLocationPicker from '../map/YardLocationPicker.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'

/**
 * Formik wiring around YardLocationPicker. Reads/writes three flat Formik
 * fields (address, lat, lng) for an agency depot yard.
 *
 * @param {string} [addressName='yardAddress'] - Formik field name for address string
 * @param {string} [latName='yardLat'] - Formik field name for latitude number
 * @param {string} [lngName='yardLng'] - Formik field name for longitude number
 * @param {string} [label='Yard Depot Location & Coordinates'] - Field label text
 */
function FormikYardLocationField({
  addressName = 'yardAddress',
  latName = 'yardLat',
  lngName = 'yardLng',
  label = 'Yard Depot Location & Coordinates',
}) {
  const { values, touched, setValues, setTouched } = useFormikContext()
  const addressField = useFieldMeta(addressName)
  const latField = useFieldMeta(latName)
  const lngField = useFieldMeta(lngName)

  const addressError = addressField.showError ? addressField.meta.error : undefined
  const latError = latField.showError ? latField.meta.error : undefined
  const lngError = lngField.showError ? lngField.meta.error : undefined

  function handleChange({ address, lat, lng }) {
    setValues({
      ...values,
      [addressName]: address,
      [latName]: lat,
      [lngName]: lng,
    })
    setTouched(
      {
        ...touched,
        [addressName]: true,
        [latName]: true,
        [lngName]: true,
      },
      false,
    )
  }

  return (
    <YardLocationPicker
      id={addressField.id}
      label={label}
      address={values[addressName] ?? ''}
      lat={values[latName] ?? ''}
      lng={values[lngName] ?? ''}
      onChange={handleChange}
      addressError={addressError}
      latError={latError}
      lngError={lngError}
    />
  )
}

export default FormikYardLocationField
