import type { AidStatus } from "./enums"
import type { NationSimplified } from "./nationSimplified"

export interface Transaction {
  id: number
  aidId: number | null
  sentBy: NationSimplified | null
  receivedBy: NationSimplified | null
  status: AidStatus
  money: number
  technology: number
  soldiers: number
  reason: string
  startsOn: string | null
  endsOn: string | null
  code: TransactionCode
  classification: number
  rate: number
  cashMovedTechCredit: number
  cashMovedCashCredit: number
  techMovedCashCredit: number
  techMovedTechCredit: number
}