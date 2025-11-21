import { defineStore } from 'pinia'
import { useAuthStore } from './auth'
import { ADJUSTMENT_TYPES } from '~/infrastructure/constants'
import type { NationSimplified } from './shared/nationSimplified'
import type { Transaction } from './shared/transaction'

export interface TransactionsPage {
  totalCount: number
  data: Transaction[]
}

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
      data: [],
    } as TransactionsPage,
  }),

  getters: {
    aidBasedTransactions: (state): Transaction[] => {
      const data = state.currentTransactionsPage.data
      return data.some((txn) => txn.aidId === null) ? [] : data
    },

    manualTransactions: (state): ManualTransaction[] => {
      const data = state.currentTransactionsPage.data
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
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const urlParams = toURLSearchParams(params.filter, params.limit, params.offset)
      const url = `${config.public.apiUrl}/api/transactions/${params.type}${
        urlParams.toString().length > 0 ? '?' + urlParams.toString() : ''
      }`

      const { data, error } = await useFetch(url, {
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
      })

      if (error.value) {
        console.error('Failed to load transactions:', error.value)
        throw error.value
      }

      if (data.value) {
        const response = data.value as { results: Transaction[]; totalCount: number }
        this.currentTransactionsPage.data = response.results
        this.currentTransactionsPage.totalCount = response.totalCount
      }
    },

    async createTransaction(payload: Partial<Transaction>) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/transactions`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
        body: payload,
      })

      if (error.value) {
        console.error('Failed to create transaction:', error.value)
        throw error.value
      }

      console.log('Transaction created:', data.value)
    },

    async updateTransaction(payload: Partial<Transaction> & { id: number }) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/transactions`, {
        method: 'PUT',
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
        body: payload,
      })

      if (error.value) {
        console.error('Failed to update transaction:', error.value)
        throw error.value
      }

      console.log('Transaction updated:', data.value)
    },

    async deleteTransaction(id: number) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { error } = await useFetch(`${config.public.apiUrl}/api/transactions/${id}`, {
        method: 'DELETE',
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
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
