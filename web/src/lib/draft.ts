import type { Invoice, InvoiceUpdate } from '../api/types'

/** Form state for the review screen. Everything is text while being edited. */
export interface InvoiceDraft {
  supplier: string
  invoiceNumber: string
  date: string
  currency: string
  net: string
  vat: string
  total: string
  lines: LineDraft[]
}

export interface LineDraft {
  /** Stable React key; server ids don't survive a save (lines are replaced). */
  key: string
  description: string
  qty: string
  unitPrice: string
  lineTotal: string
}

export type DraftErrors = Record<string, string>

export function draftFromInvoice(invoice: Invoice): InvoiceDraft {
  return {
    supplier: invoice.supplier ?? '',
    invoiceNumber: invoice.invoiceNumber ?? '',
    date: invoice.date ?? '',
    currency: invoice.currency ?? '',
    net: amountText(invoice.net),
    vat: amountText(invoice.vat),
    total: amountText(invoice.total),
    lines: invoice.lines.map((line) => ({
      key: line.id,
      description: line.description,
      qty: numberText(line.qty),
      unitPrice: numberText(line.unitPrice),
      lineTotal: amountText(line.lineTotal),
    })),
  }
}

export function emptyLine(): LineDraft {
  return { key: crypto.randomUUID(), description: '', qty: '1', unitPrice: '', lineTotal: '' }
}

/** True when the draft differs from the saved invoice (ignoring the React-only line keys). */
export function isDirty(draft: InvoiceDraft, invoice: Invoice): boolean {
  const strip = (d: InvoiceDraft) => ({ ...d, lines: d.lines.map(({ key: _key, ...rest }) => rest) })
  return JSON.stringify(strip(draft)) !== JSON.stringify(strip(draftFromInvoice(invoice)))
}

/**
 * Turns the draft into an API update, or input errors keyed like the API's field names.
 * Empty header amounts become null (the validation rules flag them); line amounts are required.
 */
export function draftToUpdate(draft: InvoiceDraft): { update: InvoiceUpdate } | { errors: DraftErrors } {
  const errors: DraftErrors = {}

  const optionalAmount = (field: string, text: string) => {
    if (text.trim() === '') return null
    const value = parseAmount(text)
    if (value === undefined) errors[field] = 'Not a number.'
    return value ?? null
  }

  const requiredAmount = (field: string, text: string) => {
    const value = parseAmount(text)
    if (value === undefined) errors[field] = text.trim() === '' ? 'Required.' : 'Not a number.'
    return value ?? 0
  }

  if (draft.date && !/^\d{4}-\d{2}-\d{2}$/.test(draft.date)) {
    errors.date = 'Use YYYY-MM-DD.'
  }

  const update: InvoiceUpdate = {
    supplier: blankToNull(draft.supplier),
    invoiceNumber: blankToNull(draft.invoiceNumber),
    date: blankToNull(draft.date),
    currency: blankToNull(draft.currency)?.toUpperCase() ?? null,
    net: optionalAmount('net', draft.net),
    vat: optionalAmount('vat', draft.vat),
    total: optionalAmount('total', draft.total),
    lines: draft.lines.map((line, i) => {
      if (line.description.trim() === '') errors[`lines[${i}].description`] = 'Required.'
      return {
        description: line.description.trim(),
        qty: requiredAmount(`lines[${i}].qty`, line.qty),
        unitPrice: requiredAmount(`lines[${i}].unitPrice`, line.unitPrice),
        lineTotal: requiredAmount(`lines[${i}].lineTotal`, line.lineTotal),
      }
    }),
  }

  return Object.keys(errors).length > 0 ? { errors } : { update }
}

/**
 * Parses what a reviewer types: "1234.5", "1,234.50", "-10", and a decimal comma ("94,99").
 * A single comma followed by exactly three digits ("1,234") is read as a thousands separator.
 * Returns undefined for anything else.
 */
export function parseAmount(text: string): number | undefined {
  let value = text.trim().replace(/\s/g, '')
  if (value === '') return undefined

  if (/^-?\d+,(\d{1,2}|\d{4})$/.test(value)) {
    value = value.replace(',', '.') // decimal comma
  } else {
    value = value.replace(/,(?=\d{3}(\D|$))/g, '') // thousands separators
  }

  if (!/^-?\d+(\.\d+)?$/.test(value)) return undefined
  return Number(value)
}

/** Sum of the line totals that parse, for the live "lines add up to" hint. */
export function linesSum(draft: InvoiceDraft): number {
  return draft.lines.reduce((sum, line) => sum + (parseAmount(line.lineTotal) ?? 0), 0)
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function amountText(value: number | null): string {
  return value === null ? '' : value.toFixed(2)
}

// Quantities and unit prices may have more decimals; drop only trailing zeros.
function numberText(value: number): string {
  return String(Number(value.toFixed(4)))
}
