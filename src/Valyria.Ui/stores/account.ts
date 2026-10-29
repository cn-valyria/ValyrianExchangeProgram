import { defineStore } from 'pinia'
import type { NationalWarStatus, RecentActivity } from './shared/enums'
import type { NationSimplified } from './shared/nationSimplified'
import type { DealFlag, DealFlagEvidence } from './adminDeals'
import type { Team } from '~/infrastructure/teamEvents'

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
  // The extra slot an account has while its team's +1 aid slot event is in effect (always slot 7)
  isTeamBonus: boolean
  // Last CN date the bonus slot is in effect. Null if it isn't a bonus slot, or the bonus has ended
  teamBonusEndsOn: string | null
  // Last CN date VEP matches a new deal into the bonus slot
  teamBonusMatchableUntil: string | null
  isInactiveTeamBonus: boolean
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

// Mirrors Valyria.Core.Entities.DealExtensionKind, which is serialized as a number
export enum DealExtensionKind {
  FiveDays = 0,
  UntilNextLeg = 1,
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
  // Set when either side's slot is a team bonus slot: the deal is cancelled after this CN date
  teamBonusEndsOn: string | null
}

export interface DealNotification {
  type: 'warning' | 'error' | 'info'
  message: string
  dealId: number | null
}

export interface AccountTeamEvent {
  team: Team
  approvedOn: string
  endsOn: string
  // Before endsOn when the bonus slot's deals need more than one aid offer, so every offer fits in the event
  bonusSlotMatchableUntil: string
}

export interface AccountDealsSummary {
  slotsUsed: number
  slotsTotal: number
  slotsAvailable: number
  slotsFull: boolean
  // The team event giving the account a bonus slot, if one is in effect
  teamEvent: AccountTeamEvent | null
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

// The account's open warning on one of its deals, as the account sees it (no admin notes)
export interface AccountDealFlag {
  flag: DealFlag
  evidence: DealFlagEvidence
}

export const useAccountStore = defineStore('account', {
  state: () => ({
    account: {} as Account,
    dealsSummary: {
      slotsUsed: 0,
      slotsTotal: 0,
      slotsAvailable: 0,
      slotsFull: false,
      teamEvent: null,
      activeDeals: [],
      notifications: [],
    } as AccountDealsSummary,
    dealHistory: { totalCount: 0, results: [] } as DealHistoryPage,
    dealActivity: {} as Record<number, DealActivity[]>,
    dealFlags: {} as Record<number, AccountDealFlag>,
  }),

  actions: {
    async loadAccount(accountId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<Account>(
        `/api/accounts/${accountId}`,
      )

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

      const { data, error } = await apiFetch<AccountDealsSummary>(
        `/api/deals/account/${accountId}`,
      )

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

      const { data, error } = await apiFetch<DealActivity[]>(
        `/api/deals/${dealId}/activity?accountId=${accountId}`,
      )

      if (error.value) {
        console.error('Failed to load deal activity:', error.value)
        throw error.value
      }

      if (data.value) {
        this.dealActivity[dealId] = data.value as DealActivity[]
      }
    },

    async loadDealFlag(accountId: number, dealId: number) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AccountDealFlag>(
        `/api/deals/${dealId}/flag?accountId=${accountId}`,
      )

      if (error.value) {
        console.error('Failed to load deal flag:', error.value)
        throw error.value
      }

      if (data.value) {
        this.dealFlags[dealId] = data.value as AccountDealFlag
      }
    },
  },
})
