/**
 * Realistic mock dataset for Admin Dispute Management (Ticket Y3S01-81).
 * Covers all lifecycle states: Raised -> UnderReview -> Resolved.
 */

export const INITIAL_MOCK_DISPUTES = [
  {
    disputeId: 'd1984a20-3b4e-4f76-8801-49b819f00001',
    displayId: 'DISP-1042',
    raisedDate: '2026-09-26T08:30:00+05:30',
    raisedByUser: {
      userId: 'u881-shipper-01',
      name: 'Sunil Weerakkody',
      role: 'Shipper',
      email: 's.weerakkody@lankateatraders.lk',
      phone: '+94 77 342 9182',
      company: 'Lanka Premium Tea Exporters'
    },
    trip: {
      tripId: 'TRP-8841',
      routeSummary: 'Colombo ➔ Kandy',
      origin: 'Colombo Port Container Terminal',
      destination: 'Peradeniya Logistics Hub, Kandy',
      carrierAgency: 'Central Express Logistics',
      truckRegNo: 'WP-NC-4892'
    },
    category: 'Damage',
    description: 'Consignment of 40 export-grade Ceylon Tea wooden chests arrived with severe water intrusion on pallet #3 and #4. Moisture seals were breached during transit under heavy rainfall.',
    status: 'Raised',
    resolution: null,
    updatedAt: '2026-09-26T08:30:00+05:30'
  },
  {
    disputeId: 'd1984a20-3b4e-4f76-8801-49b819f00002',
    displayId: 'DISP-1045',
    raisedDate: '2026-09-26T09:15:00+05:30',
    raisedByUser: {
      userId: 'u882-agency-01',
      name: 'Rohan Jayasinghe',
      role: 'Agency',
      email: 'rohan.j@wayambafreight.com',
      phone: '+94 71 884 1029',
      company: 'Wayamba Haulers & Freight Ltd'
    },
    trip: {
      tripId: 'TRP-8890',
      routeSummary: 'Kurunegala ➔ Anuradhapura',
      origin: 'Dambulla Agro Storage Center',
      destination: 'Anuradhapura Central Wholesale',
      carrierAgency: 'Wayamba Haulers & Freight Ltd',
      truckRegNo: 'NW-DA-7714'
    },
    category: 'Payment Issue',
    description: 'Shipper cancelled drop-off point access after truck arrived on site. Detention charge of 4 hours plus secondary un-docking fee has not been acknowledged in the interim invoice.',
    status: 'Raised',
    resolution: null,
    updatedAt: '2026-09-26T09:15:00+05:30'
  },
  {
    disputeId: 'd1984a20-3b4e-4f76-8801-49b819f00003',
    displayId: 'DISP-1039',
    raisedDate: '2026-09-25T14:20:00+05:30',
    raisedByUser: {
      userId: 'u883-shipper-02',
      name: 'Malini Fernando',
      role: 'Shipper',
      email: 'm.fernando@ceylonspices.com',
      phone: '+94 76 991 4321',
      company: 'Ceylon Spice Millers PLC'
    },
    trip: {
      tripId: 'TRP-8799',
      routeSummary: 'Galle ➔ Colombo',
      origin: 'Karapitiya Processing Plant, Galle',
      destination: 'Peliyagoda Cold Storage Hub',
      carrierAgency: 'Southern Coast Haulage',
      truckRegNo: 'SP-LI-3310'
    },
    category: 'Delay',
    description: 'Delivery was scheduled for 09:00 AM for fresh spice extract loading onto sea freight reefer. Carrier arrived at 04:30 PM due to uncommunicated vehicle maintenance, incurring demurrage costs.',
    status: 'UnderReview',
    resolution: null,
    updatedAt: '2026-09-26T10:00:00+05:30',
    reviewStartedAt: '2026-09-26T10:00:00+05:30',
    reviewedBy: 'Admin (Compliance Desk)'
  },
  {
    disputeId: 'd1984a20-3b4e-4f76-8801-49b819f00004',
    displayId: 'DISP-1036',
    raisedDate: '2026-09-25T11:05:00+05:30',
    raisedByUser: {
      userId: 'u884-agency-02',
      name: 'Chaminda Silva',
      role: 'Agency',
      email: 'ops@lankaheavytrans.lk',
      phone: '+94 77 410 8820',
      company: 'Lanka Heavy Transport Co.'
    },
    trip: {
      tripId: 'TRP-8760',
      routeSummary: 'Hambantota ➔ Biyagama',
      origin: 'Hambantota Port Industrial Zone',
      destination: 'Biyagama Export Processing Zone',
      carrierAgency: 'Lanka Heavy Transport Co.',
      truckRegNo: 'WP-QA-9011'
    },
    category: 'Damage',
    description: 'During cargo discharge at recipient warehouse, forklift operator employed by recipient punctured vehicle side-curtain and caused minor structural bend in rear tailgate.',
    status: 'UnderReview',
    resolution: null,
    updatedAt: '2026-09-26T07:45:00+05:30',
    reviewStartedAt: '2026-09-26T07:45:00+05:30',
    reviewedBy: 'Admin (Operations)'
  },
  {
    disputeId: 'd1984a20-3b4e-4f76-8801-49b819f00005',
    displayId: 'DISP-1028',
    raisedDate: '2026-09-24T16:40:00+05:30',
    raisedByUser: {
      userId: 'u885-shipper-03',
      name: 'Dinesh Ratnayake',
      role: 'Shipper',
      email: 'dinesh.r@apexgarments.com',
      phone: '+94 70 234 5678',
      company: 'Apex Apparel Exports Ltd'
    },
    trip: {
      tripId: 'TRP-8692',
      routeSummary: 'Katunayake ➔ Colombo',
      origin: 'Katunayake EPZ Bay 4',
      destination: 'Colombo International Container Terminal (CICT)',
      carrierAgency: 'FastTrack Cargo Services',
      truckRegNo: 'WP-LH-2301'
    },
    category: 'Payment Issue',
    description: 'Invoice #INV-2026-081 included an unverified fuel surcharge rate adjustment of 15% instead of the contractually agreed 8% formula cap.',
    status: 'Resolved',
    updatedAt: '2026-09-25T15:10:00+05:30',
    reviewStartedAt: '2026-09-25T09:00:00+05:30',
    resolution: {
      outcome: 'Upheld',
      notes: 'Reviewed contractual pricing agreement with Shipper and verified against FreightLink automated fuel index formula. Surcharge corrected back to 8% cap. Amended credit note issued for difference of LKR 18,450.',
      resolvedAt: '2026-09-25T15:10:00+05:30',
      resolvedByUser: {
        name: 'Kasun Wickramasinghe (Admin)',
        email: 'kasun.admin@freightlink.lk'
      }
    }
  },
  {
    disputeId: 'd1984a20-3b4e-4f76-8801-49b819f00006',
    displayId: 'DISP-1014',
    raisedDate: '2026-09-22T10:15:00+05:30',
    raisedByUser: {
      userId: 'u886-agency-03',
      name: 'Nadeeka Bandara',
      role: 'Agency',
      email: 'nadeeka@islandfreight.lk',
      phone: '+94 72 556 7890',
      company: 'Island Wide Freight Services'
    },
    trip: {
      tripId: 'TRP-8540',
      routeSummary: 'Trincomalee ➔ Dambulla',
      origin: 'Trincomalee Fishery Harbour',
      destination: 'Dambulla Agro Processing Facility',
      carrierAgency: 'Island Wide Freight Services',
      truckRegNo: 'EP-NC-1288'
    },
    category: 'Delay',
    description: 'Driver penalized by shipper for delayed arrival during road closure caused by landslide on A6 highway. Evidence of official police traffic diversion was submitted.',
    status: 'Resolved',
    updatedAt: '2026-09-23T11:30:00+05:30',
    reviewStartedAt: '2026-09-22T15:00:00+05:30',
    resolution: {
      outcome: 'Upheld',
      notes: 'Road Development Authority and Police advisories verified for A6 route on incident date. Delay was strictly due to Force Majeure. Delay penalty of LKR 12,000 reversed and agency standing restored to 100%.',
      resolvedAt: '2026-09-23T11:30:00+05:30',
      resolvedByUser: {
        name: 'Ruwan Senanayake (Admin)',
        email: 'ruwan.admin@freightlink.lk'
      }
    }
  },
  {
    disputeId: 'd1984a20-3b4e-4f76-8801-49b819f00007',
    displayId: 'DISP-1010',
    raisedDate: '2026-09-20T13:40:00+05:30',
    raisedByUser: {
      userId: 'u887-shipper-04',
      name: 'Anura De Silva',
      role: 'Shipper',
      email: 'anura.ds@lankacement.lk',
      phone: '+94 77 889 0012',
      company: 'Lanka Cement Industries'
    },
    trip: {
      tripId: 'TRP-8490',
      routeSummary: 'Galle ➔ Ratnapura',
      origin: 'Ruhunu Clinker Grinding Plant, Galle',
      destination: 'Ratnapura District Depots',
      carrierAgency: 'Southern Coast Haulage',
      truckRegNo: 'SP-LI-3310'
    },
    category: 'Other',
    description: 'Discrepancy in digital weight-bridge slips versus delivery receipt manifest at drop-off gate. Shipper claimed short delivery of 3 metric tons.',
    status: 'Resolved',
    updatedAt: '2026-09-21T16:20:00+05:30',
    reviewStartedAt: '2026-09-21T09:30:00+05:30',
    resolution: {
      outcome: 'PartiallyUpheld',
      notes: 'Investigated calibration records for both weight-bridges. Destination scale had certified error offset of -2.4%. Adjusted shortfall liability split equally between carrier and receiving facility according to calibrated variance.',
      resolvedAt: '2026-09-21T16:20:00+05:30',
      resolvedByUser: {
        name: 'Kasun Wickramasinghe (Admin)',
        email: 'kasun.admin@freightlink.lk'
      }
    }
  }
]
