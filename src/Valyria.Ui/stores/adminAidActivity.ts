import { defineStore } from 'pinia'
import type { AidStatus } from './shared/enums'
import type { NationSimplified } from './shared/nationSimplified'

// One side of an aid record. accountId and role are null when the nation isn't a VEP account.
export interface AidParty extends NationSimplified {
  accountId: number | null
  role: string | null
}

export interface AidActivityItem {
  aidId: number
  sentOn: string | null
  status: AidStatus
  money: number
  technology: number
  soldiers: number
  reason: string
  dealId: number | null
  sender: AidParty
  receiver: AidParty
}

export interface AidActivityPage {
  totalCount: number
  results: AidActivityItem[]
}

export type AidDirection = 'sent' | 'received'

export interface AidActivityFilter {
  search?: string
  direction?: AidDirection
  sentSince?: Date
  sentUntil?: Date
}

export const useAdminAidActivityStore = defineStore('adminAidActivity', {
  state: () => ({
    currentPage: { totalCount: 0, results: [] } as AidActivityPage,
    filters: {} as AidActivityFilter,
    includeOnHold: false,
    unmatchedOnly: false,
  }),

  actions: {
    async loadPage(limit: number, offset: number) {
      const { apiFetch } = useApiClient()

      const params = new URLSearchParams()
      if (this.filters.search) params.append('search', this.filters.search)
      if (this.filters.search && this.filters.direction)
        params.append('direction', this.filters.direction)
      if (this.filters.sentSince)
        params.append('sentSince', toDateParam(this.filters.sentSince))
      if (this.filters.sentUntil)
        params.append('sentUntil', toDateParam(this.filters.sentUntil))
      params.append('includeOnHold', this.includeOnHold.toString())
      params.append('unmatchedOnly', this.unmatchedOnly.toString())
      params.append('limit', limit.toString())
      params.append('offset', offset.toString())

      const { data, error } = await apiFetch<AidActivityPage>(
        `/api/deals/admin/aid-activity?${params.toString()}`,
      )

      if (error.value) {
        console.error('Failed to load aid activity:', error.value)
        throw error.value
      }

      if (data.value) {
        this.currentPage = data.value as AidActivityPage
      }
    },
  },
})

// yyyy-mm-dd in local time, so the date picked is the date searched
function toDateParam(date: Date): string {
  const month = (date.getMonth() + 1).toString().padStart(2, '0')
  const day = date.getDate().toString().padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}
