import { defineStore } from 'pinia'
import type { AidStatus, NationalWarStatus, RecentActivity } from './shared/enums'
import type { NationSimplified } from './shared/nationSimplified'
import type { Transaction } from './shared/transaction'
import type { TransactionsPage } from './shared/transactionsPage'
import type { ListPerformance } from './shared/listPerformance'

export interface Account {
  id: number
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
  discord: string
  discordUniqueId: number
  uniqueCode: string
  role: string
  activity: RecentActivity
  strength: number
  infra: number
  tech: number
  warStatus: NationalWarStatus
  hasForeignMinistry: boolean
  hasFederalAidCommission: boolean
  hasDisasterReliefAgency: boolean
  slotsUsed: number
  slotsFull: number
  totalCashSent: number
  totalCashReceived: number
  totalTechSent: number
  totalTechReceived: number
  credit: number
  debt: number
  cashSentTechCredit: number
  cashReceivedTechCredit: number
  cashSentCashCredit: number
  cashReceivedCashCredit: number
  techSentCashCredit: number
  techReceivedCashCredit: number
  techSentTechCredit: number
  techReceivedTechCredit: number
  previousListOrder: number
  totalTimesInDebt: number
  avgDaysToClearDebt: number
  totalTimesWithCredit: number
  avgDaysToFillCredit: number
}

export interface AidItem {
  nation: NationSimplified
  slotsFree: number
  credit: number
  debt: number
  recentActivity: RecentActivity
  discord: string
}

export interface TransactionCode {
  sendingRole: string | null
  receivingRole: string | null
}

export interface AccountTransactionHistory {
  currentPage: TransactionsPage,
  totalCreditTxns: number,
  totalDebtTxns: number
}

export interface CreditHistoryItem {
  id: number
  otherNation: NationSimplified | null
  type: string
  status: AidStatus
  rate: number
  money: number
  technology: number
  balanceChangeType: string
  sentOn: string
}

export const useAccountStore = defineStore('account', {
  state: () => ({
    account: {} as Account,
    aidList: [] as AidItem[],
    accountTransactionHistory: {
      currentPage: { totalCount: 0, results: [] } as TransactionsPage,
      totalCreditTxns: 0,
      totalDebtTxns: 0
    } as AccountTransactionHistory,
    listPerformance: [] as ListPerformance[],
  }),

  getters: {
    creditHistory: (state): CreditHistoryItem[] => {
      const account = state.account
      if (account.nationId === undefined) {
        return []
      }

      const transactions = state.accountTransactionHistory.currentPage.results
      return transactions.map((txn: Transaction) => ({
        id: txn.id,
        otherNation: buildOtherNation(txn, account),
        type: buildTransactionType(txn, account),
        status: txn.status,
        rate: txn.rate,
        money: txn.money,
        technology: txn.technology,
        balanceChangeType: buildBalanceChangeType(txn, account),
        sentOn: txn.startsOn || '',
      }))
    },
  },

  actions: {
    async loadAccount(accountId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<Account>(`/api/accounts/${accountId}`)

      if (error.value) {
        console.error('Failed to load account:', error.value)
        throw error.value
      }

      if (data.value) {
        this.account = data.value as Account
      }
    },

    async loadAidList(accountId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AidItem[]>(`/api/accounts/${accountId}/aidList`)

      if (error.value) {
        console.error('Failed to load aid list:', error.value)
        throw error.value
      }

      if (data.value) {
        this.aidList = data.value as AidItem[]
      }
    },

    async loadTransactionHistory(accountId: number, limit?: number, offset?: number) {
      const { apiFetch } = useApiClient()

      const params = new URLSearchParams()
      if (limit !== undefined) params.append('limit', limit.toString())
      if (offset !== undefined) params.append('offset', offset.toString())

      const url = `/api/accounts/${accountId}/transactions${
        params.toString().length > 0 ? '?' + params.toString() : ''
      }`

      const { data, error } = await apiFetch<AccountTransactionHistory>(url)

      if (error.value) {
        console.error('Failed to load transaction history:', error.value)
        throw error.value
      }

      if (data.value) {
        const response = data.value as AccountTransactionHistory
        this.accountTransactionHistory.currentPage = response.currentPage
        this.accountTransactionHistory.totalCreditTxns = response.totalCreditTxns
        this.accountTransactionHistory.totalDebtTxns = response.totalDebtTxns
      }
    },

    async loadListPerformance(accountId: number, includeIncoming?: boolean) {
      const { apiFetch } = useApiClient()

      const params = new URLSearchParams()
      if (includeIncoming !== undefined) params.append('includeIncoming', includeIncoming.toString())

      const url = `/api/accounts/${accountId}/listPerformance${
        params.toString().length > 0 ? '?' + params.toString() : ''
      }`

      const { data, error } = await apiFetch<ListPerformance[]>(url)

      if (error.value) {
        console.error('Failed to load list performance:', error.value)
        throw error.value
      }

      if (data.value) {
        this.listPerformance = data.value as ListPerformance[]
      }
    },
  },
})

// Helper functions
function buildOtherNation(txn: Transaction, account: Account) {
  if (txn.aidId === null) {
    return null
  } else if (txn.sentBy && txn.sentBy.nationId !== account.nationId) {
    return txn.sentBy
  } else if (txn.receivedBy && txn.receivedBy.nationId !== account.nationId) {
    return txn.receivedBy
  } else {
    throw new Error('Unable to find a valid nation for the given transaction and account.')
  }
}

function buildTransactionType(txn: Transaction, account: Account) {
  const incoming = 'Incoming'
  const outgoing = 'Outgoing'

  if (txn.aidId === null) {
    return txn.sentBy !== null ? outgoing : incoming
  } else if (txn.sentBy && txn.sentBy.nationId === account.nationId) {
    return outgoing
  } else if (txn.receivedBy && txn.receivedBy.nationId === account.nationId) {
    return incoming
  } else {
    throw new Error('Unable to determine the transaction type for the given transaction and account.')
  }
}

function buildBalanceChangeType(txn: Transaction, account: Account) {
  const credit = 'Credit'
  const debt = 'Debt'

  if (txn.aidId === null) {
    return txn.sentBy !== null ? credit : debt
  } else if (txn.sentBy && txn.sentBy.nationId === account.nationId) {
    return credit
  } else if (txn.receivedBy && txn.receivedBy.nationId === account.nationId) {
    return debt
  } else {
    throw new Error('Unable to determine the balance change type for the given transaction and account.')
  }
}
