import { defineStore } from 'pinia'
import type { RecentActivity } from './shared/enums'
import type { NationSimplified } from './shared/nationSimplified'

export interface ListRecipient {
  nation: NationSimplified
  slotsFree: number
  credit: number
  debt: number
  recentActivity: RecentActivity
  discord: string
  daysOnCurrentList: number
}

export interface AccountSimplified extends NationSimplified {
  accountId: number
}

export interface AidList {
  id: number
  name: string
  subject: string
  message: string
  recipients: ListRecipient[]
  recipientsLastUpdatedOn: Date
  messagesSentOn: Date
  messagesSentBy: AccountSimplified
}

export const useAdminListsStore = defineStore('adminLists', {
  state: () => ({
    lists: [] as AidList[],
  }),

  actions: {
    async getAllAidLists() {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AidList[]>('/api/lists')

      if (error.value) {
        console.error('Failed to load lists:', error.value)
        throw error.value
      }

      if (data.value) {
        this.lists = data.value as AidList[]
      }
    },

    async updateMessagesSent(listId: number, messagesSentOn: Date | null, messagesSentByAccountId: number | null) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AidList>(`/api/lists/${listId}`, {
        method: 'PUT',
        body: {
          messagesSentOn,
          messagesSentByAccountId
        }
      })

      if (error.value) {
        console.error('Failed to update messages sent:', error.value)
        throw error.value
      }

      if (data.value) {
        // Update the list in the store
        const index = this.lists.findIndex(l => l.id === listId)
        if (index !== -1) {
          this.lists[index] = data.value as AidList
        }
      }

      return data.value
    },
  },
})
