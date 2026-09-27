import { useRef, useState } from 'react'
import { fileUrl } from '../../api/invoices'

// Open PDFs fitted to the frame's width with the thumbnail sidebar closed; at the default
// (whole page, thumbnails open) a side panel is too small to read. Chromium/Edge read
// view/navpanes, Firefox's pdf.js reads zoom/pagemode; each ignores the others.
const pdfViewParams = '#view=FitH&navpanes=0&zoom=page-width&pagemode=none'

/** The original document next to the form, with an Enlarge button that opens it near full screen. */
export function DocumentViewer({ invoiceId, fileName }: { invoiceId: string; fileName: string }) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  // The enlarged copy is only rendered while open, so the document isn't downloaded twice up front.
  const [enlarged, setEnlarged] = useState(false)
  const url = fileUrl(invoiceId)
  const isImage = /\.(png|jpe?g)$/i.test(fileName)

  const open = () => {
    setEnlarged(true)
    dialogRef.current?.showModal()
  }

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-xl border border-slate-200 bg-white">
      <div className="flex items-center gap-3 border-b border-slate-200 px-4 py-2 text-sm">
        <span className="min-w-0 flex-1 truncate text-slate-600" title={fileName}>
          {fileName}
        </span>
        <button
          type="button"
          onClick={open}
          className="shrink-0 font-medium text-accent-600 hover:text-accent-700"
        >
          Enlarge
        </button>
        <a href={url} target="_blank" rel="noreferrer" className="shrink-0 font-medium text-accent-600 hover:text-accent-700">
          Open ↗
        </a>
      </div>

      <Document url={url} fileName={fileName} isImage={isImage} />

      {/* Native dialog: Esc and the backdrop close it, focus is trapped while open. */}
      <dialog
        ref={dialogRef}
        onClick={(e) => e.target === e.currentTarget && e.currentTarget.close()}
        onClose={() => setEnlarged(false)}
        aria-label={`Document ${fileName}`}
        className="m-auto h-[92vh] w-[min(95vw,80rem)] overflow-hidden rounded-xl bg-white p-0 shadow-2xl backdrop:bg-slate-900/60"
      >
        <div className="flex h-full flex-col">
          <div className="flex items-center gap-3 border-b border-slate-200 px-4 py-2 text-sm">
            <span className="min-w-0 flex-1 truncate font-medium text-slate-700">{fileName}</span>
            <button
              type="button"
              onClick={() => dialogRef.current?.close()}
              className="rounded-md px-2 py-1 font-medium text-slate-600 hover:bg-slate-100"
              aria-label="Close"
            >
              ✕ Close
            </button>
          </div>
          {enlarged && <Document url={url} fileName={fileName} isImage={isImage} />}
        </div>
      </dialog>
    </div>
  )
}

function Document({ url, fileName, isImage }: { url: string; fileName: string; isImage: boolean }) {
  if (isImage) {
    return (
      <div className="flex-1 overflow-auto bg-slate-100 p-4">
        <img src={url} alt={`Invoice ${fileName}`} className="mx-auto max-w-full shadow-sm" />
      </div>
    )
  }

  return <iframe src={`${url}${pdfViewParams}`} title={`Invoice ${fileName}`} className="w-full flex-1" />
}
