import { useSearchParams, Link, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { useRegisterAgencyMutation } from '../api/authApi.js'
import Card from '../../../components/Card.jsx'
import Button from '../../../components/Button.jsx'
import Input from '../../../components/Input.jsx'

export default function RegisterPage() {
  const [searchParams] = useSearchParams()
  const role = searchParams.get('role')

  if (role === 'agency') {
    return <AgencyRegisterForm />
  }

  // Placeholder for other roles or role selection
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-slate-50 p-4">
      <Card className="max-w-md w-full p-8 text-center space-y-4">
        <h1 className="text-2xl font-bold text-slate-900">Choose Account Type</h1>
        <Button as={Link} to="/register?role=agency" className="w-full">
          Join Agency Network
        </Button>
        <Button as={Link} to="/register?role=shipper" variant="secondary" className="w-full">
          Register as Shipper
        </Button>
      </Card>
    </div>
  )
}

function AgencyRegisterForm() {
  const navigate = useNavigate()
  const registerMutation = useRegisterAgencyMutation({
    onSuccess: () => {
      alert('Registration successful! Please login.')
      navigate('/login')
    },
    onError: (err) => {
      alert(`Registration failed: ${err.message || 'Check your inputs and try again.'}`)
    }
  })

  const [formData, setFormData] = useState({
    email: '',
    password: '',
    fullName: '',
    phoneE164: '',
    jobTitle: '',
    agencyName: '',
    businessRegNo: '',
    yardAddress: '',
    yardLat: '',
    yardLng: ''
  })

  const handleChange = (e) => {
    const { name, value } = e.target
    setFormData(prev => ({ ...prev, [name]: value }))
  }

  const handleSubmit = (e) => {
    e.preventDefault()
    // Convert lat/lng to numbers
    const payload = {
      ...formData,
      yardLat: Number(formData.yardLat),
      yardLng: Number(formData.yardLng)
    }
    
    // Convert phone to null if empty, otherwise E.164 requires it or breaks
    if (!payload.phoneE164) {
      delete payload.phoneE164
    }
    
    // Delete jobTitle if empty
    if (!payload.jobTitle) {
      delete payload.jobTitle
    }
    
    registerMutation.mutate(payload)
  }

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-slate-50 p-4 py-12">
      <Card className="max-w-2xl w-full">
        <Card.Header className="p-6 border-b border-slate-200">
          <h1 className="text-2xl font-bold text-slate-900">Join Agency Network</h1>
          <p className="text-sm text-slate-500 mt-1">Create your agency profile and your staff account.</p>
        </Card.Header>
        <Card.Body className="p-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            
            <div className="space-y-4">
              <h2 className="text-lg font-semibold text-slate-800 border-b pb-2">User Details</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Full Name</label>
                  <Input name="fullName" required value={formData.fullName} onChange={handleChange} placeholder="John Doe" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Email</label>
                  <Input type="email" name="email" required value={formData.email} onChange={handleChange} placeholder="john@example.com" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Password</label>
                  <Input type="password" name="password" required value={formData.password} onChange={handleChange} placeholder="Strong password" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Phone (Optional)</label>
                  <Input type="tel" name="phoneE164" value={formData.phoneE164} onChange={handleChange} placeholder="+14155552671" />
                </div>
                <div className="md:col-span-2">
                  <label className="block text-sm font-medium text-slate-700 mb-1">Job Title (Optional)</label>
                  <Input name="jobTitle" value={formData.jobTitle} onChange={handleChange} placeholder="Fleet Manager" />
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <h2 className="text-lg font-semibold text-slate-800 border-b pb-2">Agency Details</h2>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="md:col-span-2">
                  <label className="block text-sm font-medium text-slate-700 mb-1">Agency Name</label>
                  <Input name="agencyName" required value={formData.agencyName} onChange={handleChange} placeholder="Fast Freight Inc." />
                </div>
                <div className="md:col-span-2">
                  <label className="block text-sm font-medium text-slate-700 mb-1">Business Registration No</label>
                  <Input name="businessRegNo" required value={formData.businessRegNo} onChange={handleChange} placeholder="BR-12345" />
                </div>
                <div className="md:col-span-2">
                  <label className="block text-sm font-medium text-slate-700 mb-1">Yard Address</label>
                  <Input name="yardAddress" required value={formData.yardAddress} onChange={handleChange} placeholder="123 Yard St, City" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Yard Latitude</label>
                  <Input type="number" step="any" name="yardLat" required value={formData.yardLat} onChange={handleChange} placeholder="37.7749" />
                </div>
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Yard Longitude</label>
                  <Input type="number" step="any" name="yardLng" required value={formData.yardLng} onChange={handleChange} placeholder="-122.4194" />
                </div>
              </div>
            </div>

            <Button 
              type="submit" 
              className="w-full py-2.5 text-base"
              disabled={registerMutation.isPending}
            >
              {registerMutation.isPending ? 'Registering...' : 'Complete Registration'}
            </Button>
          </form>
        </Card.Body>
      </Card>
    </div>
  )
}
