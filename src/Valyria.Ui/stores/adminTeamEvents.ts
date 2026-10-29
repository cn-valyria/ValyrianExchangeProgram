import { defineStore } from 'pinia'
import type { Team } from '~/infrastructure/teamEvents'

// A team's +1 aid slot event. Every account on the team has a bonus slot from approvedOn through endsOn
export interface TeamEvent {
  id: number
  team: Team
  approvedOn: string
  // Last CN date the event is in effect, 30 days after approvedOn
  endsOn: string
  isActive: boolean
  // Accounts on the team right now with the bonus slot. 0 once the event has ended
  accountsWithBonusSlot: number
  activeBonusDeals: number
  createdOn: string
  createdBy: string
}

export interface TeamEventCreateRequest {
  team: Team
  // CN date, "YYYY-MM-DD"
  approvedOn: string
}

export const useAdminTeamEventsStore = defineStore('adminTeamEvents', {
  state: () => ({
    teamEvents: [] as TeamEvent[],
    loading: false,
  }),

  actions: {
    async loadTeamEvents() {
      const { apiFetch } = useApiClient()
      this.loading = true

      try {
        const { data, error } = await apiFetch<TeamEvent[]>(
          '/api/admin/settings/team-events',
        )

        if (error.value) {
          console.error('Failed to load team events:', error.value)
          throw error.value
        }

        this.teamEvents = data.value ?? []
      } finally {
        this.loading = false
      }
    },

    // Throws the ApiError on failure, so callers can explain a 409 (overlapping event)
    async createTeamEvent(request: TeamEventCreateRequest) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch('/api/admin/settings/team-events', {
        method: 'POST',
        body: request,
      })

      if (error.value) {
        console.error('Failed to create team event:', error.value)
        throw error.value
      }

      await this.loadTeamEvents()
    },

    async deleteTeamEvent(teamEventId: number) {
      const { apiFetch } = useApiClient()

      const { error } = await apiFetch(
        `/api/admin/settings/team-events/${teamEventId}`,
        { method: 'DELETE' },
      )

      if (error.value) {
        console.error('Failed to delete team event:', error.value)
        throw error.value
      }

      await this.loadTeamEvents()
    },
  },
})
