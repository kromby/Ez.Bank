const CURRENCY_DECIMALS: Record<string, number> = {
  ISK: 0,
  EUR: 2,
  USD: 2,
  GBP: 2,
  DKK: 2,
  SEK: 2,
  NOK: 2,
}

export function formatAmount(amount: number | undefined | null, currencyCode: string): string {
  if (amount == null) return '0'
  const decimals = CURRENCY_DECIMALS[currencyCode] ?? 0
  const value = decimals > 0 ? amount / Math.pow(10, decimals) : amount
  return new Intl.NumberFormat('is-IS', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  }).format(value)
}

export function CurrencyAmount({ amount, currencyCode }: { amount: number; currencyCode: string }) {
  return (
    <span>
      {formatAmount(amount, currencyCode)} {currencyCode}
    </span>
  )
}
