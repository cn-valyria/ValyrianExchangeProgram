import { defineStore } from 'pinia'
import type { NationalWarStatus, RecentActivity } from './shared/enums'

export interface AdminAccount {
  id: number
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
  discord: string
  discordUniqueId: bigint
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

export const useAdminAccountsStore = defineStore('adminAccounts', {
  state: () => ({
    accounts: [] as AdminAccount[],
  }),

  actions: {
    async loadAllAccounts(includeOnHold: boolean = false) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AdminAccount[]>(`/api/accounts?includeOnHold=${includeOnHold}`)

      if (error.value) {
        console.error('Failed to load accounts:', error.value)
        throw error.value
      }

      if (data.value) {
        this.accounts = data.value as AdminAccount[]
      }
    },

    async createAccount(payload: Partial<AdminAccount>) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AdminAccount>('/api/accounts', {
        method: 'POST',
        body: payload,
      })

      if (error.value) {
        console.error('Failed to create account:', error.value)
        throw error.value
      }

      if (data.value) {
        this.accounts.push(data.value as AdminAccount)
      }
    },

    async updateAccount(payload: Partial<AdminAccount> & { id: number }) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<void>('/api/accounts', {
        method: 'PUT',
        body: payload,
      })

      if (error.value) {
        console.error('Failed to update account:', error.value)
        throw error.value
      }

      const index = this.accounts.findIndex((a) => a.id === payload.id)
      if (index !== -1) {
        const updatedAccount = {
          ...this.accounts[index],
          role: payload.role,
          uniqueCode: payload.uniqueCode,
          discord: payload.discord,
          discordUniqueId: payload.discordUniqueId,
          hasForeignMinistry: payload.hasForeignMinistry,
          hasFederalAidCommission: payload.hasFederalAidCommission,
          hasDisasterReliefAgency: payload.hasDisasterReliefAgency,
        }
        this.accounts[index] = updatedAccount as AdminAccount
      }
    },

    async deleteAccount(id: number) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<void>(`/api/accounts/${id}`, {
        method: 'DELETE',
      })

      if (error.value) {
        console.error('Failed to delete account:', error.value)
        throw error.value
      }

      const index = this.accounts.findIndex((a) => a.id === id)
      if (index !== -1) {
        this.accounts.splice(index, 1)
      }
    },
  },
})
