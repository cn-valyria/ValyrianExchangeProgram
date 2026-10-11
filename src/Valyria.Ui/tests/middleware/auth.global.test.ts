import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { RouteLocationNormalized } from 'vue-router'
import authMiddleware from '~/middleware/auth.global'
import authPlugin from '~/plugins/auth.client'

// Both load when the test Nuxt app boots, so they use the real router and toasts rather than mocks

const route = (path: string) =>
  ({
    path,
    fullPath: path,
    query: {},
    params: {},
  }) as unknown as RouteLocationNormalized

let routerPush: ReturnType<typeof vi.spyOn>

beforeEach(() => {
  routerPush = vi.spyOn(useRouter(), 'push').mockResolvedValue(undefined)
  useToast().clear()
})

afterEach(() => {
  routerPush.mockRestore()
})

const signIn = (...adminRoles: string[]) => {
  const authStore = useAuthStore()
  authStore.isAuthenticated = true
  authStore.user = {
    accountId: 42,
    roles: adminRoles.map((name, id) => ({ id, name })),
  }
}

const run = (path: string) => authMiddleware(route(path), route('/'))

const redirectedTo = () => routerPush.mock.lastCall?.[0]

describe('auth middleware', () => {
  it.each(['/', '/login', '/how-it-works'])(
    'lets anyone see %s',
    async (path) => {
      await run(path)

      expect(routerPush).not.toHaveBeenCalled()
    },
  )

  it('sends signed-out visitors to log in', async () => {
    await run('/account/42')

    expect(redirectedTo()).toBe('/login?auth=failed')
  })

  it('lets members see their account pages', async () => {
    signIn()

    await run('/account/42')

    expect(routerPush).not.toHaveBeenCalled()
  })

  it.each([
    ['/admin', [], false],
    ['/admin', ['Admin - Lists'], false],
    ['/admin', ['Super Admin'], true],
    ['/admin/deals', ['Admin (Read-only)'], true],
    ['/admin/messages', [], false],
    ['/admin/messages', ['Admin - Lists'], true],
    ['/admin/messages', ['Admin (Read-only)'], true],
    ['/admin/messages', ['Super Admin'], true],
    ['/admin/settings', ['Admin (Read-only)'], false],
    ['/admin/settings', ['Admin - Lists'], false],
    ['/admin/settings', ['Super Admin'], true],
  ])('%s with roles %j allowed: %s', async (path, roles, allowed) => {
    signIn(...roles)

    await run(path)

    if (allowed) {
      expect(routerPush).not.toHaveBeenCalled()
      expect(useToast().toasts.value).toHaveLength(0)
    } else {
      vi.spyOn(console, 'warn').mockImplementation(() => {})
      expect(redirectedTo()).toBe('/')
      expect(useToast().toasts.value[0]).toMatchObject({
        title: 'Access Denied',
        color: 'error',
      })
    }
  })
})

describe('auth plugin', () => {
  it('restores the session when the app starts', async () => {
    const init = vi.spyOn(useAuthStore(), 'init').mockResolvedValue()

    await (authPlugin as unknown as (nuxtApp: unknown) => Promise<void>)(
      useNuxtApp(),
    )

    expect(init).toHaveBeenCalledOnce()
  })
})
