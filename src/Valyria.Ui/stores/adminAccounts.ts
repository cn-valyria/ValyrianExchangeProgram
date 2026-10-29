import { defineStore } from 'pinia'
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'
import type { SlotDetail } from './account'
import type { NationalWarStatus, RecentActivity } from './shared/enums'

export interface AdminAccount {
  id: number
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
  discord: string | null
  discordUniqueId: bigint | null
  uniqueCode: string
  role: string
  activity: RecentActivity
  strength: number
  infra: number
  tech: number
  warStatus: NationalWarStatus
  nationDeleted: boolean
  hasForeignMinistry: boolean
  hasFederalAidCommission: boolean
  hasDisasterReliefAgency: boolean
  slotsUsed: number
  slotsTotal: number
  openFlagCount: number
}

export const useAdminAccountsStore = defineStore('adminAccounts', {
  state: () => ({
    accounts: [] as AdminAccount[],
  }),

  actions: {
    async loadAllAccounts(includeOnHold: boolean = false) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AdminAccount[]>(
        `/api/accounts?includeOnHold=${includeOnHold}`,
      )

      if (error.value) {
        console.error('Failed to load accounts:', error.value)
        throw error.value
      }

      if (data.value) {
        this.accounts = data.value as AdminAccount[]
      }
    },

    async createAccount(payload: Partial<AdminAccount>) {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<AdminAccount>('/api/accounts', {
        method: 'POST',
        body: payload,
      })

      if (error.value) {
        console.error('Failed to create account:', error.value)
        throw error.value
      }

      if (data.value) {
        this.accounts.push(data.value as AdminAccount)
      }
    },

    async updateAccount(payload: Partial<AdminAccount> & { id: number }) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<null>('/api/accounts', {
        method: 'PUT',
        body: payload,
      })

      if (error.value) {
        console.error('Failed to update account:', error.value)
        throw error.value
      }

      const index = this.accounts.findIndex((a) => a.id === payload.id)
      if (index !== -1) {
        const updatedAccount = {
          ...this.accounts[index],
          role: payload.role,
          uniqueCode: payload.uniqueCode,
          discord: payload.discord,
          discordUniqueId: payload.discordUniqueId,
          hasForeignMinistry: payload.hasForeignMinistry,
          hasFederalAidCommission: payload.hasFederalAidCommission,
          hasDisasterReliefAgency: payload.hasDisasterReliefAgency,
        }
        this.accounts[index] = updatedAccount as AdminAccount
      }
    },

    // Also cancels the account's active deals
    async holdAccount(id: number) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<null>(`/api/accounts/${id}/hold`, {
        method: 'PUT',
      })

      if (error.value) {
        console.error('Failed to put account on hold:', error.value)
        throw error.value
      }

      const account = this.accounts.find((a) => a.id === id)
      if (account) {
        account.role = ACCOUNT_ROLE_CODES.onHold
      }
    },

    async loadAccountSlots(accountId: number): Promise<SlotDetail[]> {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<{ slots: SlotDetail[] }>(
        `/api/slots/account/${accountId}`,
      )

      if (error.value) {
        console.error('Failed to load account slots:', error.value)
        throw error.value
      }

      return data.value?.slots ?? []
    },

    async updateSlot(slot: SlotDetail) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<null>(`/api/slots/${slot.id}`, {
        method: 'PUT',
        body: {
          techSentCycles: slot.techSentCycles,
          techReceivedCycles: slot.techReceivedCycles,
          cashSentCycles: slot.cashSentCycles,
          cashReceivedCycles: slot.cashReceivedCycles,
        },
      })

      if (error.value) {
        console.error('Failed to update slot:', error.value)
        throw error.value
      }
    },

    async deleteAccount(id: number) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<null>(`/api/accounts/${id}`, {
        method: 'DELETE',
      })

      if (error.value) {
        console.error('Failed to delete account:', error.value)
        throw error.value
      }

      const index = this.accounts.findIndex((a) => a.id === id)
      if (index !== -1) {
        this.accounts.splice(index, 1)
      }
    },
  },
})
