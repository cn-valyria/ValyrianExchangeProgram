import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const json = (body: unknown, status = 200) =>
  new Response(typeof body === 'string' ? body : JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })

describe('useApiClient', () => {
  const fetchMock = vi.fn<typeof fetch>()
  let apiUrl: string

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    apiUrl = useRuntimeConfig().public.apiUrl as string
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  const signIn = (accessToken = 'access-1', refreshToken: string | null = 'refresh-1') => {
    const authStore = useAuthStore()
    authStore.accessToken = accessToken
    authStore.refreshToken = refreshToken
    return authStore
  }

  const lastRequest = () => {
    const [url, init] = fetchMock.mock.lastCall!
    return { url, init: init!, headers: init!.headers as Record<string, string> }
  }

  it('sends the access token and parses JSON responses', async () => {
    signIn()
    fetchMock.mockResolvedValue(json({ id: 1, rulerName: 'Bob' }))

    const { data, error } = await useApiClient().apiFetch('/api/accounts/1')

    expect(error.value).toBeNull()
    expect(data.value).toEqual({ id: 1, rulerName: 'Bob' })
    const { url, headers } = lastRequest()
    expect(url).toBe(`${apiUrl}/api/accounts/1`)
    expect(headers.Authorization).toBe('Bearer access-1')
    expect(headers['Content-Type']).toBe('application/json')
  })

  it('keeps Discord IDs too large for a JavaScript number exact', async () => {
    signIn()
    fetchMock.mockResolvedValue(json('{"discordUniqueId":318737615257993226}'))

    const { data } = await useApiClient().apiFetch<{ discordUniqueId: bigint }>('/api/accounts/1')

    expect(data.value!.discordUniqueId).toBe(318737615257993226n)
  })

  it('serializes request bodies, including BigInts', async () => {
    signIn()
    fetchMock.mockResolvedValue(new Response(null, { status: 200 }))

    await useApiClient().apiFetch('/api/accounts', { method: 'PUT', body: { id: 1, discordUniqueId: 318737615257993226n } })

    const { init } = lastRequest()
    expect(init.method).toBe('PUT')
    expect(init.body).toBe('{"id":1,"discordUniqueId":318737615257993226}')
  })

  it('leaves the token off when asked to, or when signed out', async () => {
    signIn()
    fetchMock.mockResolvedValue(json({}))

    await useApiClient().apiFetch('/api/public', { skipAuth: true })
    expect(lastRequest().headers.Authorization).toBeUndefined()

    useAuthStore().accessToken = null
    await useApiClient().apiFetch('/api/public')
    expect(lastRequest().headers.Authorization).toBeUndefined()
  })

  it('keeps headers passed by the caller', async () => {
    signIn()
    fetchMock.mockResolvedValue(json({}))

    await useApiClient().apiFetch('/api/x', { headers: { 'X-Test': 'yes' } })

    expect(lastRequest().headers['X-Test']).toBe('yes')
  })

  it('returns no data for responses without a JSON body', async () => {
    signIn()
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    const { data, error } = await useApiClient().apiFetch('/api/deals/1/cancel', { method: 'PUT' })

    expect(data.value).toBeNull()
    expect(error.value).toBeNull()
  })

  it('reports failed responses with their status', async () => {
    signIn()
    fetchMock.mockResolvedValue(new Response('nope', { status: 404, statusText: 'Not Found' }))

    const { data, error } = await useApiClient().apiFetch('/api/deals/99')

    expect(data.value).toBeNull()
    expect(error.value).toMatchObject({ status: 404, statusCode: 404, statusMessage: 'Not Found', message: 'Not Found' })
  })

  it('reports network failures with status 0', async () => {
    signIn()
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    const { error } = await useApiClient().apiFetch('/api/deals/1')

    expect(error.value).toEqual({ message: 'Failed to fetch', statusCode: 0 })
  })

  it('falls back to a generic message for network failures without one', async () => {
    signIn()
    fetchMock.mockRejectedValue({})

    const { error } = await useApiClient().apiFetch('/api/deals/1')

    expect(error.value).toEqual({ message: 'Network error', statusCode: 0 })
  })

  describe('expired access tokens', () => {
    it('refreshes the token and retries once on a 401', async () => {
      const authStore = signIn()
      const refresh = vi.spyOn(authStore, 'refreshAccessToken').mockImplementation(async () => {
        authStore.accessToken = 'access-2'
        return true
      })
      fetchMock
        .mockResolvedValueOnce(new Response(null, { status: 401, statusText: 'Unauthorized' }))
        .mockResolvedValueOnce(json({ ok: true }))

      const { data, error } = await useApiClient().apiFetch('/api/accounts/1')

      expect(refresh).toHaveBeenCalledOnce()
      expect(error.value).toBeNull()
      expect(data.value).toEqual({ ok: true })
      expect(lastRequest().headers.Authorization).toBe('Bearer access-2')
    })

    it('returns the 401 when the refresh fails', async () => {
      const authStore = signIn()
      vi.spyOn(authStore, 'refreshAccessToken').mockResolvedValue(false)
      fetchMock.mockResolvedValue(new Response(null, { status: 401, statusText: 'Unauthorized' }))

      const { error } = await useApiClient().apiFetch('/api/accounts/1')

      expect(error.value.statusCode).toBe(401)
      expect(fetchMock).toHaveBeenCalledOnce()
    })

    it("doesn't try to refresh without a refresh token, or when auth was skipped", async () => {
      const authStore = signIn('access-1', null)
      const refresh = vi.spyOn(authStore, 'refreshAccessToken')
      fetchMock.mockResolvedValue(new Response(null, { status: 401, statusText: 'Unauthorized' }))

      await useApiClient().apiFetch('/api/accounts/1')
      authStore.refreshToken = 'refresh-1'
      await useApiClient().apiFetch('/api/user/token', { skipAuth: true })

      expect(refresh).not.toHaveBeenCalled()
    })

    it("doesn't refresh for other errors", async () => {
      const authStore = signIn()
      const refresh = vi.spyOn(authStore, 'refreshAccessToken')
      fetchMock.mockResolvedValue(new Response(null, { status: 403, statusText: 'Forbidden' }))

      await useApiClient().apiFetch('/api/admin/settings/access')

      expect(refresh).not.toHaveBeenCalled()
    })

    it('treats an "Unauthorized" status text as a 401', async () => {
      const authStore = signIn()
      const refresh = vi.spyOn(authStore, 'refreshAccessToken').mockResolvedValue(false)
      fetchMock.mockResolvedValue(new Response(null, { status: 400, statusText: 'Unauthorized request' }))

      await useApiClient().apiFetch('/api/accounts/1')

      expect(refresh).toHaveBeenCalledOnce()
    })

    it('shares one refresh between requests that fail together', async () => {
      const authStore = signIn()
      let finishRefresh!: (succeeded: boolean) => void
      const refresh = vi.spyOn(authStore, 'refreshAccessToken').mockImplementation(
        () => new Promise((resolve) => {
          finishRefresh = (succeeded) => {
            authStore.accessToken = 'access-2'
            resolve(succeeded)
          }
        }),
      )
      fetchMock.mockImplementation(async (_url, init) =>
        (init!.headers as Record<string, string>).Authorization === 'Bearer access-2'
          ? json({ ok: true })
          : new Response(null, { status: 401, statusText: 'Unauthorized' }),
      )

      const client = useApiClient()
      const first = client.apiFetch('/api/a')
      const second = client.apiFetch('/api/b')
      await vi.waitFor(() => expect(refresh).toHaveBeenCalled())
      // Let the second request hit its 401 and start waiting on the refresh in progress
      await new Promise((resolve) => setTimeout(resolve, 0))
      finishRefresh(true)
      const results = await Promise.all([first, second])

      expect(refresh).toHaveBeenCalledOnce()
      expect(results.map((r) => r.data.value)).toEqual([{ ok: true }, { ok: true }])
    })

    it('gives up on requests waiting for a refresh that fails', async () => {
      const authStore = signIn()
      let finishRefresh!: (succeeded: boolean) => void
      vi.spyOn(authStore, 'refreshAccessToken').mockImplementation(
        () => new Promise((resolve) => { finishRefresh = resolve }),
      )
      fetchMock.mockResolvedValue(new Response(null, { status: 401, statusText: 'Unauthorized' }))

      const client = useApiClient()
      const first = client.apiFetch('/api/a')
      const second = client.apiFetch('/api/b')
      await new Promise((resolve) => setTimeout(resolve, 0))
      finishRefresh(false)
      const results = await Promise.all([first, second])

      expect(results.map((r) => r.error.value.statusCode)).toEqual([401, 401])
      expect(fetchMock).toHaveBeenCalledTimes(2)
    })
  })
})
