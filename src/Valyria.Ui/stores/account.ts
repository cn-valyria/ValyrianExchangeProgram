import { defineStore } from 'pinia'
import type { NationalWarStatus, RecentActivity } from './shared/enums'
import type { NationSimplified } from './shared/nationSimplified'

export interface Account {
  id: number
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
  discord: string
  discordUniqueId: number
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
}

export interface AccountSimplified extends NationSimplified {
  accountId: number
}

export interface SlotDetail {
  id: number
  slotNumber: number
  techSentCycles: number
  techReceivedCycles: number
  cashSentCycles: number
  cashReceivedCycles: number
  account: AccountSimplified
  hasActiveDeal: boolean
}

// Mirrors Valyria.Core.Entities.DealActivityState, which is serialized as a number
export enum DealActivityState {
  PendingStart = 0,
  InFlight = 1,
  AwaitingSend = 2,
  Blocked = 3,
}

// Mirrors Valyria.Core.Entities.DealBlockedReason, which is serialized as a number
export enum DealBlockedReason {
  SenderSlotsFull = 0,
  ReceiverSlotsFull = 1,
  OutOfSequenceAid = 2,
}

// Mirrors Valyria.Core.Entities.DealLegOutcome, which is serialized as a number
export enum DealLegOutcome {
  Pending = 0,
  Accepted = 1,
  Cancelled = 2,
  Expired = 3,
}

export interface DealDetail {
  id: number
  createdOn: string
  mySlot: SlotDetail
  partnerSlot: SlotDetail
  partnerDiscord: string | null
  lastActivityDate: string | null
  // Null until the data loader's next run picks up a newly created deal
  state: DealActivityState | null
  blockedReason: DealBlockedReason | null
  stateSince: string | null
  // Whether this account sends the next aid (after any aid in flight expires)
  isMyTurnToSend: boolean
  iAmResponsible: boolean
  partnerIsResponsible: boolean
  daysResponsible: number | null
  cancelsOn: string | null
  inFlightUntil: string | null
  inFlightIsIncoming: boolean
  inFlightAccepted: boolean
  blockedUntil: string | null
  // Set while this account has an open warning for holding up the deal
  flaggedOn: string | null
}

export interface DealNotification {
  type: 'warning' | 'error' | 'info'
  message: string
  dealId: number | null
}

export interface AccountDealsSummary {
  slotsUsed: number
  slotsTotal: number
  slotsAvailable: number
  slotsFull: boolean
  activeDeals: DealDetail[]
  notifications: DealNotification[]
}

// Mirrors Valyria.Core.Entities.DealStatus, which is serialized as a number
export enum DealStatus {
  Active = 0,
  Cancelled = 1,
}

export interface DealHistoryItem {
  id: number
  status: DealStatus
  createdOn: string
  cancelledOn: string | null
  cancellationReason: string | null
  mySlotNumber: number
  aidSentCount: number
  aidReceivedCount: number
  lastActivityDate: string | null
  partner: AccountSimplified
}

export interface DealHistoryPage {
  totalCount: number
  results: DealHistoryItem[]
}

export interface DealActivity {
  id: number
  aidId: number | null
  activityDate: string
  isOutgoing: boolean
  money: number
  technology: number
  outcome: DealLegOutcome
}

export const useAccountStore = defineStore('account', {
  state: () => ({
    account: {} as Account,
    dealsSummary: {
      slotsUsed: 0,
      slotsTotal: 0,
      slotsAvailable: 0,
      slotsFull: false,
      activeDeals: [],
      notifications: [],
    } as AccountDealsSummary,
    dealHistory: { totalCount: 0, results: [] } as DealHistoryPage,
    dealActivity: {} as Record<number, DealActivity[]>,
  }),

  actions: {
    async loadAccount(accountId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<Account>(`/api/accounts/${accountId}`)

      if (error.value) {
        console.error('Failed to load account:', error.value)
        throw error.value
      }

      if (data.value) {
        this.account = data.value as Account
      }
    },

    async loadDealsSummary(accountId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AccountDealsSummary>(`/api/deals/account/${accountId}`)

      if (error.value) {
        console.error('Failed to load deals:', error.value)
        throw error.value
      }

      if (data.value) {
        this.dealsSummary = data.value as AccountDealsSummary
      }
    },

    async loadDealHistory(accountId: number, limit?: number, offset?: number) {
      const { apiFetch } = useApiClient()

      const params = new URLSearchParams()
      if (limit !== undefined) params.append('limit', limit.toString())
      if (offset !== undefined) params.append('offset', offset.toString())

      const url = `/api/deals/account/${accountId}/history${
        params.toString().length > 0 ? '?' + params.toString() : ''
      }`

      const { data, error } = await apiFetch<DealHistoryPage>(url)

      if (error.value) {
        console.error('Failed to load deal history:', error.value)
        throw error.value
      }

      if (data.value) {
        this.dealHistory = data.value as DealHistoryPage
      }
    },

    async loadDealActivity(accountId: number, dealId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<DealActivity[]>(`/api/deals/${dealId}/activity?accountId=${accountId}`)

      if (error.value) {
        console.error('Failed to load deal activity:', error.value)
        throw error.value
      }

      if (data.value) {
        this.dealActivity[dealId] = data.value as DealActivity[]
      }
    },
  },
})
