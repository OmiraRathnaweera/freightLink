/**
 * Standalone error text — the semantic-red error convention DESIGN.md's
 * Input Fields spec implies (see `src/components/Input.jsx`'s `error`
 * prop). Renders nothing when there's no message, so callers can pass it
 * unconditionally. Reused internally by `FormField`.
 *
 * @param {string} [id] - matched by a field's `aria-describedby`.
 * @param {import('react').ReactNode} children - the error message; renders nothing if falsy.
 */
function ErrorMessage({ id, children }) {
  if (!children) return null
  return (
    <p id={id} className="mt-1 text-body-md text-status-red-text">
      {children}
    </p>
  )
}

export default ErrorMessage
