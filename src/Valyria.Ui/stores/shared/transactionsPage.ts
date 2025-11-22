import type { Transaction } from "./transaction"

export interface TransactionsPage {
  totalCount: number
  results: Transaction[]
}