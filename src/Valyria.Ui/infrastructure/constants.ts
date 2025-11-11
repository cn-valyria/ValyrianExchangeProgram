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
