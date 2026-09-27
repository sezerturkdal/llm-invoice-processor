const locale = 'en-GB'

/** Formats an amount in its currency when the code is a valid ISO 4217 code, else as a plain number. */
export function formatMoney(amount: number | null, currency: string | null): string {
  if (amount === null) return '—'

  if (currency) {
    try {
      return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
    } catch {
      // Not a currency code Intl knows; fall through and show the code as text.
    }
  }

  const number = new Intl.NumberFormat(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount)
  return currency ? `${number} ${currency}` : number
}

/** Formats an ISO date (YYYY-MM-DD) without shifting it through the local time zone. */
export function formatDate(isoDate: string | null): string {
  if (!isoDate) return '—'
  const [year, month, day] = isoDate.split('-').map(Number)
  return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, month - 1, day)),
  )
}

/** Estimated API cost in USD; cents alone would hide what a single extraction costs. */
export function formatUsd(amount: number | null): string {
  if (amount === null) return '—'
  const digits = amount !== 0 && Math.abs(amount) < 1 ? 4 : 2
  return new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', currencyDisplay: 'narrowSymbol', minimumFractionDigits: digits, maximumFractionDigits: digits }).format(amount)
}

/** "850 ms", "7.4 s". */
export function formatDuration(ms: number | null): string {
  if (ms === null) return '—'
  if (ms < 1000) return `${Math.round(ms)} ms`
  return `${(ms / 1000).toFixed(1)} s`
}

/** "5,523", or "1.2M" once counts get large. */
export function formatCount(value: number | null): string {
  if (value === null) return '—'
  return new Intl.NumberFormat(locale, value >= 1_000_000 ? { notation: 'compact', maximumFractionDigits: 1 } : {}).format(value)
}

const relative = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })

/** "2 minutes ago", "yesterday"; falls back to a date after a week. */
export function formatRelative(timestamp: string, now = Date.now()): string {
  const seconds = Math.round((new Date(timestamp).getTime() - now) / 1000)
  const abs = Math.abs(seconds)

  if (abs < 60) return relative.format(seconds, 'second')
  if (abs < 3600) return relative.format(Math.round(seconds / 60), 'minute')
  if (abs < 86400) return relative.format(Math.round(seconds / 3600), 'hour')
  if (abs < 7 * 86400) return relative.format(Math.round(seconds / 86400), 'day')
  return new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(timestamp))
}
