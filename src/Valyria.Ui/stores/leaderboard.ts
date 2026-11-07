import { defineStore } from 'pinia'

// One account's record on the leaderboard (Valyria.Core.DTOs.LeaderboardStanding)
export interface LeaderboardStanding {
  accountId: number
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
  // Legs sent in the window, not counting failed offers unless an admin excused them
  outgoingLegs: number
  // Unexcused flags raised in the window
  flags: number
  flagsPerLeg: number
  // Null until the account has enough outgoing legs to be ranked
  rank: number | null
  legsToQualify: number
  // CN date the oldest flag in the window stops counting
  nextFlagDropsOn: string | null
}

// Accounts ranked by unexcused flags per outgoing leg over a rolling window (Valyria.Core.DTOs.Leaderboard)
export interface Leaderboard {
  windowDays: number
  minimumOutgoingLegs: number
  windowStart: string
  rankedAccounts: number
  // The top 10 ranks. Exact ties share a rank
  top: LeaderboardStanding[]
  // The signed-in account's standing. Null if it's on hold
  mine: LeaderboardStanding | null
}

export const useLeaderboardStore = defineStore('leaderboard', {
  state: () => ({
    leaderboard: null as Leaderboard | null,
  }),

  actions: {
    async loadLeaderboard() {
      const { apiFetch } = useApiClient()

      const { data, error } = await apiFetch<Leaderboard>('/api/leaderboard')

      if (error.value) {
        console.error('Failed to load leaderboard:', error.value)
        throw error.value
      }

      if (data.value) {
        this.leaderboard = data.value as Leaderboard
      }
    },
  },
})
