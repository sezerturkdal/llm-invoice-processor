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
