// Single source of truth for every status / role / type string used across
// the app. Import from here instead of re-declaring these strings in a
// slice, a component, or an API layer — if a value needs to change, it
// changes in exactly one place. Every export is Object.freeze()'d so a
// stray `SomeEnum.FOO = 'bar'` fails silently in non-strict code and throws
// in strict/module code (all ES modules are strict by default), rather than
// mutating shared state at runtime.

// --- User & Auth ---
// Matches the four roles the backend issues in the JWT / user record.
export const UserRole = Object.freeze({
  SHIPPER: 'Shipper',
  AGENCY_STAFF: 'AgencyStaff',
  DRIVER: 'Driver',
  ADMIN: 'Admin',
})

// --- Component A: Load ---
// Lifecycle of a shipper's load, from creation to close-out.
export const LoadStatus = Object.freeze({
  DRAFT: 'Draft',
  POSTED: 'Posted',
  MATCHED: 'Matched',
  IN_TRANSIT: 'InTransit',
  DELIVERED: 'Delivered',
  CLOSED: 'Closed',
  CANCELLED: 'Cancelled',
})

// Lifecycle of a manual price proposal an Agency sends on a Posted load, alongside (not through)
// the multi-agent matching pipeline.
export const LoadProposalStatus = Object.freeze({
  PENDING: 'Pending',
  ACCEPTED: 'Accepted',
  REJECTED: 'Rejected',
  WITHDRAWN: 'Withdrawn',
})

// Kind of file attached to a load.
export const FileType = Object.freeze({
  MANIFEST: 'Manifest',
  INVOICE: 'Invoice',
  CARGO_PHOTO: 'CargoPhoto',
  OTHER: 'Other',
})

// --- Component B: Agency & Fleet ---
// Onboarding/standing state of a freight agency.
export const AgencyStatus = Object.freeze({
  PENDING: 'Pending',
  VERIFIED: 'Verified',
  ACTIVE: 'Active',
  SUSPENDED: 'Suspended',
})

// Operational state of a vehicle in an agency's fleet.
export const VehicleStatus = Object.freeze({
  AVAILABLE: 'Available',
  IN_USE: 'InUse',
  MAINTENANCE: 'Maintenance',
  INACTIVE: 'Inactive',
})

// Physical body/chassis type of a fleet vehicle (matches backend VehicleType enum).
export const VehicleType = Object.freeze({
  LORRY: 'Lorry',
  CONTAINER: 'Container',
  REFRIGERATED: 'Refrigerated',
  FLAT_BED: 'FlatBed',
  TIPPER: 'Tipper',
})

// Employment/availability state of a driver.
export const DriverStatus = Object.freeze({
  ACTIVE: 'Active',
  INACTIVE: 'Inactive',
  SUSPENDED: 'Suspended',
})

// Review state of a compliance document (license, insurance, etc.), set by
// the admin verification-queue workflow. Matches the backend's
// ComplianceDocStatus enum exactly — this previously drifted (Valid/
// Expiring/Expired) from what the API actually returns.
export const ComplianceDocStatus = Object.freeze({
  PENDING: 'Pending',
  VERIFIED: 'Verified',
  REJECTED: 'Rejected',
  EXPIRED: 'Expired',
})

// Kind of regulatory document an agency uploads for compliance review.
export const ComplianceDocType = Object.freeze({
  BUSINESS_REGISTRATION: 'BusinessRegistration',
  VEHICLE_INSURANCE: 'VehicleInsurance',
  REVENUE_LICENCE: 'RevenueLicence',
  GOODS_TRANSPORT_PERMIT: 'GoodsTransportPermit',
  OTHER: 'Other',
})

// --- Component C: Matching & Trip ---
// State of a proposed load-to-agency/driver assignment.
export const AssignmentStatus = Object.freeze({
  PROPOSED: 'Proposed',
  APPROVED: 'Approved',
  REJECTED: 'Rejected',
  CANCELLED: 'Cancelled',
})

// Lifecycle of a trip once an assignment is approved.
export const TripStatus = Object.freeze({
  ASSIGNED: 'Assigned',
  PICKED_UP: 'PickedUp',
  IN_TRANSIT: 'InTransit',
  DELIVERED: 'Delivered',
  CANCELLED: 'Cancelled',
})

// Kind of proof-of-handling evidence captured during a trip.
export const EvidenceType = Object.freeze({
  PICKUP_PROOF: 'PickupProof',
  DELIVERY_PROOF: 'DeliveryProof',
})

// --- Component D: Billing ---
// Lifecycle of an invoice.
export const InvoiceStatus = Object.freeze({
  DRAFT: 'Draft',
  ISSUED: 'Issued',
  PAYMENT_PENDING: 'PaymentPending',
  PAID: 'Paid',
  FAILED: 'Failed',
  REFUNDED: 'Refunded',
})

// State of a payment attempt against an invoice.
export const PaymentStatus = Object.freeze({
  PENDING: 'Pending',
  PAID: 'Paid',
  FAILED: 'Failed',
  REFUNDED: 'Refunded',
})

// State of a billing dispute raised against an invoice/payment.
export const DisputeStatus = Object.freeze({
  RAISED: 'Raised',
  UNDER_REVIEW: 'UnderReview',
  RESOLVED: 'Resolved',
})

// --- Agentic AI ---
// Lifecycle of an agent workflow run.
export const WorkflowStatus = Object.freeze({
  PENDING: 'Pending',
  RUNNING: 'Running',
  WAITING_APPROVAL: 'WaitingApproval',
  COMPLETED: 'Completed',
  FAILED: 'Failed',
  CANCELLED: 'Cancelled',
})

// The specialized agents that can act within a workflow run.
export const AgentRole = Object.freeze({
  PLANNER: 'Planner',
  DOMAIN_ANALYSIS: 'DomainAnalysis',
  MATCHING_PRICING: 'MatchingPricing',
  VALIDATION_SAFETY: 'ValidationSafety',
})

// A human reviewer's decision on an agent's proposed action.
export const ApprovalDecisionType = Object.freeze({
  APPROVE: 'Approve',
  REJECT: 'Reject',
  REVISE: 'Revise',
})

// --- Pricing Configuration (ADR-019) ---
// Fuel grade a FuelPriceRate row prices.
export const FuelType = Object.freeze({
  AUTO_DIESEL: 'AutoDiesel',
  PETROL_92: 'Petrol92',
  PETROL_95: 'Petrol95',
  SUPER_DIESEL: 'SuperDiesel',
})

// Vehicle-class tier a VehicleClassEfficiency row prices.
export const VehicleClass = Object.freeze({
  MINI_TRUCK: 'MiniTruck',
  MEDIUM_LORRY: 'MediumLorry',
  CONTAINER_TRUCK: 'ContainerTruck',
})
