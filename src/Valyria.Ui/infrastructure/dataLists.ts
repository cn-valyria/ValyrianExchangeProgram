export interface AccountRole {
  code: string
  description: string
}

export interface AllianceAcronym {
  code: string
  name: string
}

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

export type AccountRoleColor = 'warning' | 'error' | 'info' | 'neutral'

// Badge color for a role, shared by the role chip and the admin accounts table
export function getAccountRoleColor(
  code: string | undefined,
): AccountRoleColor {
  switch (code) {
    case 'B': // Buyer
    case 'D': // Cash Donor
    case 'W': // Temporary Receiver
      return 'warning'
    case 'S': // Seller
    case 'F': // Tech Farm
    case 'N': // Probationary Seller
    case 'Q': // Temporary Farm
      return 'error'
    case 'C': // Cash Collector
    case 'R': // Tech Receiver
    case 'P': // Temporary Donor
    case 'V': // Temporary Collector
      return 'info'
    default: // On Hold or unknown
      return 'neutral'
  }
}

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
