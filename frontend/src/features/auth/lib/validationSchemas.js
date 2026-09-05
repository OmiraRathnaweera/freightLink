import * as Yup from 'yup'

// Example schema demonstrating how a feature pairs its own Yup schema with
// the shared Formik field kit (src/components/form/) — the components stay
// schema-agnostic; validation rules live here, next to the feature that
// owns the form. Matches the { email, password } shape the `login` thunk
// already expects (src/features/auth/store/authSlice.js).
export const loginSchema = Yup.object({
  email: Yup.string().email('Enter a valid email').required('Email is required'),
  password: Yup.string().min(8, 'Password must be at least 8 characters').required('Password is required'),
})
