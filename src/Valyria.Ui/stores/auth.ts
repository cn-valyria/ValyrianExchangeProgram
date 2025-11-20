import { defineStore } from 'pinia'

interface AccountRole {
  id: number
  name: string
}

interface User {
  accountId: number
  roles: AccountRole[]
}

interface TokenResponse {
  accessToken: string
  tokenType: string
  expiresIn: number
  refreshToken: string
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
    async login(credentials: { nationId?: string; rulerName?: string; uniqueCode: string }) {
      const config = useRuntimeConfig()
      const { data, error } = await useFetch(`${config.public.apiUrl}/api/user/authenticate`, {
        method: 'POST',
        body: credentials,
      })

      if (error.value) {
        throw new Error(error.value.message || 'Authentication failed')
      }

      if (data.value) {
        const authData = data.value as TokenResponse
        this.accessToken = authData.accessToken
        this.refreshToken = authData.refreshToken
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

      if (data.value) {
        this.user = data.value as User
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

      if (data.value) {
        const authData = data.value as TokenResponse
        this.accessToken = authData.accessToken
        this.refreshToken = authData.refreshToken
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
})
