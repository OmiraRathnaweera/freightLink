import { ChevronDown } from 'lucide-react'
import FormField from './FormField.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'
import { cx } from '../../lib/cx.js'

/**
 * Native `<select>` wired to Formik.
 *
 * @param {string} name - required, matches a key in Formik's `initialValues`.
 * @param {string} [label] - field label text.
 * @param {string} [placeholder] - rendered as a disabled, pre-selected option.
 * @param {boolean} [disabled]
 * @param {string} [className] - passed to the underlying `<select>`, not the wrapper.
 * @param {string} [helperText] - hint shown below the field when there's no error.
 * @param {{value: string, label: string}[]} [options] - the common case; renders
 *   an `<option>` per entry.
 * @param {import('react').ReactNode} [children] - alternative to `options` for
 *   anything more custom (e.g. `<optgroup>`s) — composition over a growing
 *   prop list (.claude/rules/frontend-design.md #2).
 */
function FormikSelect({
  name,
  label,
  placeholder,
  disabled,
  className,
  helperText,
  options,
  children,
  ...rest
}) {
  const { field, meta, showError, id } = useFieldMeta(name)
  const errorId = showError ? `${id}-error` : undefined

  return (
    <FormField id={id} label={label} hint={helperText} error={showError ? meta.error : undefined}>
      <div className="relative">
        <select
          {...field}
          {...rest}
          id={id}
          disabled={disabled}
          aria-invalid={showError}
          aria-describedby={errorId}
          className={cx(
            'w-full appearance-none rounded-md border bg-white px-3 py-2 pr-9 text-[15px] text-on-surface',
            'disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-on-surface-variant',
            showError
              ? 'border-status-red-text focus:border-status-red-text focus:outline-none focus:ring-2 focus:ring-status-red-bg'
              : 'border-slate-300 focus:border-primary focus:outline-none focus:ring-2 focus:ring-slate-border',
            className,
          )}
        >
          {placeholder && (
            <option value="" disabled>
              {placeholder}
            </option>
          )}
          {options
            ? options.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))
            : children}
        </select>
        <ChevronDown
          className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-on-surface-variant"
          strokeWidth={1.5}
        />
      </div>
    </FormField>
  )
}

export default FormikSelect
