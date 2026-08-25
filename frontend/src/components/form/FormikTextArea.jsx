import Textarea from '../Textarea.jsx'
import FormField from './FormField.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'

/**
 * Multi-line text field wired to Formik — same recipe as `FormikTextField`,
 * backed by the `Textarea` primitive instead of `Input`.
 *
 * @param {string} name - required, matches a key in Formik's `initialValues`.
 * @param {string} [label] - field label text.
 * @param {string} [placeholder]
 * @param {boolean} [disabled]
 * @param {string} [className] - passed to the underlying `Textarea`, not the wrapper.
 * @param {string} [helperText] - hint shown below the field when there's no error.
 * @param {number} [rows=3]
 */
function FormikTextArea({ name, label, placeholder, disabled, className, helperText, rows = 3, ...rest }) {
  const { field, meta, showError, id } = useFieldMeta(name)
  const errorId = showError ? `${id}-error` : undefined

  return (
    <FormField id={id} label={label} hint={helperText} error={showError ? meta.error : undefined}>
      <Textarea
        {...field}
        {...rest}
        id={id}
        rows={rows}
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

export default FormikTextArea
