import { useCallback, useEffect, useRef, useState, type ReactNode, type RefObject } from 'react'
import { Link, useBlocker, useNavigate, useParams } from 'react-router'
import { ApiError } from '../api/client'
import { findNextPending, useInvoice, useReviewActions } from '../api/invoices'
import type { ExtractionUsage, Invoice, ValidationIssue } from '../api/types'
import { DocumentViewer } from '../components/review/DocumentViewer'
import { FieldInput } from '../components/review/FieldInput'
import { LinesEditor } from '../components/review/LinesEditor'
import { StatusBadge } from '../components/StatusBadge'
import { draftFromInvoice, draftToUpdate, isDirty, type DraftErrors, type InvoiceDraft } from '../lib/draft'
import { formatCount, formatDuration, formatRelative, formatUsd } from '../lib/format'

/** The review screen: the document on the left, the extracted data on the right. */
export function InvoiceDetailPage() {
  const { id = '' } = useParams()
  const { data: invoice, isPending, isError, error } = useInvoice(id)

  if (isPending) return <p className="text-sm text-slate-500">Loading…</p>
  if (isError) return <p className="text-sm text-rose-700">Could not load the invoice: {error.message}</p>

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <Link to="/" className="text-sm font-medium text-accent-600 hover:text-accent-700">
          ← All invoices
        </Link>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div className="h-[60vh] lg:sticky lg:top-6 lg:h-[calc(100vh-7rem)]">
          <DocumentViewer invoiceId={invoice.id} fileName={invoice.fileName} />
        </div>

        <div className="min-w-0">
          {/* Remount per invoice so the form starts from that invoice's data. */}
          <ReviewPanel key={invoice.id} invoice={invoice} />
        </div>
      </div>
    </div>
  )
}

function ReviewPanel({ invoice }: { invoice: Invoice }) {
  const header = (
    <div className="mb-5">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="min-w-0 truncate text-xl font-semibold tracking-tight" title={invoice.supplier ?? invoice.fileName}>
          {invoice.supplier ?? invoice.fileName}
        </h1>
        <StatusBadge status={invoice.status} />
      </div>
      <p className="mt-1 text-xs text-slate-500">
        Uploaded {formatRelative(invoice.createdAt)}
        {invoice.modelUsed && <> · extracted by {invoice.modelUsed}</>}
        {invoice.reviewedAt && (
          <>
            {' '}
            · {invoice.status === 'Rejected' ? 'rejected' : 'approved'}
            {invoice.reviewedBy && <> by {invoice.reviewedBy}</>} {formatRelative(invoice.reviewedAt)}
          </>
        )}
      </p>
      {invoice.lastExtraction?.succeeded && invoice.status !== 'Processing' && <UsageLine usage={invoice.lastExtraction} />}
    </div>
  )

  if (invoice.status === 'Processing') {
    return (
      <>
        {header}
        <Notice tone="info">
          <span className="mr-2 inline-block size-3 animate-spin rounded-full border-2 border-sky-600 border-t-transparent align-[-1px]" />
          Extracting data from the document… this usually takes a few seconds.
        </Notice>
      </>
    )
  }

  if (invoice.status === 'Failed') {
    return (
      <>
        {header}
        <FailedPanel invoice={invoice} />
      </>
    )
  }

  return (
    <>
      {header}
      <ReviewForm invoice={invoice} />
    </>
  )
}

function UsageLine({ usage }: { usage: ExtractionUsage }) {
  return (
    <p className="tabular mt-0.5 text-xs text-slate-500">
      {formatDuration(usage.latencyMs)}
      {usage.inputTokens !== null && usage.outputTokens !== null && (
        <>
          {' '}
          · {formatCount(usage.inputTokens)} in / {formatCount(usage.outputTokens)} out tokens
        </>
      )}
      {usage.costEstimate !== null && <> · ~{formatUsd(usage.costEstimate)}</>}
    </p>
  )
}

function FailedPanel({ invoice }: { invoice: Invoice }) {
  const { reextract, reject } = useReviewActions(invoice.id)
  const error = reextract.error ?? reject.error

  return (
    <div className="space-y-4">
      <Notice tone="error">
        <p className="font-medium">Extraction failed.</p>
        {invoice.extractionError && <p className="mt-1 font-mono text-xs break-words whitespace-pre-wrap">{invoice.extractionError}</p>}
      </Notice>
      {error && <Notice tone="error">{error.message}</Notice>}
      <div className="flex gap-2">
        <Button variant="primary" onClick={() => reextract.mutate()} busy={reextract.isPending}>
          Retry extraction
        </Button>
        <Button variant="danger" onClick={() => confirm('Reject this upload?') && reject.mutate()} busy={reject.isPending}>
          Reject
        </Button>
      </div>
    </div>
  )
}

function ReviewForm({ invoice }: { invoice: Invoice }) {
  const navigate = useNavigate()
  const { save, approve, reject } = useReviewActions(invoice.id)
  const [draft, setDraft] = useState<InvoiceDraft>(() => draftFromInvoice(invoice))
  const [inputErrors, setInputErrors] = useState<DraftErrors>({})
  const [actionError, setActionError] = useState<string>()
  const leaving = useRef(false)

  const readOnly = invoice.status !== 'PendingReview'
  const dirty = !readOnly && isDirty(draft, invoice)
  const busy = save.isPending || approve.isPending || reject.isPending
  const issuesByField = groupByField(invoice.validationIssues)
  const hasBlockingIssues = invoice.validationIssues.some((i) => i.rule === 'RequiredFields')

  const set = <K extends keyof InvoiceDraft>(key: K) => (value: InvoiceDraft[K]) => setDraft((d) => ({ ...d, [key]: value }))

  // Saves if there are changes; returns the saved invoice, or undefined if the input or the save failed.
  const saveDraft = useCallback(async (): Promise<Invoice | undefined> => {
    setActionError(undefined)
    if (!isDirty(draft, invoice)) return invoice

    const result = draftToUpdate(draft)
    if ('errors' in result) {
      setInputErrors(result.errors)
      return undefined
    }

    setInputErrors({})
    try {
      const saved = await save.mutateAsync(result.update)
      setDraft(draftFromInvoice(saved))
      return saved
    } catch (error) {
      handleError(error, setInputErrors, setActionError)
      return undefined
    }
  }, [draft, invoice, save])

  const finish = async (action: 'approve' | 'reject') => {
    if (action === 'reject' && !confirm('Reject this invoice? It will be excluded from reporting and duplicate checks.')) return

    try {
      if (action === 'approve') {
        const saved = await saveDraft()
        if (!saved) return
        await approve.mutateAsync()
      } else {
        await reject.mutateAsync()
      }
    } catch (error) {
      handleError(error, setInputErrors, setActionError)
      return
    }

    // Straight on to the next invoice waiting for review, if there is one.
    leaving.current = true
    const next = await findNextPending(invoice.id).catch(() => undefined)
    navigate(next ? `/invoices/${next}` : '/')
  }

  // Ctrl/Cmd+S saves.
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        if (dirty && !busy) void saveDraft()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [dirty, busy, saveDraft])

  useUnsavedChangesGuard(dirty && !busy, leaving)

  const issueCount = invoice.validationIssues.length

  return (
    <div className="space-y-6">
      {!readOnly &&
        (issueCount > 0 ? (
          <Notice tone="warning">
            <span className="font-medium">
              {issueCount} {issueCount === 1 ? 'check needs' : 'checks need'} your attention.
            </span>{' '}
            Compare the highlighted fields with the document and correct them, or approve if the invoice really says so.
          </Notice>
        ) : (
          <Notice tone="success">All checks passed. Compare with the document and approve.</Notice>
        ))}

      {/* Rows: who, which invoice, how much. Six columns so the rows split 1 / 2+2+2 / 2+2+2 (currency narrower). */}
      <section className="grid items-start gap-4 rounded-xl border border-slate-200 bg-white p-5 sm:grid-cols-6">
        <FieldInput label="Supplier" value={draft.supplier} onChange={set('supplier')} issues={issuesByField.supplier} readOnly={readOnly} className="sm:col-span-6" />
        <FieldInput label="Invoice number" value={draft.invoiceNumber} onChange={set('invoiceNumber')} issues={issuesByField.invoiceNumber} readOnly={readOnly} className="sm:col-span-3" />
        <FieldInput label="Invoice date" type="date" value={draft.date} onChange={set('date')} issues={issuesByField.date} error={inputErrors.date} readOnly={readOnly} className="sm:col-span-2" />
        <FieldInput label="Currency" value={draft.currency} onChange={set('currency')} issues={issuesByField.currency} readOnly={readOnly} placeholder="EUR" className="sm:col-span-1" />
        <FieldInput label="Net" inputMode="decimal" value={draft.net} onChange={set('net')} issues={issuesByField.net} error={inputErrors.net} readOnly={readOnly} className="sm:col-span-2" />
        <FieldInput label="VAT" inputMode="decimal" value={draft.vat} onChange={set('vat')} issues={issuesByField.vat} error={inputErrors.vat} readOnly={readOnly} className="sm:col-span-2" />
        <FieldInput label="Total" inputMode="decimal" value={draft.total} onChange={set('total')} issues={issuesByField.total} error={inputErrors.total} readOnly={readOnly} className="sm:col-span-2" />
      </section>

      <LinesEditor draft={draft} onChange={set('lines')} issues={issuesByField.lines ?? []} errors={inputErrors} readOnly={readOnly} />

      {!readOnly && (
        <div className="sticky bottom-0 -mx-1 flex flex-wrap items-center gap-2 border-t border-slate-200 bg-slate-50/95 px-1 py-3 backdrop-blur">
          <Button variant="danger" onClick={() => void finish('reject')} disabled={busy} busy={reject.isPending}>
            Reject
          </Button>
          <div className="flex-1 text-xs text-slate-500">
            {actionError ? <span className="text-rose-700">{actionError}</span> : dirty ? 'Unsaved changes' : null}
          </div>
          <Button variant="secondary" onClick={() => void saveDraft()} disabled={!dirty || busy} busy={save.isPending && !approve.isPending}>
            Save
          </Button>
          <Button
            variant="primary"
            onClick={() => void finish('approve')}
            disabled={busy || (hasBlockingIssues && !dirty)}
            busy={approve.isPending}
            title={hasBlockingIssues && !dirty ? 'Fill in the missing fields first' : undefined}
          >
            {dirty ? 'Save & approve' : 'Approve'}
          </Button>
        </div>
      )}
    </div>
  )
}

/** Asks before navigating away (in-app or closing the tab) with unsaved edits. */
function useUnsavedChangesGuard(active: boolean, leaving: RefObject<boolean>) {
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) => active && !leaving.current && currentLocation.pathname !== nextLocation.pathname,
  )

  useEffect(() => {
    if (blocker.state !== 'blocked') return
    if (confirm('You have unsaved changes. Leave without saving?')) blocker.proceed()
    else blocker.reset()
  }, [blocker])

  useEffect(() => {
    if (!active) return
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [active])
}

function handleError(error: unknown, setInputErrors: (errors: DraftErrors) => void, setActionError: (message: string) => void) {
  // The API's 400s are keyed by the same field names the form uses.
  if (error instanceof ApiError && error.status === 400 && error.problem?.errors) {
    setInputErrors(Object.fromEntries(Object.entries(error.problem.errors).map(([field, messages]) => [field, messages[0]])))
  }
  setActionError(error instanceof Error ? error.message : 'Something went wrong.')
}

function groupByField(issues: ValidationIssue[]): Partial<Record<string, ValidationIssue[]>> {
  const groups: Partial<Record<string, ValidationIssue[]>> = {}
  for (const issue of issues) (groups[issue.field] ??= []).push(issue)
  return groups
}

const noticeTones = {
  info: 'bg-sky-50 text-sky-900 ring-sky-200',
  success: 'bg-emerald-50 text-emerald-900 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-900 ring-amber-200',
  error: 'bg-rose-50 text-rose-900 ring-rose-200',
}

function Notice({ tone, children }: { tone: keyof typeof noticeTones; children: ReactNode }) {
  return <div className={`rounded-lg px-4 py-3 text-sm ring-1 ${noticeTones[tone]}`}>{children}</div>
}

const buttonVariants = {
  primary: 'bg-accent-600 text-white hover:bg-accent-700 disabled:bg-accent-600/50',
  secondary: 'border border-slate-300 bg-white text-slate-800 hover:bg-slate-50 disabled:text-slate-400',
  danger: 'border border-rose-200 bg-white text-rose-700 hover:bg-rose-50 disabled:text-rose-300',
}

interface ButtonProps {
  variant: keyof typeof buttonVariants
  onClick: () => void
  children: ReactNode
  disabled?: boolean
  busy?: boolean
  title?: string
}

function Button({ variant, onClick, children, disabled = false, busy = false, title }: ButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled || busy}
      title={title}
      className={`inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium transition-colors disabled:cursor-not-allowed ${buttonVariants[variant]}`}
    >
      {busy && <span className="size-3.5 animate-spin rounded-full border-2 border-current border-t-transparent" aria-hidden />}
      {children}
    </button>
  )
}
