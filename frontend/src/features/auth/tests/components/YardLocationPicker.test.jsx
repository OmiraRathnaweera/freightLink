import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import YardLocationPicker from '../../../../components/map/YardLocationPicker.jsx'
import * as nominatimApi from '../../../../lib/api/nominatimApi.js'

vi.mock('../../../../lib/api/nominatimApi.js', () => ({
  reverseGeocode: vi.fn(),
  searchPlaces: vi.fn(),
}))

afterEach(() => {
  vi.mocked(nominatimApi.reverseGeocode).mockReset()
  vi.mocked(nominatimApi.searchPlaces).mockReset()
  cleanup()
})

describe('YardLocationPicker', () => {
  it('renders search input, address input, use current location button, and coordinate inputs', () => {
    render(
      <YardLocationPicker
        address="Peliyagoda Yard"
        lat={6.9583}
        lng={79.8833}
        onChange={vi.fn()}
      />,
    )

    expect(screen.getByRole('button', { name: /Use Current Location/i })).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/Search for a place or address/i)).toBeInTheDocument()
    expect(screen.getByDisplayValue('Peliyagoda Yard')).toBeInTheDocument()
    expect(screen.getByLabelText(/Yard Latitude/i)).toHaveValue(6.9583)
    expect(screen.getByLabelText(/Yard Longitude/i)).toHaveValue(79.8833)
    expect(screen.getByText('6.958300, 79.883300')).toBeInTheDocument()
  })

  it('triggers geolocation on "Use Current Location" button click and updates location', async () => {
    const onChange = vi.fn()
    vi.mocked(nominatimApi.reverseGeocode).mockResolvedValueOnce({
      displayName: 'Colombo Port Yard, Colombo',
    })

    const getCurrentPosition = vi.fn().mockImplementation((success) => {
      success({
        coords: { latitude: 6.94, longitude: 79.85 },
      })
    })

    const originalGeolocation = navigator.geolocation
    Object.defineProperty(navigator, 'geolocation', {
      value: { getCurrentPosition },
      configurable: true,
      writable: true,
    })

    const user = userEvent.setup()
    render(
      <YardLocationPicker
        address=""
        lat=""
        lng=""
        onChange={onChange}
      />,
    )

    await user.click(screen.getByRole('button', { name: /Use Current Location/i }))

    expect(getCurrentPosition).toHaveBeenCalled()

    await waitFor(() => {
      expect(onChange).toHaveBeenCalledWith(
        expect.objectContaining({
          lat: 6.94,
          lng: 79.85,
        }),
      )
    })

    Object.defineProperty(navigator, 'geolocation', {
      value: originalGeolocation,
      configurable: true,
      writable: true,
    })
  })

  it('updates address when typing directly into the address input', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()

    render(
      <YardLocationPicker
        address=""
        lat={6.9}
        lng={79.8}
        onChange={onChange}
      />,
    )

    const addressInput = screen.getByPlaceholderText('Address')
    await user.type(addressInput, 'New Depot Address')

    expect(onChange).toHaveBeenCalled()
  })
})
