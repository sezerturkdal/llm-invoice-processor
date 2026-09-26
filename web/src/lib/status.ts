import type { InvoiceStatus } from '../api/types'

export const statusLabels: Record<InvoiceStatus, string> = {
  Processing: 'Processing',
  PendingReview: 'Pending review',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Failed: 'Failed',
}
