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

const STORAGE_KEY = 'vep_auth'

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
    // Save auth state to session storage
    saveToStorage() {
      if (import.meta.client) {
        const authData = {
          user: this.user,
          accessToken: this.accessToken,
          refreshToken: this.refreshToken,
          isAuthenticated: this.isAuthenticated,
        }
        localStorage.setItem(STORAGE_KEY, JSON.stringify(authData))
      }
    },

    // Load auth state from session storage
    loadFromStorage() {
      if (import.meta.client) {
        const stored = localStorage.getItem(STORAGE_KEY)
        if (stored) {
          try {
            const authData = JSON.parse(stored)
            this.user = authData.user
            this.accessToken = authData.accessToken
            this.refreshToken = authData.refreshToken
            this.isAuthenticated = authData.isAuthenticated
          } catch (error) {
            console.error('Failed to parse stored auth data:', error)
            this.clearStorage()
          }
        }
      }
    },

    // Clear session storage
    clearStorage() {
      if (import.meta.client) {
        localStorage.removeItem(STORAGE_KEY)
      }
    },

    async login(credentials: { nationId?: string; rulerName?: string; uniqueCode: string }) {
      const config = useRuntimeConfig()
      // Login doesn't use the API client to avoid circular dependency
      const { data, error } = await useFetch<TokenResponse>(`${config.public.apiUrl}/api/user/authenticate`, {
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
        this.saveToStorage()
      }
    },

    async fetchUser() {
      if (!this.accessToken) return

      const { apiFetch } = useApiClient()
      const { data, error } = await apiFetch<User>('/api/user/account', {
        method: 'GET',
      })

      if (error.value) {
        console.error('Failed to fetch user:', error.value)
        return
      }

      if (data.value) {
        this.user = data.value as User
        this.isAuthenticated = true
        this.saveToStorage()
      }
    },

    async refreshAccessToken() {
      if (!this.refreshToken) return false

      const config = useRuntimeConfig()
      // Refresh doesn't use the API client to avoid circular dependency
      const { data, error } = await useFetch<TokenResponse>(`${config.public.apiUrl}/api/user/refresh`, {
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
        this.saveToStorage()
        return true
      }

      return false
    },

    logout() {
      this.user = null
      this.accessToken = null
      this.refreshToken = null
      this.isAuthenticated = false
      this.clearStorage()
      navigateTo('/login?auth=logout')
    },

    // Initialize auth state from storage
    async init() {
      this.loadFromStorage()
      // If we have a token but no user, fetch the user
      if (this.accessToken && !this.user) {
        await this.fetchUser()
      }
    },
  },
})
