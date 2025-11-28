interface ApiClientOptions extends RequestInit {
  skipAuth?: boolean
  body?: any
}

let isRefreshing = false
let refreshPromise: Promise<boolean> | null = null

export const useApiClient = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiUrl

  const apiFetch = async <T>(
    endpoint: string,
    options: ApiClientOptions = {}
  ): Promise<{ data: Ref<T | null>; error: Ref<any> }> => {
    const { skipAuth = false, body, ...fetchOptions } = options
    const authStore = useAuthStore()

    // Helper to make the actual request
    const makeRequest = async (token: string | null): Promise<{ data: Ref<T | null>; error: Ref<any> }> => {
      const headers: HeadersInit = {
        'Content-Type': 'application/json',
        ...((fetchOptions.headers as HeadersInit) || {}),
      }

      if (!skipAuth && token) {
        headers['Authorization'] = `Bearer ${token}`
      }

      try {
        const response = await fetch(`${baseURL}${endpoint}`, {
          ...fetchOptions,
          headers,
          body: body ? JSON.stringify(body) : undefined,
        })

        let data: T | null = null
        let error: any = null

        if (!response.ok) {
          error = {
            status: response.status,
            statusCode: response.status,
            statusMessage: response.statusText,
            message: response.statusText,
          }
        } else {
          const contentType = response.headers.get('content-type')
          if (contentType && contentType.includes('application/json')) {
            data = await response.json()
          }
        }

        return {
          data: ref(data) as Ref<T | null>,
          error: ref(error) as Ref<any>,
        }
      } catch (err: any) {
        return {
          data: ref(null) as Ref<T | null>,
          error: ref({
            message: err.message || 'Network error',
            statusCode: 0,
          }) as Ref<any>,
        }
      }
    }

    // Make the initial request
    let response = await makeRequest(authStore.accessToken)

    // Check if we got a 401 error and should attempt token refresh
    if (
      response.error.value &&
      !skipAuth &&
      authStore.refreshToken &&
      isUnauthorizedError(response.error.value)
    ) {
      // If a refresh is already in progress, wait for it
      if (isRefreshing && refreshPromise) {
        const refreshSuccess = await refreshPromise
        if (refreshSuccess) {
          // Retry with the new token
          response = await makeRequest(authStore.accessToken)
        }
      } else {
        // Start a new refresh
        isRefreshing = true
        refreshPromise = authStore.refreshAccessToken()

        try {
          const refreshSuccess = await refreshPromise
          if (refreshSuccess) {
            // Retry the original request with the new token
            response = await makeRequest(authStore.accessToken)
          }
        } finally {
          isRefreshing = false
          refreshPromise = null
        }
      }
    }

    return response
  }

  return {
    apiFetch,
  }
}

// Helper to check if an error is a 401 Unauthorized
function isUnauthorizedError(error: any): boolean {
  // Check for status code in various error structures
  if (error?.statusCode === 401) return true
  if (error?.response?.status === 401) return true
  if (error?.data?.statusCode === 401) return true
  if (error?.status === 401) return true

  // Check for status message
  const statusText = error?.statusMessage || error?.message || ''
  if (statusText.toLowerCase().includes('unauthorized')) return true

  return false
}
