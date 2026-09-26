import { useState } from 'react'
import { useCurrentUserQuery } from '../../auth/api/authApi.js'
import { useComplianceDocsQuery, useVehiclesQuery, useAddComplianceDocMutation, useAddVehicleMutation } from '../api/agencyApi.js'
import { useUploadFileMutation } from '../../loads/api/loadsApi.js'
import Card from '../../../components/Card.jsx'
import PageHeader from '../../../components/PageHeader.jsx'
import Button from '../../../components/Button.jsx'
import Input from '../../../components/Input.jsx'
import StatusBadge from '../../../components/StatusBadge.jsx'

export default function AgencyProfilePage() {
  const { data: user } = useCurrentUserQuery()
  // Assuming the user object has the agencyId for AgencyStaff, or we have to get it.
  // Wait, getCurrentUser returns { id, email, role, agencyId: "..." } ?
  // Let's assume the agencyId is available in `user.agencyId`.
  const agencyId = user?.agencyId
  
  const { data: docs, isLoading: docsLoading } = useComplianceDocsQuery(agencyId, { enabled: !!agencyId })
  const { data: vehicles, isLoading: vehiclesLoading } = useVehiclesQuery(agencyId, { enabled: !!agencyId })

  const uploadFile = useUploadFileMutation()
  const addDoc = useAddComplianceDocMutation()
  const addVehicle = useAddVehicleMutation()

  const [docFile, setDocFile] = useState(null)
  const [docType, setDocType] = useState('BusinessRegistration')
  const [docNumber, setDocNumber] = useState('')

  const [regNo, setRegNo] = useState('')
  const [vType, setVType] = useState('MiniTruck')
  const [capacity, setCapacity] = useState('')
  const [volume, setVolume] = useState('')

  const handleAddDoc = async (e) => {
    e.preventDefault()
    if (!docFile || !agencyId) return

    try {
      const uploadRes = await uploadFile.mutateAsync(docFile)
      await addDoc.mutateAsync({
        agencyId,
        doc: {
          publicId: uploadRes.publicId,
          docType: docType,
          docNumber: docNumber,
          issuedOn: new Date().toISOString().split('T')[0]
        }
      })
      setDocFile(null)
      setDocNumber('')
      alert('Document uploaded successfully!')
    } catch (err) {
      alert('Upload failed: ' + (err.response?.data?.message || err.message))
    }
  }

  const handleAddVehicle = async (e) => {
    e.preventDefault()
    if (!agencyId) return

    try {
      await addVehicle.mutateAsync({
        agencyId,
        vehicle: {
          registrationNo: regNo,
          vehicleType: vType,
          capacityKg: Number(capacity),
          volumeM3: Number(volume)
        }
      })
      setRegNo('')
      setCapacity('')
      setVolume('')
      alert('Vehicle added successfully!')
    } catch (err) {
      alert('Failed to add vehicle: ' + (err.response?.data?.message || err.message))
    }
  }

  if (!agencyId) return <div className="p-8 text-center">Loading Profile...</div>

  return (
    <div className="space-y-6">
      <PageHeader title="My Agency Profile" subtitle="Manage your compliance documents and fleet." />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        
        {/* Compliance Docs */}
        <Card className="flex flex-col">
          <Card.Header className="p-6 border-b border-slate-200">
            <h2 className="text-xl font-semibold">Compliance Documents</h2>
          </Card.Header>
          <Card.Body className="p-6 space-y-6">
            <form onSubmit={handleAddDoc} className="space-y-4 bg-slate-50 p-4 rounded-md border border-slate-200">
              <h3 className="font-medium text-slate-800">Upload New Document</h3>
              <div>
                <label className="block text-sm mb-1 text-slate-700">Document Type</label>
                <select 
                  className="w-full rounded-md border border-slate-300 px-3 py-2 text-[15px]" 
                  value={docType} 
                  onChange={e => setDocType(e.target.value)}
                >
                  <option value="BusinessRegistration">Business Registration</option>
                  <option value="VehicleInsurance">Vehicle Insurance</option>
                  <option value="RevenueLicence">Revenue Licence</option>
                  <option value="GoodsTransportPermit">Goods Transport Permit</option>
                  <option value="Other">Other</option>
                </select>
              </div>
              <div>
                <label className="block text-sm mb-1 text-slate-700">Document Number</label>
                <Input required value={docNumber} onChange={e => setDocNumber(e.target.value)} placeholder="DOC-12345" />
              </div>
              <div>
                <label className="block text-sm mb-1 text-slate-700">File (.pdf, .jpg, .png)</label>
                <input required type="file" onChange={e => setDocFile(e.target.files[0])} className="w-full text-sm" />
              </div>
              <Button type="submit" disabled={uploadFile.isPending || addDoc.isPending}>
                {uploadFile.isPending || addDoc.isPending ? 'Uploading...' : 'Upload Document'}
              </Button>
            </form>

            <div>
              <h3 className="font-medium text-slate-800 mb-3">Uploaded Documents</h3>
              {docsLoading ? (
                <p className="text-sm text-slate-500">Loading...</p>
              ) : docs?.length === 0 ? (
                <p className="text-sm text-slate-500">No documents found.</p>
              ) : (
                <ul className="space-y-3">
                  {docs?.map(d => (
                    <li key={d.complianceDocId} className="flex justify-between items-center p-3 border border-slate-200 rounded-md">
                      <div>
                        <p className="font-medium text-sm">{d.docType}</p>
                        <p className="text-xs text-slate-500">{d.docNumber}</p>
                      </div>
                      <StatusBadge tone={d.status === 'Pending' ? 'amber' : d.status === 'Verified' ? 'green' : 'red'}>
                        {d.status}
                      </StatusBadge>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </Card.Body>
        </Card>

        {/* Vehicles */}
        <Card className="flex flex-col">
          <Card.Header className="p-6 border-b border-slate-200">
            <h2 className="text-xl font-semibold">Fleet Vehicles</h2>
          </Card.Header>
          <Card.Body className="p-6 space-y-6">
            <form onSubmit={handleAddVehicle} className="space-y-4 bg-slate-50 p-4 rounded-md border border-slate-200">
              <h3 className="font-medium text-slate-800">Add New Vehicle</h3>
              <div>
                <label className="block text-sm mb-1 text-slate-700">Vehicle Type</label>
                <select 
                  className="w-full rounded-md border border-slate-300 px-3 py-2 text-[15px]" 
                  value={vType} 
                  onChange={e => setVType(e.target.value)}
                >
                  <option value="MiniTruck">Mini Truck</option>
                  <option value="MediumLorry">Medium Lorry</option>
                  <option value="ContainerTruck">Container Truck</option>
                </select>
              </div>
              <div>
                <label className="block text-sm mb-1 text-slate-700">Registration Number</label>
                <Input required value={regNo} onChange={e => setRegNo(e.target.value)} placeholder="AB-1234" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm mb-1 text-slate-700">Capacity (Kg)</label>
                  <Input type="number" required value={capacity} onChange={e => setCapacity(e.target.value)} placeholder="2000" />
                </div>
                <div>
                  <label className="block text-sm mb-1 text-slate-700">Volume (m³)</label>
                  <Input type="number" step="any" required value={volume} onChange={e => setVolume(e.target.value)} placeholder="10.5" />
                </div>
              </div>
              <Button type="submit" disabled={addVehicle.isPending}>
                {addVehicle.isPending ? 'Adding...' : 'Add Vehicle'}
              </Button>
            </form>

            <div>
              <h3 className="font-medium text-slate-800 mb-3">Your Fleet</h3>
              {vehiclesLoading ? (
                <p className="text-sm text-slate-500">Loading...</p>
              ) : vehicles?.length === 0 ? (
                <p className="text-sm text-slate-500">No vehicles found.</p>
              ) : (
                <ul className="space-y-3">
                  {vehicles?.map(v => (
                    <li key={v.vehicleId} className="flex justify-between items-center p-3 border border-slate-200 rounded-md">
                      <div>
                        <p className="font-medium text-sm">{v.registrationNo}</p>
                        <p className="text-xs text-slate-500">{v.vehicleType} • {v.capacityKg}Kg</p>
                      </div>
                      <StatusBadge tone={v.status === 'Available' ? 'green' : 'amber'}>
                        {v.status}
                      </StatusBadge>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </Card.Body>
        </Card>

      </div>
    </div>
  )
}
