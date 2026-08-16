import { useField } from 'formik'

/**
 * Derives the three things every field in `src/components/form/` needs
 * from Formik's `useField`, so each field component doesn't repeat the
 * same three lines: the `field`/`meta` pair, a stable `id` (for label
 * `htmlFor` / `aria-describedby`), and whether the error should be visible
 * yet (standard Formik UX: only after the field has been touched, which
 * Formik also does to every field on submit).
 *
 * @param {string} name - Formik field name (matches a key in `initialValues`).
 * @param {'checkbox'|'radio'} [type] - pass 'checkbox' for FormikCheckbox,
 *   which needs `field.checked` instead of `field.value` (see Formik's
 *   `useField` docs) — omit for every other field type.
 * @returns {{ field: object, meta: object, showError: boolean, id: string }}
 */
export function useFieldMeta(name, type) {
  const [field, meta] = useField(type ? { name, type } : name)
  const showError = meta.touched && Boolean(meta.error)
  const id = `field-${name}`
  return { field, meta, showError, id }
}
