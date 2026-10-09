import { defineStore } from 'pinia'
import type { AccountSimplified } from './account'

// Mirrors Valyria.Core.Entities.MassMessageCode, which is serialized as a number
export enum MassMessageCode {
  SendAid = 0,
  AcceptAid = 1,
  Overdue = 2,
  FinalWarning = 3,
}

// An account that needs to act on its deals today. The counts are only shown to admins;
// the message itself never includes deal-specific information
export interface MassMessageRecipient {
  accountId: number
  nationId: number
  rulerName: string
  nationName: string
  allianceName: string | null
  discord: string | null
  sendCount: number
  acceptCount: number
  // Deals blocked by this account's full slots or out-of-turn aid
  blockedCount: number
  flaggedCount: number
  // Longest any flagged deal has been held up, or null if none are flagged
  daysOverdue: number | null
  cancelsOn: string | null
}

export interface MassMessage {
  id: number
  code: MassMessageCode
  name: string
  description: string
  // {{date}} and {{siteUrl}} are filled in by fillTemplate
  subject: string
  message: string
  // Null until an admin marks today's messages as sent
  sentOn: string | null
  sentBy: AccountSimplified | null
  recipients: MassMessageRecipient[]
}

export interface MassMessagesSummary {
  cnDate: string
  dealStatesUpdatedOn: string | null
  messages: MassMessage[]
}

export const useAdminMassMessagesStore = defineStore('adminMassMessages', {
  state: () => ({
    summary: null as MassMessagesSummary | null,
  }),

  actions: {
    async loadMassMessages() {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<MassMessagesSummary>('/api/mass-messages')

      if (error.value) {
        console.error('Failed to load mass messages:', error.value)
        throw error.value
      }

      if (data.value) {
        this.summary = data.value as MassMessagesSummary
      }
    },

    // Reloads afterwards so the page shows who actually gets the credit if another admin got there first
    async setMessageSent(messageId: number, sent: boolean) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch<void>(`/api/mass-messages/${messageId}/sent`, {
        method: 'PUT',
        body: { sent },
      })

      if (error.value) {
        console.error('Failed to update messages sent:', error.value)
        throw error.value
      }

      await this.loadMassMessages()
    },
  },
})
