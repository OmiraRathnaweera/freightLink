import { useState } from 'react'
import { Eye, EyeOff } from 'lucide-react'
import Input from '../Input.jsx'
import FormField from './FormField.jsx'
import { useFieldMeta } from '../../hooks/useFieldMeta.js'

/**
 * Password input wired to Formik, with an optional show/hide toggle. The
 * toggle's own open/closed state is local UI state (not Formik-related),
 * so it's a plain `useState` here (.claude/rules/frontend-design.md #4).
 *
 * @param {string} name - required, matches a key in Formik's `initialValues`.
 * @param {string} [label] - field label text.
 * @param {string} [placeholder]
 * @param {boolean} [disabled]
 * @param {string} [className] - passed to the underlying `Input`, not the wrapper.
 * @param {string} [helperText] - hint shown below the field when there's no error.
 * @param {boolean} [showToggle=true] - set false to hide the show/hide eye icon.
 */
function FormikPasswordField({ name, label, placeholder, disabled, className, helperText, showToggle = true, ...rest }) {
  const { field, meta, showError, id } = useFieldMeta(name)
  const [isVisible, setIsVisible] = useState(false)
  const errorId = showError ? `${id}-error` : undefined

  return (
    <FormField id={id} label={label} hint={helperText} error={showError ? meta.error : undefined}>
      <div className="relative">
        <Input
          {...field}
          {...rest}
          id={id}
          type={isVisible ? 'text' : 'password'}
          placeholder={placeholder}
          disabled={disabled}
          className={showToggle ? `pr-10 ${className ?? ''}` : className}
          error={showError}
          aria-invalid={showError}
          aria-describedby={errorId}
        />
        {showToggle && (
          <button
            type="button"
            onClick={() => setIsVisible((prev) => !prev)}
            disabled={disabled}
            aria-label={isVisible ? 'Hide password' : 'Show password'}
            className="absolute right-2 top-1/2 flex h-7 w-7 -translate-y-1/2 items-center justify-center rounded text-on-surface-variant hover:text-on-surface disabled:cursor-not-allowed"
          >
            {isVisible ? <EyeOff className="h-4 w-4" strokeWidth={1.5} /> : <Eye className="h-4 w-4" strokeWidth={1.5} />}
          </button>
        )}
      </div>
    </FormField>
  )
}

export default FormikPasswordField
