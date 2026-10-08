import { AID_PACKAGE } from './constants'

export type DealState = 'flagged' | 'needsAction' | 'atRisk' | 'new' | 'active'

// Mirrors the thresholds in DealsRepository.GetAccountDealsAsync
const DAYS_UNTIL_AT_RISK = 1
const DAYS_UNTIL_NEEDS_ACTION = 5

// Turns a slot's cycle counts into text like "100 tech + $9M"
export function describeAid(techCycles: number, cashCycles: number): string {
  const parts: string[] = []
  if (techCycles > 0) parts.push(`${techCycles * AID_PACKAGE.tech} tech`)
  if (cashCycles > 0) parts.push(`$${cashCycles * AID_PACKAGE.cashMillions}M`)
  return parts.length > 0 ? parts.join(' + ') : 'Nothing'
}

export function getDealStateFromActivity(
  hasOpenFlag: boolean,
  daysSinceActivity: number,
  lastActivityDate: string | null
): DealState {
  if (hasOpenFlag) return 'flagged'
  if (daysSinceActivity >= DAYS_UNTIL_NEEDS_ACTION) return 'needsAction'
  if (daysSinceActivity >= DAYS_UNTIL_AT_RISK) return 'atRisk'
  if (lastActivityDate === null) return 'new'
  return 'active'
}

export function getDealStateLabel(state: DealState): string {
  switch (state) {
    case 'flagged': return 'Flagged'
    case 'needsAction': return 'Needs Action'
    case 'atRisk': return 'At Risk'
    case 'new': return 'New'
    default: return 'Active'
  }
}

export function getDealStateColor(state: DealState): 'error' | 'warning' | 'info' | 'success' {
  switch (state) {
    case 'flagged':
    case 'needsAction': return 'error'
    case 'atRisk': return 'warning'
    case 'new': return 'info'
    default: return 'success'
  }
}
