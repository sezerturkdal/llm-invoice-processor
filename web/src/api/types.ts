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
  /** Email of the user who approved or rejected it. */
  reviewedBy: string | null
  modelUsed: string | null
  lines: InvoiceLine[]
  validationIssues: ValidationIssue[]
  /** Why the last extraction failed; set only for Failed invoices. */
  extractionError: string | null
  lastExtraction: ExtractionUsage | null
}

/** What one extraction attempt used. Cost is an estimate in USD. */
export interface ExtractionUsage {
  provider: string
  model: string
  inputTokens: number | null
  outputTokens: number | null
  latencyMs: number
  costEstimate: number | null
  succeeded: boolean
  createdAt: string
}

export interface ExtractionStats {
  /** All attempts, including failures and re-extractions. */
  extractions: number
  failures: number
  inputTokens: number
  outputTokens: number
  totalCost: number
  /** Per successful extraction. */
  averageCost: number | null
  /** Over successful extractions. */
  averageLatencyMs: number | null
  maxLatencyMs: number | null
}

export interface ExtractionStatsResponse {
  /** Start of the period; null for all time. */
  since: string | null
  totals: ExtractionStats
  models: { provider: string; model: string; stats: ExtractionStats }[]
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

export type Role = 'Admin' | 'Reviewer'

export const roles: readonly Role[] = ['Reviewer', 'Admin']

export interface CurrentUser {
  email: string
  role: Role | null
}

export interface UserAccount {
  id: string
  email: string
  role: Role | null
  isActive: boolean
}

/** A model an admin can pick for extraction (src/InvoiceProcessor.Api/Settings). */
export interface ModelChoice {
  id: string
  /** False for text-only models, which can read only PDFs with a text layer. */
  supportsImages: boolean
  /** Parameter count for local models, e.g. "8.8B". */
  size: string | null
  /** USD per million tokens; null for local models. */
  inputPerMillion: number | null
  outputPerMillion: number | null
}

export interface ProviderChoice {
  provider: string
  /** False when the server configuration (Llm:AllowedProviders) rules it out. */
  allowed: boolean
  isSelectable: boolean
  /** Why it cannot be picked right now, e.g. Ollama is not running. */
  unavailable: string | null
  models: ModelChoice[]
}

export interface ModelChange {
  provider: string
  model: string
  changedBy: string
  changedAt: string
}

export interface ExtractionSettings {
  selected: {
    provider: string
    model: string
    /** True when the server's configured default applies. */
    isDefault: boolean
    changedBy: string | null
    changedAt: string | null
  }
  providers: ProviderChoice[]
  history: ModelChange[]
}

/** RFC 9457 problem details, as returned by the API for errors. */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}
