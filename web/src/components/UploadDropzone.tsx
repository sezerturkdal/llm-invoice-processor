import { useRef, useState, type DragEvent } from 'react'
import { useUploadInvoice } from '../api/invoices'

const accept = '.pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg'

interface UploadItem {
  key: string
  name: string
  state: 'uploading' | 'done' | 'error'
  error?: string
}

/**
 * Drop or pick one or more invoices. Each file is uploaded on its own, so one bad file does not
 * stop the others; the server decides what is acceptable (it checks the actual file content).
 */
export function UploadDropzone() {
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [items, setItems] = useState<UploadItem[]>([])
  const upload = useUploadInvoice()

  const update = (key: string, changes: Partial<UploadItem>) =>
    setItems((current) => current.map((item) => (item.key === key ? { ...item, ...changes } : item)))

  const uploadFiles = (files: FileList | null) => {
    if (!files?.length) return

    for (const file of Array.from(files)) {
      const key = crypto.randomUUID()
      setItems((current) => [{ key, name: file.name, state: 'uploading' }, ...current])
      upload.mutate(file, {
        onSuccess: () => update(key, { state: 'done' }),
        onError: (error) => update(key, { state: 'error', error: error.message }),
      })
    }
  }

  const onDrop = (event: DragEvent) => {
    event.preventDefault()
    setDragging(false)
    uploadFiles(event.dataTransfer.files)
  }

  return (
    <section>
      <div
        role="button"
        tabIndex={0}
        onClick={() => inputRef.current?.click()}
        onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && inputRef.current?.click()}
        onDragOver={(e) => {
          e.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        className={`flex cursor-pointer flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed px-6 py-8 text-center transition-colors focus:ring-2 focus:ring-accent-500 focus:outline-none ${
          dragging ? 'border-accent-500 bg-accent-50' : 'border-slate-300 bg-white hover:border-slate-400'
        }`}
      >
        <UploadIcon />
        <p className="text-sm font-medium text-slate-800">
          Drop invoices here or <span className="text-accent-600">browse</span>
        </p>
        <p className="text-xs text-slate-500">PDF, PNG or JPEG, up to 10 MB each</p>
        <input
          ref={inputRef}
          type="file"
          accept={accept}
          multiple
          className="hidden"
          onChange={(e) => {
            uploadFiles(e.target.files)
            e.target.value = ''
          }}
        />
      </div>

      {items.length > 0 && (
        <ul className="mt-3 space-y-1.5">
          {items.slice(0, 5).map((item) => (
            <li key={item.key} className="flex items-center gap-2 text-sm">
              <UploadStateIcon state={item.state} />
              <span className="truncate text-slate-700">{item.name}</span>
              {item.state === 'done' && <span className="text-slate-500">uploaded, extraction started</span>}
              {item.state === 'error' && <span className="text-rose-700">{item.error}</span>}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function UploadStateIcon({ state }: { state: UploadItem['state'] }) {
  if (state === 'uploading') {
    return <span className="size-3.5 shrink-0 animate-spin rounded-full border-2 border-slate-400 border-t-transparent" aria-label="Uploading" />
  }
  if (state === 'done') {
    return <span className="shrink-0 text-emerald-600" aria-label="Uploaded">✓</span>
  }
  return <span className="shrink-0 text-rose-600" aria-label="Failed">✕</span>
}

function UploadIcon() {
  return (
    <svg className="mb-1 size-8 text-slate-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} aria-hidden>
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 16V4m0 0-4 4m4-4 4 4M4 16v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" />
    </svg>
  )
}
