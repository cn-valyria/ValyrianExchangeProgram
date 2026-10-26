import { defineStore } from 'pinia'
import type {
  AccountSimplified,
  DealActivityState,
  DealBlockedReason,
  DealExtensionKind,
  DealLegOutcome,
  DealStatus,
  SlotDetail,
} from './account'
import type { NationSimplified } from './shared/nationSimplified'

export interface AdminDeal {
  id: number
  createdOn: string
  createdBy: string | null
  lastActivityDate: string | null
  // Null until the data loader's next run picks up a newly created deal
  state: DealActivityState | null
  blockedReason: DealBlockedReason | null
  stateSince: string | null
  nextSenderAccountId: number | null
  responsibleAccountId: number | null
  daysResponsible: number | null
  // Extensions included. Null when nobody is holding up the deal, or an extension removed the deadline
  cancelsOn: string | null
  // Extra time an admin gave the account holding up the deal, while it still applies
  extensionKind: DealExtensionKind | null
  extendedBy: string | null
  inFlightUntil: string | null
  hasOpenFlag: boolean
  slot1: SlotDetail
  slot2: SlotDetail
  // Set when either side's slot is a team bonus slot: the deal is cancelled after this CN date
  teamBonusEndsOn: string | null
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

// Mirrors Valyria.Core.Entities.DealFlagReason, which is serialized as a number
export enum DealFlagReason {
  AwaitingSend = 0,
  SenderSlotsFull = 1,
  ReceiverSlotsFull = 2,
  OutOfSequenceAid = 3,
  AidCancelled = 4,
  AidExpired = 5,
}

export interface DealFlag {
  id: number
  dealId: number
  flagType: DealFlagType
  // Null for flags raised before VEP 3.0's activity rules
  flagReason: DealFlagReason | null
  // Names are null if the account's nation has been deleted
  flaggedAccount: AccountSimplified
  innocentAccount: AccountSimplified
  flaggedOn: string
  resolvedOn: string | null
  resolvedBy: string | null
  adminNotes: string | null
  flaggedAccountRole: string
  innocentAccountRole: string
  // Open flags against the flagged account, this one included. It isn't matched until they're all resolved
  flaggedAccountOpenFlagCount: number
  flaggedSlot: SlotDetail
  innocentSlot: SlotDetail
  dealStatus: DealStatus
  dealCreatedOn: string
  dealCancelledOn: string | null
  dealCancelledBy: string | null
  cancellationReason: string | null
  // The deal's current state. Null once it's cancelled, or until the data loader picks up a restored deal
  state: DealActivityState | null
  blockedReason: DealBlockedReason | null
  stateSince: string | null
  nextSenderAccountId: number | null
  responsibleAccountId: number | null
  daysResponsible: number | null
  // Extensions included
  cancelsOn: string | null
  // Extra time an admin already gave this account on the deal, while it still applies
  extensionKind: DealExtensionKind | null
  extendedBy: string | null
  inFlightUntil: string | null
  blockedUntil: string | null
  // Legs that haven't failed, including any in flight
  legsCompleted: number
  flaggedLastSentOn: string | null
  // What stops the deal being restored
  flaggedSlotOtherDealId: number | null
  innocentSlotOtherDealId: number | null
  pairOtherDealId: number | null
  flaggedAccountOnHold: boolean
  innocentAccountOnHold: boolean
  slotsStillMatch: boolean
  canRestoreDeal: boolean
}

export interface FlagDealLeg {
  legNumber: number
  aidId: number | null
  activityDate: string
  sentByFlaggedAccount: boolean
  money: number
  technology: number
  outcome: DealLegOutcome
  // The leg failed, but an admin restored the deal it cancelled
  failureExcused: boolean
}

export interface FlagSlotAid {
  aidId: number
  sentOn: string
  // True if the flagged nation sent it
  isOutgoing: boolean
  status: number
  money: number
  technology: number
  soldiers: number
  reason: string | null
  // The deal this aid is a leg of, if any
  dealId: number | null
  // Sent before the flag's deal was created, so it isn't held against the nation
  sentBeforeDeal: boolean
  // Names are null if the nation has since been deleted
  otherNation: NationSimplified
}

export interface DealFlagEvidence {
  // Oldest first
  legs: FlagDealLeg[]
  // CN date: today while the deal is active, otherwise the day the flag was raised
  slotsAsOf: string
  flaggedSlotsTotal: number
  // Aid that held one of the flagged nation's slots as of slotsAsOf, oldest first
  flaggedSlotAid: FlagSlotAid[]
}

export const useAdminDealsStore = defineStore('adminDeals', {
  state: () => ({
    activeDeals: { totalCount: 0, results: [] } as AdminDealPage,
    waitQueue: [] as WaitQueueEntry[],
    flags: [] as DealFlag[],
    flagEvidence: {} as Record<number, DealFlagEvidence>,
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

      const { data, error } = await apiFetch<WaitQueueEntry[]>(
        '/api/deals/admin/wait-queue',
      )

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

      const { data, error } = await apiFetch<DealFlag[]>(
        '/api/deals/admin/flagged',
      )

      if (error.value) {
        console.error('Failed to load flagged deals:', error.value)
        throw error.value
      }

      if (data.value) {
        this.flags = data.value as DealFlag[]
        // Evidence for an active deal goes stale between loads
        this.flagEvidence = {}
      }
    },

    async loadFlagEvidence(flagId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<DealFlagEvidence>(
        `/api/deals/admin/flags/${flagId}/evidence`,
      )

      if (error.value) {
        console.error('Failed to load flag evidence:', error.value)
        throw error.value
      }

      if (data.value) {
        this.flagEvidence[flagId] = data.value as DealFlagEvidence
      }
    },

    async cancelDeal(dealId: number, reason: string) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<null>(`/api/deals/${dealId}/cancel`, {
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

      const { error } = await apiFetch<null>(
        `/api/deals/admin/flags/${flagId}/resolve`,
        {
          method: 'PUT',
          body: { adminNotes },
        },
      )

      if (error.value) {
        console.error('Failed to resolve flag:', error.value)
        throw error.value
      }

      this.flags = this.flags.filter((flag) => flag.id !== flagId)
    },

    // Extending resolves the warning, so it leaves the list
    async extendFlaggedDeal(
      flagId: number,
      kind: DealExtensionKind,
      adminNotes: string,
    ) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<null>(
        `/api/deals/admin/flags/${flagId}/extend`,
        {
          method: 'PUT',
          body: { kind, adminNotes },
        },
      )

      if (error.value) {
        console.error('Failed to extend deal:', error.value)
        throw error.value
      }

      this.flags = this.flags.filter((flag) => flag.id !== flagId)
    },

    // Restoring resolves every open flag on the deal, so they all leave the list
    async restoreFlaggedDeal(flagId: number, adminNotes: string) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<null>(
        `/api/deals/admin/flags/${flagId}/restore`,
        {
          method: 'PUT',
          body: { adminNotes },
        },
      )

      if (error.value) {
        console.error('Failed to restore deal:', error.value)
        throw error.value
      }

      const dealId = this.flags.find((flag) => flag.id === flagId)?.dealId
      this.flags = this.flags.filter((flag) => flag.dealId !== dealId)
    },

    async loadAvailableSlots(accountId: number): Promise<SlotDetail[]> {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<SlotDetail[]>(
        `/api/slots/available/${accountId}`,
      )

      if (error.value) {
        console.error('Failed to load available slots:', error.value)
        throw error.value
      }

      return (data.value as SlotDetail[]) ?? []
    },
  },
})
