import { defineStore } from 'pinia'
import { useAuthStore } from './auth'

interface Account {
  id: number
  nationId: number
  rulerName: string
  role: string
  allianceId: number
  allianceAcronym: string
  uniqueCode: string
  discord: string | null
  discordUniqueId: string | null
  hasForeignMinistry: boolean
  hasFederalAidCommission: boolean
  hasDisasterReliefAgency: boolean
}

export const useAdminAccountsStore = defineStore('adminAccounts', {
  state: () => ({
    accounts: [] as Account[],
  }),

  actions: {
    async loadAllAccounts() {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/accounts`, {
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
      })

      if (error.value) {
        console.error('Failed to load accounts:', error.value)
        throw error.value
      }

      if (data.value) {
        this.accounts = data.value as Account[]
      }
    },

    async createAccount(payload: Partial<Account>) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/accounts`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
        body: payload,
      })

      if (error.value) {
        console.error('Failed to create account:', error.value)
        throw error.value
      }

      if (data.value) {
        this.accounts.push(data.value as Account)
      }
    },

    async updateAccount(payload: Partial<Account> & { id: number }) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/accounts`, {
        method: 'PUT',
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
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
        this.accounts[index] = updatedAccount as Account
      }
    },

    async deleteAccount(id: number) {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { error } = await useFetch(`${config.public.apiUrl}/api/accounts/${id}`, {
        method: 'DELETE',
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
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
