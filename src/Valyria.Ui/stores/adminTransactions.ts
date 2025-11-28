import { defineStore } from 'pinia'
import { ADJUSTMENT_TYPES } from '~/infrastructure/constants'
import type { NationSimplified } from './shared/nationSimplified'
import type { Transaction } from './shared/transaction'
import type { TransactionsPage } from './shared/transactionsPage'

export interface TransactionFilter {
  sentBy?: number
  receivedBy?: number
  sentSince?: Date
  sentUntil?: Date
}

export interface ManualTransaction {
  id: number
  nation: NationSimplified
  adjustmentType: string
  reason: string
  accountCode: string
  classification: number
  rate: number
  cashMovedTechCredit: number
  cashMovedCashCredit: number
  techMovedCashCredit: number
  techMovedTechCredit: number
}

export const useAdminTransactionsStore = defineStore('adminTransactions', {
  state: () => ({
    currentTransactionsPage: {
      totalCount: 0,
      results: [],
    } as TransactionsPage,
  }),

  getters: {
    aidBasedTransactions: (state): Transaction[] => {
      const data = state.currentTransactionsPage.results
      return data.some((txn) => txn.aidId === null) ? [] : data
    },

    manualTransactions: (state): ManualTransaction[] => {
      const data = state.currentTransactionsPage.results
      return data.some((txn) => txn.aidId !== null)
        ? []
        : data.map((txn) => ({
            id: txn.id,
            nation: (txn.sentBy !== null ? txn.sentBy : txn.receivedBy)!,
            adjustmentType: txn.sentBy !== null ? ADJUSTMENT_TYPES.credit : ADJUSTMENT_TYPES.debt,
            reason: txn.reason,
            accountCode: (txn.code?.sendingRole ?? '') + (txn.code?.receivingRole ?? ''),
            classification: txn.classification,
            rate: txn.rate,
            cashMovedTechCredit: txn.cashMovedTechCredit,
            cashMovedCashCredit: txn.cashMovedCashCredit,
            techMovedCashCredit: txn.techMovedCashCredit,
            techMovedTechCredit: txn.techMovedTechCredit,
          }))
    },
  },

  actions: {
    async reloadCurrentTransactionsPage(params: {
      type: string
      filter?: TransactionFilter
      limit?: number
      offset?: number
    }) {
      const { apiFetch } = useApiClient()

      const urlParams = toURLSearchParams(params.filter, params.limit, params.offset)
      const url = `/api/transactions/${params.type}${
        urlParams.toString().length > 0 ? '?' + urlParams.toString() : ''
      }`

      const { data, error } = await apiFetch<{ results: Transaction[]; totalCount: number }>(url)

      if (error.value) {
        console.error('Failed to load transactions:', error.value)
        throw error.value
      }

      if (data.value) {
        const response = data.value as { results: Transaction[]; totalCount: number }
        this.currentTransactionsPage.results = response.results
        this.currentTransactionsPage.totalCount = response.totalCount
      }
    },

    async createTransaction(payload: Partial<Transaction>) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<Transaction>('/api/transactions', {
        method: 'POST',
        body: payload,
      })

      if (error.value) {
        console.error('Failed to create transaction:', error.value)
        throw error.value
      }

      console.log('Transaction created:', data.value)
    },

    async updateTransaction(payload: Partial<Transaction> & { id: number }) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<Transaction>('/api/transactions', {
        method: 'PUT',
        body: payload,
      })

      if (error.value) {
        console.error('Failed to update transaction:', error.value)
        throw error.value
      }

      console.log('Transaction updated:', data.value)
    },

    async deleteTransaction(id: number) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<void>(`/api/transactions/${id}`, {
        method: 'DELETE',
      })

      if (error.value) {
        console.error('Failed to delete transaction:', error.value)
        throw error.value
      }

      console.log('Transaction deleted:', id)
    },
  },
})

function toURLSearchParams(
  filter?: TransactionFilter,
  limit?: number,
  offset?: number
): URLSearchParams {
  const params = new URLSearchParams()
  if (filter) {
    if (filter.sentBy) params.append('sentBy', filter.sentBy.toString())
    if (filter.receivedBy) params.append('receivedBy', filter.receivedBy.toString())
    if (filter.sentSince) params.append('sentSince', filter.sentSince.toDateString())
    if (filter.sentUntil) params.append('sentUntil', filter.sentUntil.toDateString())
  }
  if (limit) params.append('limit', limit.toString())
  if (offset) params.append('offset', offset.toString())

  return params
}
