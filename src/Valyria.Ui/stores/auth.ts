import { defineStore } from 'pinia'

interface User {
  nationId: number
  rulerName: string
  role: string
  allianceId: number
  allianceAcronym: string
  discord: string | null
  discordUniqueId: string | null
}

interface AuthState {
  user: User | null
  accessToken: string | null
  refreshToken: string | null
  isAuthenticated: boolean
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    user: null,
    accessToken: null,
    refreshToken: null,
    isAuthenticated: false,
  }),

  getters: {
    loggedInUser: (state) => state.user,
    isLoggedIn: (state) => state.isAuthenticated,
  },

  actions: {
    async login(credentials: { nationId?: number; rulerName?: string; uniqueCode: string }) {
      const config = useRuntimeConfig()
      const { data, error } = await useFetch(`${config.public.apiUrl}/api/user/authenticate`, {
        method: 'POST',
        body: credentials,
      })

      if (error.value) {
        throw new Error(error.value.message || 'Authentication failed')
      }

      if (data.value?.data) {
        const authData = data.value.data
        this.accessToken = authData.access_token
        this.refreshToken = authData.refresh_token
        await this.fetchUser()
      }
    },

    async fetchUser() {
      if (!this.accessToken) return

      const config = useRuntimeConfig()
      const { data, error } = await useFetch(`${config.public.apiUrl}/api/user/account`, {
        method: 'GET',
        headers: {
          Authorization: `Bearer ${this.accessToken}`,
        },
      })

      if (error.value) {
        console.error('Failed to fetch user:', error.value)
        return
      }

      if (data.value?.data) {
        this.user = data.value.data
        this.isAuthenticated = true
      }
    },

    async refreshAccessToken() {
      if (!this.refreshToken) return false

      const config = useRuntimeConfig()
      const { data, error } = await useFetch(`${config.public.apiUrl}/api/user/refresh`, {
        method: 'POST',
        body: {
          refresh_token: this.refreshToken,
        },
      })

      if (error.value) {
        console.error('Token refresh failed:', error.value)
        this.logout()
        return false
      }

      if (data.value?.data) {
        this.accessToken = data.value.data.access_token
        this.refreshToken = data.value.data.refresh_token
        return true
      }

      return false
    },

    logout() {
      this.user = null
      this.accessToken = null
      this.refreshToken = null
      this.isAuthenticated = false
      navigateTo('/?auth=failed')
    },

    // Initialize auth state from storage (if using persistence)
    async init() {
      // This could be expanded to load from localStorage/sessionStorage if needed
      if (this.accessToken) {
        await this.fetchUser()
      }
    },
  },

  persist: {
    storage: persistedState.localStorage,
    paths: ['accessToken', 'refreshToken'],
  },
})
