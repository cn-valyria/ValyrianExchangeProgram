import { defineStore } from 'pinia'

export interface TransactionCountByDay {
  dayTxnRecorded: string
  txnCount: number
}

export interface CreditGroup {
  dateRange: string
  totalCredit: number
}

export interface CreditGroupsByAlliance {
  allianceName: string
  creditGroups: CreditGroup[]
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

export interface FreeSlotCountByList {
  listName: string
  freeSlots: number
}

export interface AccountCountByAlliance {
  allianceName: string
  totalAccounts: number
}

export interface GeneralProgramSummary {
  totalAccounts: number
  totalTransactions: number
}

export interface AidRatioSummary {
  totalIncomingCashSlots: number
  totalOutgoingCashSlots: number
  totalIncomingTechSlots: number
  totalOutgoingTechSlots: number
}

export interface UnusedCreditSummary {
  unusedCredit: number
}

export interface SlotUsageSummary {
  maxCapacity: number 
  currentSlotsUsed: number
}

export interface Dashboard {
  generalProgramSummary: GeneralProgramSummary
  aidRatioSummary: AidRatioSummary
  unusedCreditSummary: UnusedCreditSummary
  slotUsageSummary: SlotUsageSummary
  transactionsPerDay: TransactionCountByDay[]
  pendingCreditByAlliance: CreditGroupsByAlliance[]
  accountsPerRole: AccountCountByRole[]
  totalSlotsPerAlliance: TotalSlotGroupsByAlliance[]
  freeSlotsPerList: FreeSlotCountByList[]
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
        unusedCreditSummaryResponse,
        slotUsageSummaryResponse,
        transactionsPerDayResponse,
        pendingCreditResponse,
        accountsPerRoleResponse,
        totalSlotsResponse,
        freeSlotsResponse,
        accountsPerAllianceResponse,
      ] = await Promise.all([
        apiFetch<GeneralProgramSummary>('/api/admin/dashboard/general-program-summary'),
        apiFetch<AidRatioSummary>('/api/admin/dashboard/aid-ratio-summary'),
        apiFetch<UnusedCreditSummary>('/api/admin/dashboard/unused-credit-summary'),
        apiFetch<SlotUsageSummary>('/api/admin/dashboard/slot-usage-summary'),
        apiFetch<TransactionCountByDay[]>('/api/admin/dashboard/transactions-per-day'),
        apiFetch<CreditGroupsByAlliance[]>('/api/admin/dashboard/pending-credit-per-alliance'),
        apiFetch<AccountCountByRole[]>('/api/admin/dashboard/accounts-per-role'),
        apiFetch<TotalSlotGroupsByAlliance[]>('/api/admin/dashboard/total-slots-per-alliance'),
        apiFetch<FreeSlotCountByList[]>('/api/admin/dashboard/free-slots-per-list'),
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
      if (unusedCreditSummaryResponse.error.value) {
        console.error('Failed to load dashboard summary:', unusedCreditSummaryResponse.error.value)
        throw unusedCreditSummaryResponse.error.value
      }
      if (slotUsageSummaryResponse.error.value) {
        console.error('Failed to load dashboard summary:', slotUsageSummaryResponse.error.value)
        throw slotUsageSummaryResponse.error.value
      }
      if (transactionsPerDayResponse.error.value) {
        console.error('Failed to load transactions per day:', transactionsPerDayResponse.error.value)
        throw transactionsPerDayResponse.error.value
      }
      if (pendingCreditResponse.error.value) {
        console.error('Failed to load pending credit:', pendingCreditResponse.error.value)
        throw pendingCreditResponse.error.value
      }
      if (accountsPerRoleResponse.error.value) {
        console.error('Failed to load accounts per role:', accountsPerRoleResponse.error.value)
        throw accountsPerRoleResponse.error.value
      }
      if (totalSlotsResponse.error.value) {
        console.error('Failed to load total slots:', totalSlotsResponse.error.value)
        throw totalSlotsResponse.error.value
      }
      if (freeSlotsResponse.error.value) {
        console.error('Failed to load free slots:', freeSlotsResponse.error.value)
        throw freeSlotsResponse.error.value
      }
      if (accountsPerAllianceResponse.error.value) {
        console.error('Failed to load accounts per alliance:', accountsPerAllianceResponse.error.value)
        throw accountsPerAllianceResponse.error.value
      }

      // Combine all responses into the dashboard object
      this.dashboard = {
        generalProgramSummary: generalProgramSummaryResponse.data.value!,
        aidRatioSummary: aidRatioSummaryResponse.data.value!,
        unusedCreditSummary: unusedCreditSummaryResponse.data.value!,
        slotUsageSummary: slotUsageSummaryResponse.data.value!,
        transactionsPerDay: transactionsPerDayResponse.data.value!,
        pendingCreditByAlliance: pendingCreditResponse.data.value!,
        accountsPerRole: accountsPerRoleResponse.data.value!,
        totalSlotsPerAlliance: totalSlotsResponse.data.value!,
        freeSlotsPerList: freeSlotsResponse.data.value!,
        accountsPerAlliance: accountsPerAllianceResponse.data.value!,
      }
    },
  },
})
