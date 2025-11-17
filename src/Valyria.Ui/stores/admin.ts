import { defineStore } from 'pinia'
import { useAuthStore } from './auth'

export interface TransactionCountByDay {
  dayTxnRecorded: string
  txnCount: number
}

export interface CreditGroup {
  dateRange: string
  totalCredit: number
}

export interface CreditGroupsByAlliance {
  allianceName: string
  creditGroups: CreditGroup[]
}

export interface AccountCountByRole {
  role: string
  totalAccounts: number
}

export interface TotalSlotCountByRole {
  role: string
  totalSlots: number
}

export interface FreeSlotCountByList {
  listName: string
  freeSlots: number
}

export interface AccountCountByAlliance {
  allianceName: string
  totalAccounts: number
}

export interface Dashboard {
  totalAccounts: number
  totalBuyers: number
  totalSellers: number
  totalTransactions: number
  unusedCredit: number
  transactionsPerDay: TransactionCountByDay[]
  pendingCreditByAlliance: CreditGroupsByAlliance[]
  accountsPerRole: AccountCountByRole[]
  totalSlotsPerRole: TotalSlotCountByRole[]
  freeSlotsPerList: FreeSlotCountByList[]
  accountsPerAlliance: AccountCountByAlliance[]
}

export const useAdminStore = defineStore('admin', {
  state: () => ({
    dashboard: {} as Dashboard,
  }),

  actions: {
    async loadDashboard() {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/admin/dashboard`, {
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
      })

      if (error.value) {
        console.error('Failed to load dashboard:', error.value)
        throw error.value
      }

      if (data.value) {
        this.dashboard = data.value as Dashboard
      }
    },
  },
})
