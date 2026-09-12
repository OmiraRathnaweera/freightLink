import Input from '../Input.jsx'
import FormField from './FormField.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'

/**
 * Single-line text input wired to Formik via `name`. Renders a label, the
 * input, and an error message (shown once the field is touched or the form
 * is submitted) through `FormField`.
 *
 * @param {string} name - required, matches a key in Formik's `initialValues`.
 * @param {string} [label] - field label text.
 * @param {string} [placeholder]
 * @param {boolean} [disabled]
 * @param {string} [className] - passed to the underlying `Input`, not the wrapper.
 * @param {string} [helperText] - hint shown below the field when there's no error.
 * @param {string} [type='text'] - any native text-like input type ("email", "tel", "url", ...).
 *   For passwords use `FormikPasswordField` (adds a show/hide toggle); for
 *   numbers use `FormikNumberField` (stores a real Number in Formik state).
 */
function FormikTextField({ name, label, placeholder, disabled, className, helperText, type = 'text', ...rest }) {
  const { field, meta, showError, id } = useFieldMeta(name)
  const errorId = showError ? `${id}-error` : undefined

  return (
    <FormField id={id} label={label} hint={helperText} error={showError ? meta.error : undefined}>
      <Input
        {...field}
        {...rest}
        id={id}
        type={type}
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

export default FormikTextField
