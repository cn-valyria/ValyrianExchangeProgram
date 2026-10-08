import { defineStore } from 'pinia'

export interface DealAidCountByDay {
  day: string
  aidCount: number
}

export interface TotalSlotsGroup {
  allianceName: string
  totalSlots: number
}

export interface TotalSlotGroupsByAlliance {
  slotGroupName: string,
  totalSlotGroups: TotalSlotsGroup[]
}

export interface AccountCountByRole {
  role: string
  totalAccounts: number
}

export interface OpenSlotCountByRole {
  role: string
  openSlots: number
}

export interface AccountCountByAlliance {
  allianceName: string
  totalAccounts: number
}

export interface GeneralProgramSummary {
  totalAccounts: number
  activeDeals: number
}

// Number of slots that send or receive each kind of aid
export interface AidRatioSummary {
  totalIncomingCashSlots: number
  totalOutgoingCashSlots: number
  totalIncomingTechSlots: number
  totalOutgoingTechSlots: number
}

export interface DealHealthSummary {
  accountsWaiting: number
  slotsWaiting: number
  openFlags: number
}

export interface SlotUsageSummary {
  maxCapacity: number
  currentSlotsUsed: number
}

export interface Dashboard {
  generalProgramSummary: GeneralProgramSummary
  aidRatioSummary: AidRatioSummary
  dealHealthSummary: DealHealthSummary
  slotUsageSummary: SlotUsageSummary
  dealAidPerDay: DealAidCountByDay[]
  accountsPerRole: AccountCountByRole[]
  totalSlotsPerAlliance: TotalSlotGroupsByAlliance[]
  openSlotsPerRole: OpenSlotCountByRole[]
  accountsPerAlliance: AccountCountByAlliance[]
}

export const useAdminStore = defineStore('admin', {
  state: () => ({
    dashboard: {} as Dashboard,
  }),

  actions: {
    async loadDashboard() {
      const { apiFetch } = useApiClient()

      // Call all dashboard endpoints in parallel
      const [
        generalProgramSummaryResponse,
        aidRatioSummaryResponse,
        dealHealthSummaryResponse,
        slotUsageSummaryResponse,
        dealAidPerDayResponse,
        accountsPerRoleResponse,
        totalSlotsResponse,
        openSlotsResponse,
        accountsPerAllianceResponse,
      ] = await Promise.all([
        apiFetch<GeneralProgramSummary>('/api/admin/dashboard/general-program-summary'),
        apiFetch<AidRatioSummary>('/api/admin/dashboard/aid-ratio-summary'),
        apiFetch<DealHealthSummary>('/api/admin/dashboard/deal-health-summary'),
        apiFetch<SlotUsageSummary>('/api/admin/dashboard/slot-usage-summary'),
        apiFetch<DealAidCountByDay[]>('/api/admin/dashboard/deal-aid-per-day'),
        apiFetch<AccountCountByRole[]>('/api/admin/dashboard/accounts-per-role'),
        apiFetch<TotalSlotGroupsByAlliance[]>('/api/admin/dashboard/total-slots-per-alliance'),
        apiFetch<OpenSlotCountByRole[]>('/api/admin/dashboard/open-slots-per-role'),
        apiFetch<AccountCountByAlliance[]>('/api/admin/dashboard/accounts-per-alliance'),
      ])

      // Check for errors
      if (generalProgramSummaryResponse.error.value) {
        console.error('Failed to load dashboard summary:', generalProgramSummaryResponse.error.value)
        throw generalProgramSummaryResponse.error.value
      }
      if (aidRatioSummaryResponse.error.value) {
        console.error('Failed to load dashboard summary:', aidRatioSummaryResponse.error.value)
        throw aidRatioSummaryResponse.error.value
      }
      if (dealHealthSummaryResponse.error.value) {
        console.error('Failed to load deal health summary:', dealHealthSummaryResponse.error.value)
        throw dealHealthSummaryResponse.error.value
      }
      if (slotUsageSummaryResponse.error.value) {
        console.error('Failed to load dashboard summary:', slotUsageSummaryResponse.error.value)
        throw slotUsageSummaryResponse.error.value
      }
      if (dealAidPerDayResponse.error.value) {
        console.error('Failed to load deal aid per day:', dealAidPerDayResponse.error.value)
        throw dealAidPerDayResponse.error.value
      }
      if (accountsPerRoleResponse.error.value) {
        console.error('Failed to load accounts per role:', accountsPerRoleResponse.error.value)
        throw accountsPerRoleResponse.error.value
      }
      if (totalSlotsResponse.error.value) {
        console.error('Failed to load total slots:', totalSlotsResponse.error.value)
        throw totalSlotsResponse.error.value
      }
      if (openSlotsResponse.error.value) {
        console.error('Failed to load open slots:', openSlotsResponse.error.value)
        throw openSlotsResponse.error.value
      }
      if (accountsPerAllianceResponse.error.value) {
        console.error('Failed to load accounts per alliance:', accountsPerAllianceResponse.error.value)
        throw accountsPerAllianceResponse.error.value
      }

      // Combine all responses into the dashboard object
      this.dashboard = {
        generalProgramSummary: generalProgramSummaryResponse.data.value!,
        aidRatioSummary: aidRatioSummaryResponse.data.value!,
        dealHealthSummary: dealHealthSummaryResponse.data.value!,
        slotUsageSummary: slotUsageSummaryResponse.data.value!,
        dealAidPerDay: dealAidPerDayResponse.data.value!,
        accountsPerRole: accountsPerRoleResponse.data.value!,
        totalSlotsPerAlliance: totalSlotsResponse.data.value!,
        openSlotsPerRole: openSlotsResponse.data.value!,
        accountsPerAlliance: accountsPerAllianceResponse.data.value!,
      }
    },
  },
})
