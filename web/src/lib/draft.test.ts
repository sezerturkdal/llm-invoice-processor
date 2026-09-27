import { describe, expect, it } from 'vitest'
import type { Invoice } from '../api/types'
import { draftFromInvoice, draftToUpdate, emptyLine, isDirty, linesSum, parseAmount } from './draft'

const invoice: Invoice = {
  id: 'inv-1',
  fileName: 'invoice1.pdf',
  status: 'PendingReview',
  supplier: 'Acme Ltd',
  invoiceNumber: 'INV-1',
  date: '2026-07-05',
  currency: 'GBP',
  net: 94.99,
  vat: 19,
  total: 113.99,
  createdAt: '2026-09-26T22:47:16Z',
  reviewedAt: null,
  reviewedBy: null,
  modelUsed: 'claude-opus-5',
  lines: [
    { id: 'l1', description: 'Fridge', qty: 1, unitPrice: 94.99, lineTotal: 113.99 },
    { id: 'l2', description: 'Shipping', qty: 1, unitPrice: 0, lineTotal: 0 },
  ],
  validationIssues: [],
  extractionError: null,
  lastExtraction: null,
}

describe('parseAmount', () => {
  it.each([
    ['94.99', 94.99],
    ['94,99', 94.99],
    ['1,234.50', 1234.5],
    ['1,234', 1234],
    ['1,234,567.8', 1234567.8],
    ['-10', -10],
    [' 12 ', 12],
    ['0,1234', 0.1234],
  ])('reads %s as %d', (text, expected) => {
    expect(parseAmount(text)).toBe(expected)
  })

  it.each(['', '  ', 'abc', '£10', '1.2.3', '1,2,3'])('rejects %j', (text) => {
    expect(parseAmount(text)).toBeUndefined()
  })
})

describe('draft round trip', () => {
  it('formats amounts with two decimals for editing', () => {
    const draft = draftFromInvoice(invoice)

    expect(draft.vat).toBe('19.00')
    expect(draft.lines[0]).toMatchObject({ qty: '1', unitPrice: '94.99', lineTotal: '113.99' })
  })

  it('is not dirty until something changes', () => {
    const draft = draftFromInvoice(invoice)

    expect(isDirty(draft, invoice)).toBe(false)
    expect(isDirty({ ...draft, total: '113.98' }, invoice)).toBe(true)
  })

  it('converts back to an update with trimmed text and upper-case currency', () => {
    const draft = { ...draftFromInvoice(invoice), supplier: '  Acme Ltd ', currency: 'gbp' }

    const result = draftToUpdate(draft)

    expect(result).toEqual({
      update: {
        supplier: 'Acme Ltd',
        invoiceNumber: 'INV-1',
        date: '2026-07-05',
        currency: 'GBP',
        net: 94.99,
        vat: 19,
        total: 113.99,
        lines: [
          { description: 'Fridge', qty: 1, unitPrice: 94.99, lineTotal: 113.99 },
          { description: 'Shipping', qty: 1, unitPrice: 0, lineTotal: 0 },
        ],
      },
    })
  })

  it('sends cleared fields as null so validation can flag them', () => {
    const draft = { ...draftFromInvoice(invoice), supplier: ' ', net: '' }

    const result = draftToUpdate(draft)

    expect(result).toMatchObject({ update: { supplier: null, net: null } })
  })
})

describe('draftToUpdate errors', () => {
  it('reports bad numbers, dates and empty line descriptions by field', () => {
    const draft = {
      ...draftFromInvoice(invoice),
      total: 'lots',
      date: '05/07/2026',
      lines: [{ ...emptyLine(), description: ' ', unitPrice: '', lineTotal: 'x' }],
    }

    const result = draftToUpdate(draft)

    expect(result).toEqual({
      errors: {
        total: 'Not a number.',
        date: 'Use YYYY-MM-DD.',
        'lines[0].description': 'Required.',
        'lines[0].unitPrice': 'Required.',
        'lines[0].lineTotal': 'Not a number.',
      },
    })
  })
})

describe('linesSum', () => {
  it('adds the line totals that parse and ignores the rest', () => {
    const draft = draftFromInvoice(invoice)
    draft.lines.push({ ...emptyLine(), lineTotal: 'n/a' }, { ...emptyLine(), lineTotal: '-10,50' })

    expect(linesSum(draft)).toBeCloseTo(103.49)
  })
})
