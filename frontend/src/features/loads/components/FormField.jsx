// Label + control + inline error wrapper, reused by Post a Load and Edit
// Load — both forms repeat this same label/error markup per field.
function FormField({ label, error, children, className }) {
  return (
    <div className={className}>
      <label className="mb-1.5 block text-body-md font-semibold text-on-surface">{label}</label>
      {children}
      {error && <p className="mt-1 text-body-md text-status-red-text">{error}</p>}
    </div>
  )
}

export default FormField
