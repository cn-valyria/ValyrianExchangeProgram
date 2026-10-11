import { JSONParse, JSONStringify } from "json-with-bigint"

interface ApiClientOptions extends Omit<RequestInit, 'body'> {
  skipAuth?: boolean
  body?: unknown
}

// What apiFetch reports for a failed request. statusCode is 0 when the request never reached the API
export interface ApiError {
  status?: number
  statusCode: number
  statusMessage?: string
  message: string
}

interface ApiResponse<T> {
  data: Ref<T | null>
  error: Ref<ApiError | null>
}

let isRefreshing = false
let refreshPromise: Promise<boolean> | null = null

export const useApiClient = () => {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiUrl

  const apiFetch = async <T>(
    endpoint: string,
    options: ApiClientOptions = {}
  ): Promise<ApiResponse<T>> => {
    const { skipAuth = false, body, ...fetchOptions } = options
    const authStore = useAuthStore()

    // Helper to make the actual request
    const makeRequest = async (token: string | null): Promise<ApiResponse<T>> => {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
        ...((fetchOptions.headers as Record<string, string>) || {}),
      }

      if (!skipAuth && token) {
        headers['Authorization'] = `Bearer ${token}`
      }

      try {
        const response = await fetch(`${baseURL}${endpoint}`, {
          ...fetchOptions,
          headers,
          body: body ? JSONStringify(body) : undefined,
        })

        let data: T | null = null
        let error: ApiError | null = null

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
            const dataAsText = await response.text()
            data = JSONParse(dataAsText)
          }
        }

        return {
          data: ref(data) as Ref<T | null>,
          error: ref(error) as Ref<ApiError | null>,
        }
      } catch (err) {
        return {
          data: ref(null) as Ref<T | null>,
          error: ref({
            message: (err as { message?: string } | null)?.message || 'Network error',
            statusCode: 0,
          }) as Ref<ApiError | null>,
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
function isUnauthorizedError(error: ApiError): boolean {
  if (error.statusCode === 401 || error.status === 401) return true

  // Check for status message
  const statusText = error.statusMessage || error.message || ''
  return statusText.toLowerCase().includes('unauthorized')
}
