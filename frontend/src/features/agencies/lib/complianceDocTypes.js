import { Building2, FileCheck2, FileText, ShieldCheck, Truck } from 'lucide-react'
import { ComplianceDocType } from '../../../lib/enums.js'

// Single source of truth for compliance-doc-type display metadata (title,
// description, icon, whether it's mandatory for an agency to go Active).
// Shared by AgencyProfilePage (agency's own upload grid) and
// AgencyVerificationPage (admin review queue) so the copy can't drift
// between the two screens.
export const COMPLIANCE_DOC_TYPES = [
  {
    id: ComplianceDocType.BUSINESS_REGISTRATION,
    title: 'Business Registration',
    shortTitle: 'Business Reg',
    description: 'Certificate of Incorporation or Business Registration license.',
    icon: Building2,
    mandatory: true,
  },
  {
    id: ComplianceDocType.VEHICLE_INSURANCE,
    title: 'Fleet Insurance Policy',
    shortTitle: 'Insurance',
    description: 'Comprehensive commercial vehicle & goods-in-transit liability coverage.',
    icon: ShieldCheck,
    mandatory: true,
  },
  {
    id: ComplianceDocType.REVENUE_LICENCE,
    title: 'Revenue Licence',
    shortTitle: 'Revenue Licence',
    description: 'Annual valid commercial motor traffic revenue license permit.',
    icon: FileCheck2,
    mandatory: true,
  },
  {
    id: ComplianceDocType.GOODS_TRANSPORT_PERMIT,
    title: 'Goods Transport Permit',
    shortTitle: 'Transport Permit',
    description: 'Provincial / national freight transportation and carriage permit.',
    icon: Truck,
    mandatory: true,
  },
  {
    id: ComplianceDocType.OTHER,
    title: 'Additional Documentation',
    shortTitle: 'Additional Doc',
    description: 'Tax clearance certificates, environmental permits, or safety records.',
    icon: FileText,
    mandatory: false,
  },
]

const DOC_TYPE_BY_ID = Object.fromEntries(COMPLIANCE_DOC_TYPES.map((t) => [t.id, t]))

/** Short human label for a doc type id, falling back to the raw id if unknown. */
export function getComplianceDocTypeLabel(docTypeId) {
  return DOC_TYPE_BY_ID[docTypeId]?.shortTitle ?? docTypeId
}
