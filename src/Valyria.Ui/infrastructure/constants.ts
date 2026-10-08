export const ACCOUNT_ROLE_CODES = {
  buyer: 'B',
  seller: 'S',
  cashDonor: 'D',
  techFarm: 'F',
  cashCollector: 'C',
  techReceiver: 'R',
  probationarySeller: 'N',
  temporaryDonor: 'P',
  temporaryCollector: 'V',
  temporaryFarm: 'Q',
  temporaryReceiver: 'W',
  onHold: 'H',
} as const

export const TRANSACTION_TYPES = {
  aidBased: 'AidBased',
  manual: 'Manual',
  all: 'All',
} as const

export const ADJUSTMENT_TYPES = {
  credit: 'Credit',
  debt: 'Debt',
} as const

export const ERROR_MESSAGES = {
  default:
    'VEP encountered an error while saving. Please try again, or contact @lilweirdward if the error persists.',
} as const

// A deal slot's cycle values count these packages per 10-day period
export const AID_PACKAGE = {
  cashMillions: 9,
  tech: 100,
} as const
