import * as Yup from 'yup'

// Existing login schema matching the { email, password } credentials shape
export const loginSchema = Yup.object({
  email: Yup.string().email('Enter a valid email address').required('Email is required'),
  password: Yup.string().min(8, 'Password must be at least 8 characters').required('Password is required'),
})

export const forgotPasswordSchema = Yup.object({
  email: Yup.string().trim().email('Enter a valid email address').required('Email is required'),
})

export const resetPasswordSchema = Yup.object({
  password: Yup.string()
    .min(8, 'Password must be at least 8 characters')
    .max(100, 'Password must be 100 characters or fewer')
    .matches(/[A-Z]/, 'Password must contain at least one uppercase letter')
    .matches(/[a-z]/, 'Password must contain at least one lowercase letter')
    .matches(/[0-9]/, 'Password must contain at least one number')
    .matches(/[^A-Za-z0-9]/, 'Password must contain at least one special character')
    .required('Password is required'),
  confirmPassword: Yup.string()
    .oneOf([Yup.ref('password')], 'Passwords must match')
    .required('Confirm your password'),
})

// Shared password validation rules
// Enforces minimum 8 characters, upper, lower, digit, and special character
const passwordSchema = Yup.string()
  .min(8, 'Password must be at least 8 characters')
  .max(100, 'Password must be 100 characters or fewer')
  .matches(/[A-Z]/, 'Password must contain at least one uppercase letter')
  .matches(/[a-z]/, 'Password must contain at least one lowercase letter')
  .matches(/[0-9]/, 'Password must contain at least one number')
  .matches(/[^A-Za-z0-9]/, 'Password must contain at least one special character')
  .required('Password is required')

// Shared email validation
const emailSchema = Yup.string()
  .trim()
  .email('Enter a valid email address')
  .max(254, 'Email must be 254 characters or fewer')
  .required('Email is required')

// Shared full name validation (letters, spaces, hyphens, periods, apostrophes for initials/names)
const fullNameSchema = Yup.string()
  .trim()
  .min(2, 'Full name must be at least 2 characters')
  .max(100, 'Full name must be 100 characters or fewer')
  .matches(/^[a-zA-Z\s.'-]+$/, 'Full name can only contain letters, spaces, hyphens, periods, and apostrophes')
  .required('Full name is required')

// Optional string transformer helper: converts empty or whitespace-only strings to undefined so .optional() works smoothly
function optionalString(schema) {
  return schema
    .trim()
    .transform((value, originalValue) =>
      typeof originalValue === 'string' && originalValue.trim() === '' ? undefined : value,
    )
    .optional()
}

// Coordinate number field validation helper
function coordinateField({ label, min, max }) {
  return Yup.number()
    .transform((value, originalValue) =>
      originalValue === '' || originalValue === null || originalValue === undefined
        ? undefined
        : Number(originalValue),
    )
    .typeError(`${label} must be a valid number`)
    .min(min, `${label} must be at least ${min}`)
    .max(max, `${label} must be at most ${max}`)
    .required(`${label} is required`)
}

// E.164 phone format validation helper (e.g. +94771234567 or +14155552671)
const phoneE164Schema = optionalString(
  Yup.string().matches(
    /^\+[1-9]\d{6,14}$/,
    'Phone number must be in E.164 format with country code (e.g. +94771234567 or +14155552671)',
  ),
)

/**
 * Shipper registration schema
 * Validates every input in RegisterShipperRequest (API contract Section 4.1):
 * required: [email, password, fullName, companyName, billingAddress]
 * optional: [phoneE164, businessRegNo]
 */
export const shipperRegisterSchema = Yup.object({
  fullName: fullNameSchema,
  email: emailSchema,
  password: passwordSchema,
  phoneE164: phoneE164Schema,
  companyName: Yup.string()
    .trim()
    .min(2, 'Company name must be at least 2 characters')
    .max(150, 'Company name must be 150 characters or fewer')
    .required('Company name is required'),
  businessRegNo: optionalString(
    Yup.string()
      .min(2, 'Business registration number must be at least 2 characters')
      .max(50, 'Business registration number must be 50 characters or fewer')
      .matches(
        /^[a-zA-Z0-9\-_/ ]+$/,
        'Business registration number can only contain letters, numbers, hyphens, and slashes',
      ),
  ),
  billingAddress: Yup.string()
    .trim()
    .min(5, 'Billing address must be at least 5 characters')
    .max(500, 'Billing address must be 500 characters or fewer')
    .required('Billing address is required'),
})

/**
 * Agency registration schema
 * Validates every input in RegisterAgencyRequest (API contract Section 4.1):
 * required: [email, password, fullName, agencyName, businessRegNo, yardAddress, yardLat, yardLng]
 * optional: [phoneE164, jobTitle]
 */
export const agencyRegisterSchema = Yup.object({
  fullName: fullNameSchema,
  email: emailSchema,
  password: passwordSchema,
  phoneE164: phoneE164Schema,
  jobTitle: optionalString(
    Yup.string()
      .min(2, 'Job title must be at least 2 characters')
      .max(100, 'Job title must be 100 characters or fewer'),
  ),
  agencyName: Yup.string()
    .trim()
    .min(2, 'Agency name must be at least 2 characters')
    .max(150, 'Agency name must be 150 characters or fewer')
    .required('Agency name is required'),
  businessRegNo: Yup.string()
    .trim()
    .min(2, 'Business registration number must be at least 2 characters')
    .max(50, 'Business registration number must be 50 characters or fewer')
    .matches(
      /^[a-zA-Z0-9\-_/ ]+$/,
      'Business registration number can only contain letters, numbers, hyphens, and slashes',
    )
    .required('Business registration number is required'),
  yardAddress: Yup.string()
    .trim()
    .min(5, 'Yard address must be at least 5 characters')
    .max(500, 'Yard address must be 500 characters or fewer')
    .required('Yard address is required'),
  yardLat: coordinateField({ label: 'Yard latitude', min: -90, max: 90 }),
  yardLng: coordinateField({ label: 'Yard longitude', min: -180, max: 180 }),
})
