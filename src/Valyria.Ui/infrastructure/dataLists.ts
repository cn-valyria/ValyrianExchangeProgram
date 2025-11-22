export interface TransactionClassification {
  id: number
  description: string
}

export interface AccountRole {
  code: string
  description: string
}

export interface RecentActivityValue {
  id: number
  description: string
}

export interface AllianceAcronym {
  code: string,
  name: string
}

export const transactionClassifications: TransactionClassification[] = [
  { id: 0, description: 'Non-VEP aid' },
  { id: 1, description: 'VEP aid' },
  { id: 2, description: 'Credit to B or W' },
  { id: 3, description: 'Credit to S or N or Q' },
  { id: 4, description: 'Credit for wonder' },
  { id: 5, description: 'Debt to B or P' },
  { id: 6, description: 'Debt to S or N or V' },
]

export const accountRoles: AccountRole[] = [
  { code: 'B', description: 'Buyer' },
  { code: 'S', description: 'Seller' },
  { code: 'D', description: 'Cash Donor' },
  { code: 'F', description: 'Tech Farm' },
  { code: 'C', description: 'Cash Collector' },
  { code: 'R', description: 'Tech Receiver' },
  { code: 'N', description: 'Probationary Seller' },
  { code: 'P', description: 'Temporary Donor' },
  { code: 'V', description: 'Temporary Collector' },
  { code: 'Q', description: 'Temporary Farm' },
  { code: 'W', description: 'Temporary Receiver' },
  { code: 'H', description: 'On Hold' },
]

export const recentActivityValues: RecentActivityValue[] = [
  { id: 1, description: 'Last 3 Days' },
  { id: 2, description: 'This Week' },
  { id: 3, description: 'Last Week' },
  { id: 4, description: 'Three Weeks Ago' },
  { id: 5, description: 'More Than 3 Weeks Ago' },
]

export const allianceAcronyms: AllianceAcronym[] = [
  { code: 'CCC', name: 'Christian Coalition Of Countries' },
  { code: 'FAN', name: 'Federation of Armed Nations' },
  { code: 'FARK', name: 'Fark' },
  { code: 'FTW', name: 'Freehold of the Wolves' },
  { code: 'MHA', name: 'Mostly Harmless Alliance' },
  { code: 'NATO', name: 'NATO' },
  { code: 'NG', name: 'Non Grata' },
  { code: 'OG', name: 'Old Guard' },
  { code: 'RIA', name: 'Random Insanity Alliance' },
  { code: 'TIO', name: 'The Imperial Order' },
]
