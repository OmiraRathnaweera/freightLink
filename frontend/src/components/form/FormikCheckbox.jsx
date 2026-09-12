import ErrorMessage from './ErrorMessage.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'
import { cx } from '../../lib/cx.js'

/**
 * Checkbox wired to Formik. Uses `useField`'s `type: 'checkbox'` mode,
 * which gives back `field.checked` instead of `field.value` (see Formik's
 * `useField` docs). Doesn't reuse `FormField` — that layout puts the label
 * above the control, but a checkbox's label sits beside it — so it composes
 * its own row + hint/error markup instead.
 *
 * @param {string} name - required, matches a key in Formik's `initialValues`.
 * @param {string} [label] - trailing label text, rendered beside the box.
 * @param {boolean} [disabled]
 * @param {string} [className] - passed to the wrapping `<div>`.
 * @param {string} [helperText] - hint shown below the row when there's no error.
 */
function FormikCheckbox({ name, label, disabled, className, helperText, ...rest }) {
  const { field, meta, showError, id } = useFieldMeta(name, 'checkbox')
  const errorId = showError ? `${id}-error` : undefined
  const hintId = !showError && helperText ? `${id}-hint` : undefined

  return (
    <div className={className}>
      <label htmlFor={id} className="flex items-center gap-2 text-body-md text-on-surface">
        <input
          {...field}
          {...rest}
          id={id}
          type="checkbox"
          disabled={disabled}
          aria-invalid={showError}
          aria-describedby={errorId ?? hintId}
          className={cx(
            'h-4 w-4 rounded border-slate-300 text-primary focus:outline-none focus:ring-2 focus:ring-slate-border',
            showError && 'border-status-red-text',
          )}
        />
        {label}
      </label>
      {showError ? (
        <ErrorMessage id={errorId}>{meta.error}</ErrorMessage>
      ) : (
        helperText && (
          <p id={hintId} className="mt-1 text-body-md text-on-surface-variant">
            {helperText}
          </p>
        )
      )}
    </div>
  )
}

export default FormikCheckbox
