import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// The auth plugin loads this store when the test Nuxt app boots, before mockNuxtImport could swap its
// imports, so these stub one layer down: $fetch under useFetch (login and refresh), fetch under the
// API client (loading the user), and the router under navigateTo

const tokens = { accessToken: 'access-1', tokenType: 'Bearer', expiresIn: 86400, refreshToken: 'refresh-1' }
const user = { accountId: 42, roles: [{ id: 1, name: 'Super Admin' }] }
const stored = () => JSON.parse(localStorage.getItem('vep_auth') ?? 'null')
const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })

const $fetchMock = vi.fn()
const fetchMock = vi.fn<typeof fetch>()
let routerPush: ReturnType<typeof vi.spyOn>

// useFetch reports a thrown $fetch error as its error
const rejected = (message: string) => Object.assign(new Error(message), { statusCode: 401 })

beforeEach(() => {
  vi.stubGlobal('$fetch', $fetchMock)
  vi.stubGlobal('fetch', fetchMock)
  routerPush = vi.spyOn(useRouter(), 'push').mockResolvedValue(undefined)
})

afterEach(() => {
  vi.unstubAllGlobals()
  routerPush.mockRestore()
})

describe('auth store', () => {
  describe('login', () => {
    it('stores the tokens, loads the user, and remembers the session', async () => {
      $fetchMock.mockResolvedValue(tokens)
      fetchMock.mockResolvedValue(json(user))
      const authStore = useAuthStore()

      await authStore.login({ nationId: '101', uniqueCode: '12345' })

      const [url, options] = $fetchMock.mock.lastCall!
      expect(url).toBe(`${useRuntimeConfig().public.apiUrl}/api/user/authenticate`)
      expect(options).toMatchObject({ method: 'POST', body: { nationId: '101', uniqueCode: '12345' } })
      const [userUrl, userRequest] = fetchMock.mock.lastCall!
      expect(userUrl).toBe(`${useRuntimeConfig().public.apiUrl}/api/user/account`)
      expect((userRequest!.headers as Record<string, string>).Authorization).toBe('Bearer access-1')
      expect(authStore.accessToken).toBe('access-1')
      expect(authStore.refreshToken).toBe('refresh-1')
      expect(authStore.loggedInUser).toEqual(user)
      expect(authStore.isLoggedIn).toBe(true)
      expect(stored()).toEqual({ user, accessToken: 'access-1', refreshToken: 'refresh-1', isAuthenticated: true })
    })

    it('throws when the credentials are rejected', async () => {
      $fetchMock.mockRejectedValue(rejected('Invalid credentials'))

      await expect(useAuthStore().login({ rulerName: 'Bob', uniqueCode: 'wrong' })).rejects.toThrow('Invalid credentials')
      expect(useAuthStore().isAuthenticated).toBe(false)
    })

    it('throws a generic message when the error has none', async () => {
      $fetchMock.mockRejectedValue(rejected(''))

      await expect(useAuthStore().login({ rulerName: 'Bob', uniqueCode: 'wrong' })).rejects.toThrow('Authentication failed')
    })
  })

  describe('fetchUser', () => {
    it('does nothing without an access token', async () => {
      await useAuthStore().fetchUser()

      expect(fetchMock).not.toHaveBeenCalled()
    })

    it('stays signed out when the user cannot be loaded', async () => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      fetchMock.mockResolvedValue(new Response(null, { status: 500, statusText: 'Server Error' }))
      const authStore = useAuthStore()
      authStore.accessToken = 'access-1'

      await authStore.fetchUser()

      expect(authStore.user).toBeNull()
      expect(authStore.isAuthenticated).toBe(false)
    })
  })

  describe('refreshAccessToken', () => {
    it('is false without a refresh token', async () => {
      expect(await useAuthStore().refreshAccessToken()).toBe(false)
      expect($fetchMock).not.toHaveBeenCalled()
    })

    it('swaps in the new tokens', async () => {
      $fetchMock.mockResolvedValue({ ...tokens, accessToken: 'access-2', refreshToken: 'refresh-2' })
      const authStore = useAuthStore()
      authStore.refreshToken = 'refresh-1'

      expect(await authStore.refreshAccessToken()).toBe(true)

      expect($fetchMock.mock.lastCall![1]).toMatchObject({ method: 'POST', body: { refresh_token: 'refresh-1' } })
      expect(authStore.accessToken).toBe('access-2')
      expect(authStore.refreshToken).toBe('refresh-2')
      expect(stored().accessToken).toBe('access-2')
    })

    it('logs out when the refresh token is rejected', async () => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      $fetchMock.mockRejectedValue(rejected('Unauthorized'))
      const authStore = useAuthStore()
      authStore.refreshToken = 'refresh-1'
      authStore.isAuthenticated = true

      expect(await authStore.refreshAccessToken()).toBe(false)

      expect(authStore.isAuthenticated).toBe(false)
      expect(authStore.refreshToken).toBeNull()
      expect(routerPush).toHaveBeenCalled()
    })

    it('is false when the refresh returns nothing', async () => {
      $fetchMock.mockResolvedValue(null)
      const authStore = useAuthStore()
      authStore.refreshToken = 'refresh-1'

      expect(await authStore.refreshAccessToken()).toBe(false)
    })
  })

  describe('logout', () => {
    it('forgets the session', () => {
      localStorage.setItem('vep_auth', JSON.stringify({ accessToken: 'x' }))
      const authStore = useAuthStore()
      Object.assign(authStore, { user, accessToken: 'a', refreshToken: 'r', isAuthenticated: true })

      authStore.logout()

      expect(authStore.$state).toEqual({ user: null, accessToken: null, refreshToken: null, isAuthenticated: false })
      expect(localStorage.getItem('vep_auth')).toBeNull()
    })

    // Relative to the current page: from /account/5 this goes to /account/login, and from /admin/settings to
    // /admin/login. The auth middleware then sends the signed-out user to /login?auth=failed, so they see
    // "Authentication Required" instead of "Logout Successful"
    it.skip('BUG: goes to the login page from any page (navigateTo path is missing its leading slash)', () => {
      useAuthStore().logout()

      expect(routerPush).toHaveBeenCalledWith('/login?auth=logout')
    })
  })

  describe('init', () => {
    it('restores a remembered session without reloading the user', async () => {
      localStorage.setItem('vep_auth', JSON.stringify({ user, accessToken: 'a', refreshToken: 'r', isAuthenticated: true }))
      const authStore = useAuthStore()

      await authStore.init()

      expect(authStore.loggedInUser).toEqual(user)
      expect(authStore.isAuthenticated).toBe(true)
      expect(fetchMock).not.toHaveBeenCalled()
    })

    it('loads the user when only the tokens were remembered', async () => {
      localStorage.setItem('vep_auth', JSON.stringify({ user: null, accessToken: 'a', refreshToken: 'r', isAuthenticated: false }))
      fetchMock.mockResolvedValue(json(user))
      const authStore = useAuthStore()

      await authStore.init()

      expect(authStore.loggedInUser).toEqual(user)
      expect(authStore.isAuthenticated).toBe(true)
    })

    it('starts signed out with nothing remembered', async () => {
      await useAuthStore().init()

      expect(useAuthStore().isAuthenticated).toBe(false)
      expect(fetchMock).not.toHaveBeenCalled()
    })

    it('throws away a corrupted session', async () => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      localStorage.setItem('vep_auth', '{not json')

      await useAuthStore().init()

      expect(useAuthStore().isAuthenticated).toBe(false)
      expect(localStorage.getItem('vep_auth')).toBeNull()
    })
  })
})
