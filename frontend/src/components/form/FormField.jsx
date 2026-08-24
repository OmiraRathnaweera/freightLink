import ErrorMessage from './ErrorMessage.jsx'

/**
 * Label + control slot + hint/error layout, shared by every `Formik*Field`
 * component in this folder. Deliberately has no Formik/Yup knowledge of its
 * own (just id/label/hint/error/children props) so the layout stays
 * reusable even outside a Formik context.
 *
 * Not the same component as `src/features/loads/components/FormField.jsx`
 * — that one backs the Loads pages' plain uncontrolled inputs and is left
 * as-is; this is the Formik-aware sibling for the shared field kit.
 *
 * @param {string} [id] - passed to the label's `htmlFor`; the input itself
 *   must be given the same `id` by the caller.
 * @param {string} [label]
 * @param {string} [hint] - shown below the field; hidden whenever `error` is set.
 * @param {string} [error] - shown below the field instead of `hint` when present.
 * @param {boolean} [required=false] - renders a red `*` after the label.
 * @param {import('react').ReactNode} children - the actual input control.
 * @param {string} [className] - passed to the wrapping `<div>`.
 */
function FormField({ id, label, hint, error, required = false, children, className }) {
  const errorId = error ? `${id}-error` : undefined
  const hintId = !error && hint ? `${id}-hint` : undefined

  return (
    <div className={className}>
      {label && (
        <label htmlFor={id} className="mb-1.5 block text-body-md font-semibold text-on-surface">
          {label}
          {required && <span className="text-status-red-text"> *</span>}
        </label>
      )}
      {children}
      {error ? (
        <ErrorMessage id={errorId}>{error}</ErrorMessage>
      ) : (
        hint && (
          <p id={hintId} className="mt-1 text-body-md text-on-surface-variant">
            {hint}
          </p>
        )
      )}
    </div>
  )
}

export default FormField
