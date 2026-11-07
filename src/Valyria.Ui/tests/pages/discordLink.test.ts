import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import DiscordLinkPage from '~/pages/discord/link.vue'
import { account } from '../helpers/fixtures'
import { button, click, hasButton, mountInApp, toasts } from '../helpers/dom'

let routerPush: ReturnType<typeof vi.spyOn>

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {})
  routerPush = vi.spyOn(useRouter(), 'push').mockResolvedValue(undefined)
  useToast().clear()
})

afterEach(() => {
  routerPush.mockRestore()
})

const signIn = (accountId = 1) => {
  const authStore = useAuthStore()
  authStore.isAuthenticated = true
  authStore.user = { accountId, roles: [] }
}

const bobLink = {
  discordId: '123456789012345678',
  discordUsername: 'bob_',
  expiresAt: '2026-11-07T16:15:00Z',
}

const stubStore = (discordUniqueId: number | bigint | null = null) => {
  const store = useAccountStore()
  return {
    preview: vi.spyOn(store, 'previewDiscordLink').mockResolvedValue(bobLink),
    load: vi.spyOn(store, 'loadAccount').mockImplementation(async () => {
      store.account = account({
        rulerName: 'Bob',
        nationName: 'Buyland',
        discord: 'old_bob',
        discordUniqueId: discordUniqueId as number | null,
      })
    }),
    link: vi.spyOn(store, 'linkDiscordAccount').mockResolvedValue(bobLink),
  }
}

const route = '/discord/link?token=abc.def'

describe('Discord link page', () => {
  it('asks signed-out visitors to log in, and brings them back to the link', async () => {
    const calls = stubStore()

    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    expect(wrapper.text()).toContain('Log into your VEP account')
    const login = new URL(
      button('Log In').getAttribute('href')!,
      'http://localhost',
    )
    expect(login.pathname).toBe('/login')
    expect(login.searchParams.get('redirect')).toBe(route)
    expect(calls.preview).not.toHaveBeenCalled()
  })

  it('says when the link has no token', async () => {
    signIn()
    const calls = stubStore()

    const wrapper = await mountInApp(
      DiscordLinkPage,
      {},
      { route: '/discord/link' },
    )

    expect(wrapper.text()).toContain('This link is missing its code')
    expect(calls.preview).not.toHaveBeenCalled()
  })

  it('shows the Discord account and the VEP account it would be linked to', async () => {
    signIn(1)
    const calls = stubStore()

    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    expect(calls.preview).toHaveBeenCalledWith('abc.def')
    expect(calls.load).toHaveBeenCalledWith(1)
    expect(wrapper.text()).toContain(
      'Link the Discord account bob_ to your VEP account Bob of Buyland?',
    )
    expect(wrapper.text()).not.toContain('This replaces')
    expect(hasButton('Authorize')).toBe(true)
    expect(calls.link).not.toHaveBeenCalled()
  })

  it('warns when it would replace a different linked Discord account', async () => {
    signIn(1)
    stubStore(BigInt('999999999999999999'))

    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    expect(wrapper.text()).toContain(
      'This replaces your linked Discord account',
    )
    expect(wrapper.text()).toContain('old_bob')
  })

  it("doesn't warn when the same Discord account is already linked", async () => {
    signIn(1)
    stubStore(BigInt('123456789012345678'))

    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    expect(wrapper.text()).not.toContain('This replaces')
  })

  it('links the account once the member authorizes it', async () => {
    signIn(1)
    const calls = stubStore()
    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    await click(button('Authorize'))

    expect(calls.link).toHaveBeenCalledWith('abc.def')
    expect(wrapper.text()).toContain('You can close this window now')
    expect(button('Go to my account').getAttribute('href')).toBe('/account/1')
  })

  it('cancelling goes back to the account without linking', async () => {
    signIn(1)
    const calls = stubStore()
    await mountInApp(DiscordLinkPage, {}, { route })

    expect(button('Cancel').getAttribute('href')).toBe('/account/1')
    expect(calls.link).not.toHaveBeenCalled()
  })

  it('says when the link is invalid or expired', async () => {
    signIn()
    stubStore().preview.mockRejectedValue({ statusCode: 400 })

    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    expect(wrapper.text()).toContain('This link is invalid or has expired')
    expect(hasButton('Authorize')).toBe(false)
  })

  it('says when the link expires before it is approved', async () => {
    signIn()
    stubStore().link.mockRejectedValue({ statusCode: 400 })
    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    await click(button('Authorize'))

    expect(wrapper.text()).toContain('This link is invalid or has expired')
  })

  it('shows an error when linking fails, and lets the member try again', async () => {
    signIn()
    stubStore().link.mockRejectedValue({ statusCode: 500 })
    await mountInApp(DiscordLinkPage, {}, { route })

    await click(button('Authorize'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(hasButton('Authorize')).toBe(true)
  })

  it('shows an error when the link fails to load', async () => {
    signIn()
    stubStore().preview.mockRejectedValue({ statusCode: 0 })

    const wrapper = await mountInApp(DiscordLinkPage, {}, { route })

    expect(wrapper.text()).toContain('Something went wrong loading this link')
  })
})
