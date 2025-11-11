import { defineStore } from 'pinia'
import { useAuthStore } from './auth'

interface AidList {
  id: number
  name: string
  description: string
  // Add other properties as needed based on your API response
}

export const useAdminListsStore = defineStore('adminLists', {
  state: () => ({
    lists: [] as AidList[],
  }),

  actions: {
    async getAllAidLists() {
      const config = useRuntimeConfig()
      const authStore = useAuthStore()

      const { data, error } = await useFetch(`${config.public.apiUrl}/api/lists`, {
        headers: {
          Authorization: `Bearer ${authStore.accessToken}`,
        },
      })

      if (error.value) {
        console.error('Failed to load lists:', error.value)
        throw error.value
      }

      if (data.value) {
        this.lists = data.value as AidList[]
      }
    },
  },
})
