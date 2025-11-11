import { defineStore } from 'pinia'
import { useAuthStore } from './auth'

interface Dashboard {
  totalAccounts: number
  totalTransactions: number
  ratios: {
    buyerSellerRatio: number
    donorRecipientRatio: number
  }
  statistics: Record<string, any>
}

export const useAdminStore = defineStore('admin', {
  state: () => ({
    dashboard: {} as Dashboard,
  }),

  actions: {
    async loadDashboard() {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/dashboard`, {
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
