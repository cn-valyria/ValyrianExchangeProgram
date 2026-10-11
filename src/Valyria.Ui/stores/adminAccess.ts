import { defineStore } from 'pinia'

export interface AdminAccessSetting {
  accountId: number
  rulerName: string
  nationName: string
  vepRole: string
  adminRoleId: number
  adminRoleName: string
}

export interface AdminAccessCreateRequest {
  accountId: number
  adminRoleId: number
}

export interface AdminAccessDeleteRequest {
  accountId: number
  adminRoleId: number
}

export const useAdminAccessStore = defineStore('adminAccess', {
  state: () => ({
    adminAccessSettings: [] as AdminAccessSetting[],
    loading: false,
    error: null as string | null,
  }),

  actions: {
    async loadAdminAccessSettings() {
      const { apiFetch } = useApiClient()
      this.loading = true
      this.error = null

      try {
        const { data, error } = await apiFetch<AdminAccessSetting[]>(
          '/api/admin/settings/access',
        )

        if (error.value) {
          console.error('Failed to load admin access settings:', error.value)
          this.error = 'Failed to load admin access settings'
          throw error.value
        }

        if (data.value) {
          this.adminAccessSettings = data.value
        }
      } finally {
        this.loading = false
      }
    },

    async createAdminAccess(request: AdminAccessCreateRequest) {
      const { apiFetch } = useApiClient()
      this.error = null

      try {
        const { error } = await apiFetch('/api/admin/settings/access', {
          method: 'POST',
          body: request,
        })

        if (error.value) {
          console.error('Failed to create admin access:', error.value)
          this.error = 'Failed to create admin access'
          throw error.value
        }

        await this.loadAdminAccessSettings()
        return true
      } catch (e) {
        console.error('Error creating admin access:', e)
        return false
      }
    },

    async deleteAdminAccess(request: AdminAccessDeleteRequest) {
      const { apiFetch } = useApiClient()
      this.error = null

      try {
        const { error } = await apiFetch('/api/admin/settings/access', {
          method: 'DELETE',
          body: request,
        })

        if (error.value) {
          console.error('Failed to delete admin access:', error.value)
          this.error = 'Failed to delete admin access'
          throw error.value
        }

        await this.loadAdminAccessSettings()
        return true
      } catch (e) {
        console.error('Error deleting admin access:', e)
        return false
      }
    },
  },
})
