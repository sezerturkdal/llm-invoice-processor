// Mirrors the API's response records (src/InvoiceProcessor.Api/Invoices).

export type InvoiceStatus = 'Processing' | 'PendingReview' | 'Approved' | 'Rejected' | 'Failed'

export const invoiceStatuses: readonly InvoiceStatus[] = ['Processing', 'PendingReview', 'Approved', 'Rejected', 'Failed']

export interface InvoiceSummary {
  id: string
  fileName: string
  status: InvoiceStatus
  supplier: string | null
  invoiceNumber: string | null
  date: string | null
  currency: string | null
  total: number | null
  issueCount: number
  createdAt: string
  reviewedAt: string | null
}

export interface InvoiceListResponse {
  items: InvoiceSummary[]
  totalCount: number
  page: number
  pageSize: number
}

export interface InvoiceLine {
  id: string
  description: string
  qty: number
  unitPrice: number
  lineTotal: number
}

export interface ValidationIssue {
  /** Flagged field, e.g. "net" or "lines". */
  field: string
  rule: string
  message: string
}

export interface Invoice {
  id: string
  fileName: string
  status: InvoiceStatus
  supplier: string | null
  invoiceNumber: string | null
  date: string | null
  currency: string | null
  net: number | null
  vat: number | null
  total: number | null
  createdAt: string
  reviewedAt: string | null
  modelUsed: string | null
  lines: InvoiceLine[]
  validationIssues: ValidationIssue[]
  /** Why the last extraction failed; set only for Failed invoices. */
  extractionError: string | null
}

/** The reviewer's corrected data; replaces all fields and lines. */
export interface InvoiceUpdate {
  supplier: string | null
  invoiceNumber: string | null
  date: string | null
  currency: string | null
  net: number | null
  vat: number | null
  total: number | null
  lines: { description: string; qty: number; unitPrice: number; lineTotal: number }[]
}

export interface InvoiceListFilters {
  status?: InvoiceStatus
  supplier?: string
  page: number
  pageSize: number
}

/** RFC 9457 problem details, as returned by the API for errors. */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}
