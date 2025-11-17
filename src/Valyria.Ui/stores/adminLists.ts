import { defineStore } from 'pinia'
import { useAuthStore } from './auth'
import type { RecentActivity } from './shared/enums'
import type { NationSimplified } from './shared/nationSimplified'

export interface ListRecipient {
  nation: NationSimplified
  slotsFree: number
  credit: number
  debt: number
  recentActivity: RecentActivity
  discord: string
}

export interface AidList {
  id: number
  name: string
  subject: string
  message: string
  recipients: ListRecipient[]
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
