import { defineStore } from 'pinia'

// Program-wide counts. On-hold accounts are left out
export interface ProgramSummary {
  totalAccounts: number
  activeDeals: number
  openFlags: number
  // Accounts with an unresolved flag, which keeps them out of matching
  flaggedAccounts: number
}

export interface SlotCycles {
  techSentCycles: number
  techReceivedCycles: number
  cashSentCycles: number
  cashReceivedCycles: number
}

// Every slot with the same cycle values. Matching pairs a slot only with slots whose cycles are the exact inverse
export interface SlotConfigSummary extends SlotCycles {
  accounts: number
  totalSlots: number
  slotsInDeals: number
  // Not in a deal, on accounts matching can use
  openSlots: number
  // Accounts with at least one of openSlots. Two accounts can share only one deal
  openAccounts: number
  // Not in a deal, on accounts kept out of matching by an unresolved flag
  flaggedOpenSlots: number
}

// Mirrors Valyria.Core.DTOs.DealHealth, which is serialized as a number
export enum DealHealth {
  OnTrack = 0,
  Starting = 1,
  Stalled = 2,
  AtRisk = 3,
}

export interface DealHealthByPairing {
  slot1TechSentCycles: number
  slot1TechReceivedCycles: number
  slot1CashSentCycles: number
  slot1CashReceivedCycles: number
  slot2TechSentCycles: number
  slot2TechReceivedCycles: number
  slot2CashSentCycles: number
  slot2CashReceivedCycles: number
  health: DealHealth
  deals: number
}

// SlotConfigSummary for one alliance's accounts
export interface AllianceSlotConfigSummary extends SlotConfigSummary {
  allianceName: string
  // All of the alliance's accounts, whatever their slots. Accounts can have slots in several configs
  allianceAccounts: number
}

// DealHealthByPairing for deals involving one alliance. A deal between two alliances counts for both
export interface AllianceDealHealth extends DealHealthByPairing {
  allianceName: string
  // Both accounts in these deals are in this alliance
  withinAlliance: boolean
}

export interface DailyDealActivity {
  day: string
  legsSent: number
  legsFailed: number
  dealsCreated: number
  dealsCancelled: number
}

export interface CancellationReasonCount {
  reason: string
  deals: number
}

export interface Dashboard {
  summary: ProgramSummary
  slotConfigs: SlotConfigSummary[]
  dealHealth: DealHealthByPairing[]
  allianceSlotConfigs: AllianceSlotConfigSummary[]
  allianceDealHealth: AllianceDealHealth[]
  activity: DailyDealActivity[]
  cancellationReasons: CancellationReasonCount[]
}

export const ACTIVITY_DAYS = 14
export const CANCELLATION_DAYS = 30

export const useAdminStore = defineStore('admin', {
  state: () => ({
    dashboard: {} as Dashboard,
  }),

  actions: {
    async loadDashboard() {
      const { apiFetch } = useApiClient()

      // Call all dashboard endpoints in parallel
      const [
        summaryResponse,
        slotConfigsResponse,
        dealHealthResponse,
        allianceSlotConfigsResponse,
        allianceDealHealthResponse,
        activityResponse,
        cancellationReasonsResponse,
      ] = await Promise.all([
        apiFetch<ProgramSummary>('/api/admin/dashboard/summary'),
        apiFetch<SlotConfigSummary[]>('/api/admin/dashboard/slot-configs'),
        apiFetch<DealHealthByPairing[]>('/api/admin/dashboard/deal-health'),
        apiFetch<AllianceSlotConfigSummary[]>(
          '/api/admin/dashboard/alliance-slot-configs',
        ),
        apiFetch<AllianceDealHealth[]>(
          '/api/admin/dashboard/alliance-deal-health',
        ),
        apiFetch<DailyDealActivity[]>(
          `/api/admin/dashboard/activity?days=${ACTIVITY_DAYS}`,
        ),
        apiFetch<CancellationReasonCount[]>(
          `/api/admin/dashboard/cancellation-reasons?days=${CANCELLATION_DAYS}`,
        ),
      ])

      const responses = {
        summary: summaryResponse,
        'slot configs': slotConfigsResponse,
        'deal health': dealHealthResponse,
        'alliance slot configs': allianceSlotConfigsResponse,
        'alliance deal health': allianceDealHealthResponse,
        activity: activityResponse,
        'cancellation reasons': cancellationReasonsResponse,
      }
      for (const [name, response] of Object.entries(responses)) {
        if (response.error.value) {
          console.error(
            `Failed to load dashboard ${name}:`,
            response.error.value,
          )
          throw response.error.value
        }
      }

      this.dashboard = {
        summary: summaryResponse.data.value!,
        slotConfigs: slotConfigsResponse.data.value!,
        dealHealth: dealHealthResponse.data.value!,
        allianceSlotConfigs: allianceSlotConfigsResponse.data.value!,
        allianceDealHealth: allianceDealHealthResponse.data.value!,
        activity: activityResponse.data.value!,
        cancellationReasons: cancellationReasonsResponse.data.value!,
      }
    },
  },
})
