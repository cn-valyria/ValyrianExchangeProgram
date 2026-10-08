import { defineStore } from 'pinia'
import type { AccountSimplified, SlotDetail } from './account'

export interface AdminDeal {
  id: number
  createdOn: string
  createdBy: string | null
  lastActivityDate: string | null
  daysSinceActivity: number
  hasOpenFlag: boolean
  slot1: SlotDetail
  slot2: SlotDetail
}

export interface AdminDealPage {
  totalCount: number
  results: AdminDeal[]
}

export interface WaitQueueEntry {
  accountId: number
  slotsNeeded: number
  enteredQueueOn: string
  nextSlotOpensOn: string | null
  priorityScore: number
  account: AccountSimplified
}

// Mirrors Valyria.Core.Entities.DealFlagType, which is serialized as a number
export enum DealFlagType {
  OneDayInactive = 0,
  FiveDayReplacement = 1,
  AidFailed = 2,
}

export interface DealFlag {
  id: number
  dealId: number
  flagType: DealFlagType
  flaggedAccount: AccountSimplified
  innocentAccount: AccountSimplified
  flaggedOn: string
  resolvedOn: string | null
  resolvedBy: string | null
  adminNotes: string | null
}

export const useAdminDealsStore = defineStore('adminDeals', {
  state: () => ({
    activeDeals: { totalCount: 0, results: [] } as AdminDealPage,
    waitQueue: [] as WaitQueueEntry[],
    flags: [] as DealFlag[],
  }),

  actions: {
    async loadActiveDeals(search?: string, limit?: number, offset?: number) {
      const { apiFetch } = useApiClient()

      const params = new URLSearchParams()
      if (search) params.append('search', search)
      if (limit !== undefined) params.append('limit', limit.toString())
      if (offset !== undefined) params.append('offset', offset.toString())

      const url = `/api/deals/admin/active${params.toString().length > 0 ? '?' + params.toString() : ''}`
      const { data, error } = await apiFetch<AdminDealPage>(url)

      if (error.value) {
        console.error('Failed to load active deals:', error.value)
        throw error.value
      }

      if (data.value) {
        this.activeDeals = data.value as AdminDealPage
      }
    },

    async loadWaitQueue() {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<WaitQueueEntry[]>('/api/deals/admin/wait-queue')

      if (error.value) {
        console.error('Failed to load wait queue:', error.value)
        throw error.value
      }

      if (data.value) {
        this.waitQueue = data.value as WaitQueueEntry[]
      }
    },

    async loadFlags() {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<DealFlag[]>('/api/deals/admin/flagged')

      if (error.value) {
        console.error('Failed to load flagged deals:', error.value)
        throw error.value
      }

      if (data.value) {
        this.flags = data.value as DealFlag[]
      }
    },

    async cancelDeal(dealId: number, reason: string) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<void>(`/api/deals/${dealId}/cancel`, {
        method: 'PUT',
        body: { reason },
      })

      if (error.value) {
        console.error('Failed to cancel deal:', error.value)
        throw error.value
      }
    },

    async createDeal(slot1Id: number, slot2Id: number) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<{ id: number }>('/api/deals', {
        method: 'POST',
        body: { slot1Id, slot2Id },
      })

      if (error.value) {
        console.error('Failed to create deal:', error.value)
        throw error.value
      }
    },

    async resolveFlag(flagId: number, adminNotes: string) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<void>(`/api/deals/admin/flags/${flagId}/resolve`, {
        method: 'PUT',
        body: { adminNotes },
      })

      if (error.value) {
        console.error('Failed to resolve flag:', error.value)
        throw error.value
      }

      this.flags = this.flags.filter((flag) => flag.id !== flagId)
    },

    async loadAvailableSlots(accountId: number): Promise<SlotDetail[]> {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<SlotDetail[]>(`/api/slots/available/${accountId}`)

      if (error.value) {
        console.error('Failed to load available slots:', error.value)
        throw error.value
      }

      return (data.value as SlotDetail[]) ?? []
    },
  },
})
