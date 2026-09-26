import Input from '../Input.jsx'
import FormField from './FormField.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'

/**
 * Numeric input wired to Formik. Unlike `FormikTextField`, this stores a
 * real `Number` in Formik state (not the raw input string) so a Yup
 * `.number()` schema validates the actual value being submitted, not a
 * stringified one.
 *
 * @param {string} name - required, matches a key in Formik's `initialValues`.
 * @param {string} [label] - field label text.
 * @param {string} [placeholder]
 * @param {boolean} [disabled]
 * @param {string} [className] - passed to the underlying `Input`, not the wrapper.
 * @param {string} [helperText] - hint shown below the field when there's no error.
 * @param {boolean} [mono=false] - render the value in JetBrains Mono; matches
 *   `Input`'s existing convention for money/weight/distance fields (DESIGN.md
 *   "Monospace Fields").
 */
function FormikNumberField({ name, label, placeholder, disabled, className, helperText, mono = false, required = false, ...rest }) {
  const { field, meta, showError, id } = useFieldMeta(name)
  const errorId = showError ? `${id}-error` : undefined

  const handleChange = (event) => {
    const raw = event.target.value
    field.onChange({ target: { name, value: raw === '' ? '' : Number(raw) } })
  }

  return (
    <FormField id={id} label={label} hint={helperText} error={showError ? meta.error : undefined} required={required}>
      <Input
        {...rest}
        id={id}
        name={name}
        type="number"
        mono={mono}
        value={field.value ?? ''}
        onChange={handleChange}
        onBlur={field.onBlur}
        placeholder={placeholder}
        disabled={disabled}
        className={className}
        error={showError}
        aria-invalid={showError}
        aria-describedby={errorId}
      />
    </FormField>
  )
}

export default FormikNumberField
