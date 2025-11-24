import type { NationSimplified } from './nationSimplified'

export interface ListPerformance {
  nation: NationSimplified
  role: string
  listType: string
  dayStarted: string
  daysUntilRemoved: number
}
