import Input from '../Input.jsx'
import FormField from './FormField.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'

/**
 * Date+time input wired to Formik, via the native
 * `<input type="datetime-local">` — no calendar-picker library is installed
 * in this project, so this is the practical version rather than a rich
 * picker. Value must be a string in "YYYY-MM-DDTHH:mm" format (the format
 * the native control itself produces and expects).
 *
 * @param {string} name - required, matches a key in Formik's `initialValues`.
 * @param {string} [label] - field label text.
 * @param {boolean} [disabled]
 * @param {string} [className] - passed to the underlying `Input`, not the wrapper.
 * @param {string} [helperText] - hint shown below the field when there's no error.
 */
function FormikDateTimeField({ name, label, disabled, className, helperText, ...rest }) {
  const { field, meta, showError, id } = useFieldMeta(name)
  const errorId = showError ? `${id}-error` : undefined

  return (
    <FormField id={id} label={label} hint={helperText} error={showError ? meta.error : undefined}>
      <Input
        {...field}
        {...rest}
        id={id}
        type="datetime-local"
        disabled={disabled}
        className={className}
        error={showError}
        aria-invalid={showError}
        aria-describedby={errorId}
      />
    </FormField>
  )
}

export default FormikDateTimeField
