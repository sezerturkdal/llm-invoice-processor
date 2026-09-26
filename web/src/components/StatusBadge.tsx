import type { InvoiceStatus } from '../api/types'
import { statusLabels } from '../lib/status'

const styles: Record<InvoiceStatus, string> = {
  Processing: 'bg-sky-50 text-sky-700 ring-sky-600/20',
  PendingReview: 'bg-amber-50 text-amber-800 ring-amber-600/25',
  Approved: 'bg-emerald-50 text-emerald-700 ring-emerald-600/20',
  Rejected: 'bg-slate-100 text-slate-600 ring-slate-500/20',
  Failed: 'bg-rose-50 text-rose-700 ring-rose-600/20',
}

export function StatusBadge({ status }: { status: InvoiceStatus }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap ring-1 ring-inset ${styles[status]}`}
    >
      {status === 'Processing' && (
        <span className="size-2.5 animate-spin rounded-full border-[1.5px] border-current border-t-transparent" aria-hidden />
      )}
      {statusLabels[status]}
    </span>
  )
}
