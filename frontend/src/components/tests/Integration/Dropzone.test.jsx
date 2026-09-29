import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import Dropzone from '../../Dropzone.jsx'

describe('Dropzone', () => {
  it('renders default dropzone prompt and formats message', () => {
    render(<Dropzone file={null} onFileChange={vi.fn()} />)

    expect(screen.getByText(/drag and drop your file here/i)).toBeInTheDocument()
    expect(screen.getByText(/supported formats:/i)).toBeInTheDocument()
    expect(screen.getByText(/click to browse/i)).toBeInTheDocument()
  })

  it('renders file preview and remove button when file is provided', () => {
    const mockFile = new File(['dummy content'], 'registration.pdf', {
      type: 'application/pdf',
    })
    const onFileChange = vi.fn()

    render(<Dropzone file={mockFile} onFileChange={onFileChange} />)

    expect(screen.getByText('registration.pdf')).toBeInTheDocument()
    expect(screen.getByLabelText(/remove file/i)).toBeInTheDocument()
  })

  it('calls onFileChange with null when remove button is clicked', () => {
    const mockFile = new File(['dummy content'], 'license.png', {
      type: 'image/png',
    })
    const onFileChange = vi.fn()

    render(<Dropzone file={mockFile} onFileChange={onFileChange} />)

    const removeBtn = screen.getByLabelText(/remove file/i)
    fireEvent.click(removeBtn)

    expect(onFileChange).toHaveBeenCalledWith(null)
  })

  it('shows error message if file exceeds maxSizeMB', () => {
    const onFileChange = vi.fn()
    const { container } = render(<Dropzone file={null} onFileChange={onFileChange} maxSizeMB={1} />)

    const input = container.querySelector('input[type="file"]')
    const oversizedFile = new File(['a'.repeat(2 * 1024 * 1024)], 'huge.pdf', {
      type: 'application/pdf',
    })
    Object.defineProperty(oversizedFile, 'size', { value: 2 * 1024 * 1024 })

    fireEvent.change(input, { target: { files: [oversizedFile] } })

    expect(screen.getByText(/file exceeds maximum size of 1mb/i)).toBeInTheDocument()
    expect(onFileChange).not.toHaveBeenCalled()
  })

  it('shows error message for unsupported file extensions', () => {
    const onFileChange = vi.fn()
    const { container } = render(
      <Dropzone file={null} onFileChange={onFileChange} accept=".pdf,.png" />,
    )

    const input = container.querySelector('input[type="file"]')
    const badFile = new File(['text'], 'document.docx', {
      type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    })

    fireEvent.change(input, { target: { files: [badFile] } })

    expect(screen.getByText(/unsupported file format/i)).toBeInTheDocument()
    expect(onFileChange).not.toHaveBeenCalled()
  })
})
