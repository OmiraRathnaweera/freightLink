import { useFormikContext } from 'formik'
import Button from '../Button.jsx'

/**
 * Submit button that disables itself while Formik's `handleSubmit` is in
 * flight, so a slow validation/submit can't be double-clicked. Wraps the
 * existing `Button` primitive rather than reinventing button styling. Must
 * be rendered inside a `<Formik>`/`<Form>` tree.
 *
 * @param {import('react').ReactNode} [children='Submit']
 * @param {boolean} [disabled] - ORed with Formik's `isSubmitting`.
 * @param {...*} props - forwarded to `Button` (e.g. `className`, `variant`).
 */
function FormikSubmitButton({ children = 'Submit', disabled, ...props }) {
  const { isSubmitting } = useFormikContext()

  return (
    <Button type="submit" variant="primary" disabled={isSubmitting || disabled} {...props}>
      {children}
    </Button>
  )
}

export default FormikSubmitButton
