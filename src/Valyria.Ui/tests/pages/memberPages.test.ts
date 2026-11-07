import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AccountPage from '~/pages/account/[id].vue'
import HowItWorksPage from '~/pages/how-it-works.vue'
import IndexPage from '~/pages/index.vue'
import LeaderboardPage from '~/pages/leaderboard.vue'
import LoginPage from '~/pages/login.vue'
import { useLeaderboardStore } from '~/stores/leaderboard'
import {
  account,
  dealDetail,
  leaderboard,
  leaderboardStanding,
} from '../helpers/fixtures'
import {
  button,
  click,
  inputByPlaceholder,
  mountInApp,
  toasts,
  type,
} from '../helpers/dom'

let routerPush: ReturnType<typeof vi.spyOn>

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {})
  routerPush = vi.spyOn(useRouter(), 'push').mockResolvedValue(undefined)
})

afterEach(() => {
  routerPush.mockRestore()
})

const signIn = (accountId = 1) => {
  const authStore = useAuthStore()
  authStore.isAuthenticated = true
  authStore.user = { accountId, roles: [] }
  return authStore
}

describe('account page', () => {
  const stubLoads = () => {
    const store = useAccountStore()
    const loads = {
      account: vi.spyOn(store, 'loadAccount').mockImplementation(async () => {
        store.account = account({
          rulerName: 'Bob',
          nationName: 'Buyland',
          nationId: 101,
        })
      }),
      summary: vi
        .spyOn(store, 'loadDealsSummary')
        .mockImplementation(async () => {
          store.dealsSummary = {
            slotsUsed: 1,
            slotsTotal: 4,
            slotsAvailable: 3,
            slotsFull: false,
            teamEvent: null,
            activeDeals: [dealDetail({ isMyTurnToSend: true })],
            notifications: [],
          }
        }),
      history: vi.spyOn(store, 'loadDealHistory').mockResolvedValue(),
    }
    return loads
  }

  it("loads and shows the signed-in member's account", async () => {
    signIn(1)
    const loads = stubLoads()

    const wrapper = await mountInApp(AccountPage, {}, { route: '/account/1' })

    expect(loads.account).toHaveBeenCalledWith(1)
    expect(loads.summary).toHaveBeenCalledWith(1)
    expect(loads.history).toHaveBeenCalledWith(1)
    expect(wrapper.text()).toContain('Bob of Buyland')
    expect(wrapper.text()).toContain('1 / 4')
    expect(wrapper.text()).toContain('Action Needed Today')
    expect(wrapper.text()).toContain('Active Deals')
    expect(wrapper.text()).toContain('Deal History')
    expect(wrapper.find('.animate-spin').exists()).toBe(false)
  })

  it('opens the VEP settings from the gear next to the header', async () => {
    signIn(1)
    stubLoads()

    await mountInApp(AccountPage, {}, { route: '/account/1' })
    await click(
      document.body.querySelector<HTMLButtonElement>(
        'button[aria-label="VEP Settings"]',
      )!,
    )

    expect(document.body.textContent).toContain('Discord notifications')
  })

  it("sends members away from someone else's account", async () => {
    signIn(1)
    const loads = stubLoads()

    await mountInApp(AccountPage, {}, { route: '/account/2' })

    expect(routerPush).toHaveBeenCalledWith('/')
    expect(toasts()[0]).toMatchObject({ title: 'Access Denied' })
    expect(loads.account).not.toHaveBeenCalled()
  })

  it("shows an error when the account can't be loaded", async () => {
    signIn(1)
    stubLoads().account.mockRejectedValue(new Error('down'))

    const wrapper = await mountInApp(AccountPage, {}, { route: '/account/1' })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load account data. Please try again.',
    })
    expect(wrapper.find('.animate-spin').exists()).toBe(false)
  })
})

describe('login page', () => {
  const fillIn = async (username: string, uniqueCode: string) => {
    await type(
      inputByPlaceholder('Enter your Nation ID or Ruler Name'),
      username,
    )
    await type(inputByPlaceholder('Enter your unique code'), uniqueCode)
  }

  it('logs in by nation ID', async () => {
    const login = vi.spyOn(useAuthStore(), 'login').mockResolvedValue()
    await mountInApp(LoginPage, {}, { route: '/login' })

    await fillIn(' 101 ', '12345')
    await click(button('Log In'))

    expect(login).toHaveBeenCalledWith({
      nationId: '101',
      rulerName: undefined,
      uniqueCode: '12345',
    })
    expect(toasts()[0]).toMatchObject({ title: 'Login Successful' })
    expect(routerPush).toHaveBeenLastCalledWith('/')
  })

  it('logs in by ruler name, then goes where the visitor was headed', async () => {
    const login = vi.spyOn(useAuthStore(), 'login').mockResolvedValue()
    await mountInApp(LoginPage, {}, { route: '/login?redirect=/account/1' })

    await fillIn('Bob the Builder', '12345')
    await click(button('Log In'))

    expect(login).toHaveBeenCalledWith({
      nationId: undefined,
      rulerName: 'Bob the Builder',
      uniqueCode: '12345',
    })
    expect(routerPush).toHaveBeenLastCalledWith('/account/1')
  })

  it.each(['https://evil.example.com', '//evil.example.com', '/\\evil'])(
    "won't redirect off the site to %s after logging in",
    async (redirect) => {
      vi.spyOn(useAuthStore(), 'login').mockResolvedValue()
      await mountInApp(
        LoginPage,
        {},
        { route: `/login?redirect=${encodeURIComponent(redirect)}` },
      )

      await fillIn('101', '12345')
      await click(button('Log In'))

      expect(routerPush).toHaveBeenLastCalledWith('/')
    },
  )

  it('says when the credentials are wrong', async () => {
    vi.spyOn(useAuthStore(), 'login').mockRejectedValue(
      new Error('Invalid credentials'),
    )
    await mountInApp(LoginPage, {}, { route: '/login' })

    await fillIn('101', 'wrong')
    await click(button('Log In'))

    expect(toasts()[0]).toMatchObject({ title: 'Login Failed', color: 'error' })
    expect(routerPush).not.toHaveBeenCalled()
  })

  it('requires both fields', async () => {
    const login = vi.spyOn(useAuthStore(), 'login')
    const wrapper = await mountInApp(LoginPage, {}, { route: '/login' })

    await click(button('Log In'))

    expect(login).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Nation ID or Ruler Name is required')
    expect(wrapper.text()).toContain('Unique Code is required')
  })

  it('sends signed-in members home', async () => {
    signIn()

    await mountInApp(LoginPage, {}, { route: '/login' })

    expect(routerPush).toHaveBeenCalledWith('/')
  })

  it('explains why a visitor was sent to log in', async () => {
    await mountInApp(LoginPage, {}, { route: '/login?auth=failed' })

    expect(toasts()[0]).toMatchObject({
      title: 'Authentication Required',
      color: 'error',
    })
  })

  it('confirms a logout', async () => {
    await mountInApp(LoginPage, {}, { route: '/login?auth=logout' })

    expect(toasts()[0]).toMatchObject({
      title: 'Logout Successful',
      color: 'success',
    })
  })
})

describe('leaderboard page', () => {
  const stubLoad = (loaded = leaderboard()) =>
    vi
      .spyOn(useLeaderboardStore(), 'loadLeaderboard')
      .mockImplementation(async () => {
        useLeaderboardStore().leaderboard = loaded
      })

  it('shows the podium, the rest of the top 10, and your standing', async () => {
    signIn(7)
    const top = Array.from({ length: 10 }, (_, i) =>
      leaderboardStanding(i + 1, {
        flags: i,
        flagsPerLeg: i / 10,
      }),
    )
    stubLoad(
      leaderboard({
        top,
        mine: leaderboardStanding(7, {
          flags: 6,
          flagsPerLeg: 0.6,
          nextFlagDropsOn: '2026-12-01T00:00:00',
        }),
      }),
    )

    const wrapper = await mountInApp(
      LeaderboardPage,
      {},
      { route: '/leaderboard' },
    )

    const text = wrapper.text()
    expect(text).toContain('Deal Partner Leaderboard')
    expect(text).toContain('Accounts need 5 outgoing legs since 2026-08-10')
    expect(text).toContain('Your standing')
    expect(text).toContain('#7 of 12')
    expect(text).toContain('Your oldest flag stops counting on 2026-12-01.')
    expect(text).toContain('Perfect record')
    expect(text).toContain('0.90 flags per leg')
    expect(text).toContain('9 flags across 10 outgoing legs')
    expect(text).toContain('Hall of Fame')
    expect(text).toContain('Nation10')
  })

  it('tells an unranked member how many legs they need, when no one is ranked', async () => {
    signIn(1)
    stubLoad(
      leaderboard({
        rankedAccounts: 0,
        mine: leaderboardStanding(1, {
          rank: null,
          outgoingLegs: 2,
          legsToQualify: 3,
        }),
      }),
    )

    const wrapper = await mountInApp(
      LeaderboardPage,
      {},
      { route: '/leaderboard' },
    )

    expect(wrapper.text()).toContain('Not ranked yet')
    expect(wrapper.text()).toContain(
      'Send 3 more outgoing legs in the next 90 days',
    )
    expect(wrapper.text()).toContain('No rankings available yet')
    expect(wrapper.text()).not.toContain('Hall of Fame')
  })

  it('reports a failed load', async () => {
    signIn(1)
    vi.spyOn(useLeaderboardStore(), 'loadLeaderboard').mockRejectedValue(
      new Error('boom'),
    )

    await mountInApp(LeaderboardPage, {}, { route: '/leaderboard' })

    expect(toasts()[0]).toMatchObject({ color: 'error' })
  })
})

describe('public pages', () => {
  it('the home page introduces VEP', async () => {
    const wrapper = await mountInApp(IndexPage, {}, { route: '/' })

    expect(wrapper.text()).toContain('Valyrian Exchange Program')
    expect(wrapper.find('a[href="/how-it-works"]').exists()).toBe(true)
  })

  it('the how it works page explains the program', async () => {
    const wrapper = await mountInApp(
      HowItWorksPage,
      {},
      { route: '/how-it-works' },
    )

    expect(wrapper.text().length).toBeGreaterThan(500)
  })
})
