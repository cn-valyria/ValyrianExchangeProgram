import { defineStore } from 'pinia'
import { useAuthStore } from './auth'

interface Account {
  nationId: number
  rulerName: string
  role: string
  allianceId: number
  allianceAcronym: string
  discord: string | null
  discordUniqueId: string | null
  hasForeignMinistry: boolean
  hasFederalAidCommission: boolean
  hasDisasterReliefAgency: boolean
}

interface AidItem {
  id: number
  nationId: number
  rulerName: string
  allianceId: number
  allianceAcronym: string
  money: number
  technology: number
}

interface Transaction {
  id: number
  aidId: number | null
  sentBy: {
    nationId: number
    rulerName: string
    allianceId: number
    allianceAcronym: string
  } | null
  receivedBy: {
    nationId: number
    rulerName: string
    allianceId: number
    allianceAcronym: string
  } | null
  status: string
  rate: number
  money: number | null
  technology: number | null
  reason: string | null
  classification: string | null
  code: {
    sendingRole?: string
    receivingRole?: string
  } | null
  cashMovedTechCredit: number
  cashMovedCashCredit: number
  techMovedCashCredit: number
  techMovedTechCredit: number
  startsOn: string
}

interface CreditHistoryItem {
  id: number
  otherNation: {
    nationId: number
    rulerName: string
    allianceId: number
    allianceAcronym: string
  } | null
  type: string
  status: string
  rate: number
  money: number | null
  technology: number | null
  balanceChangeType: string
  sentOn: string
}

export const useAccountStore = defineStore('account', {
  state: () => ({
    account: {} as Account,
    aidList: [] as AidItem[],
    transactionHistory: [] as Transaction[],
  }),

  getters: {
    creditHistory: (state): CreditHistoryItem[] => {
      const account = state.account
      if (account.nationId === undefined) {
        return []
      }

      const transactions = state.transactionHistory
      return transactions.map((txn) => ({
        id: txn.id,
        otherNation: buildOtherNation(txn, account),
        type: buildTransactionType(txn, account),
        status: txn.status,
        rate: txn.rate,
        money: txn.aidId === null ? null : txn.money,
        technology: txn.aidId === null ? null : txn.technology,
        balanceChangeType: buildBalanceChangeType(txn, account),
        sentOn: txn.startsOn,
      }))
    },
  },

  actions: {
    async loadAccount(accountId: number) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/accounts/${accountId}`, {
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
      })

      if (error.value) {
        console.error('Failed to load account:', error.value)
        throw error.value
      }

      if (data.value) {
        this.account = data.value as Account
      }
    },

    async loadAidList(accountId: number) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(
        `${config.public.apiUrl}/api/accounts/${accountId}/aidList`,
        {
          headers: {
            Authorization: `Bearer ${authStore.accessToken}`,
          },
        }
      )

      if (error.value) {
        console.error('Failed to load aid list:', error.value)
        throw error.value
      }

      if (data.value) {
        this.aidList = data.value as AidItem[]
      }
    },

    async loadTransactionHistory(accountId: number) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(
        `${config.public.apiUrl}/api/accounts/${accountId}/transactions`,
        {
          headers: {
            Authorization: `Bearer ${authStore.accessToken}`,
          },
        }
      )

      if (error.value) {
        console.error('Failed to load transaction history:', error.value)
        throw error.value
      }

      if (data.value) {
        this.transactionHistory = data.value as Transaction[]
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
