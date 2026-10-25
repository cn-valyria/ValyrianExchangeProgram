import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import {
  account,
  adminAccount,
  adminDeal,
  apiError,
  apiOk,
  dealFlag,
  massMessage,
  slot,
} from '../helpers/fixtures'

// The stores are thin wrappers around the API, so these check each one calls the right endpoint,
// keeps what comes back, and throws (or reports) failures

const { apiFetch } = vi.hoisted(() => ({ apiFetch: vi.fn() }))

mockNuxtImport('useApiClient', () => () => ({ apiFetch }))

const failure = { statusCode: 500, message: 'Server Error' }

// Calls a store action by name, for the tests that run the same check against several actions
const callAction = (store: object, action: string, args: unknown[]) =>
  (store as Record<string, (...args: unknown[]) => Promise<unknown>>)[action](
    ...args,
  )

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {})
})

describe('account store', () => {
  it('loads the account', async () => {
    apiFetch.mockResolvedValue(apiOk(account()))
    const store = useAccountStore()

    await store.loadAccount(1)

    expect(apiFetch).toHaveBeenCalledWith('/api/accounts/1')
    expect(store.account.rulerName).toBe('Bob')
  })

  it('loads the deals summary', async () => {
    const summary = {
      slotsUsed: 1,
      slotsTotal: 4,
      slotsAvailable: 3,
      slotsFull: false,
      activeDeals: [],
      notifications: [],
    }
    apiFetch.mockResolvedValue(apiOk(summary))
    const store = useAccountStore()

    await store.loadDealsSummary(1)

    expect(apiFetch).toHaveBeenCalledWith('/api/deals/account/1')
    expect(store.dealsSummary).toEqual(summary)
  })

  it('loads deal history, with paging only when given', async () => {
    apiFetch.mockResolvedValue(apiOk({ totalCount: 12, results: [] }))
    const store = useAccountStore()

    await store.loadDealHistory(1)
    expect(apiFetch).toHaveBeenLastCalledWith('/api/deals/account/1/history')

    await store.loadDealHistory(1, 10, 20)
    expect(apiFetch).toHaveBeenLastCalledWith(
      '/api/deals/account/1/history?limit=10&offset=20',
    )
    expect(store.dealHistory.totalCount).toBe(12)
  })

  it("keeps each deal's activity separately", async () => {
    apiFetch.mockResolvedValue(apiOk([{ id: 1 }]))
    const store = useAccountStore()

    await store.loadDealActivity(1, 7)

    expect(apiFetch).toHaveBeenCalledWith('/api/deals/7/activity?accountId=1')
    expect(store.dealActivity[7]).toEqual([{ id: 1 }])
  })

  it.each([
    ['loadAccount', [1]],
    ['loadDealsSummary', [1]],
    ['loadDealHistory', [1]],
    ['loadDealActivity', [1, 7]],
  ])('%s throws on failure and keeps what it had', async (action, args) => {
    apiFetch.mockResolvedValue(apiError(failure))
    const store = useAccountStore()
    const before = JSON.stringify(store.$state)

    await expect(callAction(store, action, args)).rejects.toEqual(failure)
    expect(JSON.stringify(store.$state)).toBe(before)
  })

  it.each(['loadAccount', 'loadDealsSummary', 'loadDealHistory'])(
    '%s ignores an empty response',
    async (action) => {
      apiFetch.mockResolvedValue(apiOk(null))
      const store = useAccountStore()
      const before = JSON.stringify(store.$state)

      await callAction(store, action, [1])

      expect(JSON.stringify(store.$state)).toBe(before)
    },
  )
})

describe('admin dashboard store', () => {
  const endpoints: Record<string, unknown> = {
    summary: { totalAccounts: 10, activeDeals: 4 },
    'slot-configs': [{ cashSentCycles: 1, totalSlots: 4 }],
    'deal-health': [{ health: 0, deals: 4 }],
    'alliance-slot-configs': [{ allianceName: 'Fark', totalSlots: 4 }],
    'alliance-deal-health': [{ allianceName: 'Fark', deals: 2 }],
    'activity?days=14': [{ day: '2026-10-09', legsSent: 3 }],
    'cancellation-reasons?days=30': [{ reason: 'Nation deleted', deals: 1 }],
  }

  it('loads every dashboard section in parallel', async () => {
    apiFetch.mockImplementation(async (url: string) =>
      apiOk(endpoints[url.replace('/api/admin/dashboard/', '')]),
    )
    const store = useAdminStore()

    await store.loadDashboard()

    expect(apiFetch).toHaveBeenCalledTimes(7)
    expect(store.dashboard).toEqual({
      summary: endpoints.summary,
      slotConfigs: endpoints['slot-configs'],
      dealHealth: endpoints['deal-health'],
      allianceSlotConfigs: endpoints['alliance-slot-configs'],
      allianceDealHealth: endpoints['alliance-deal-health'],
      activity: endpoints['activity?days=14'],
      cancellationReasons: endpoints['cancellation-reasons?days=30'],
    })
  })

  it.each(Object.keys(endpoints))('throws when %s fails', async (failing) => {
    apiFetch.mockImplementation(async (url: string) =>
      url.endsWith(`/${failing}`)
        ? apiError(failure)
        : apiOk(endpoints[url.replace('/api/admin/dashboard/', '')]),
    )
    const store = useAdminStore()

    await expect(store.loadDashboard()).rejects.toEqual(failure)
    expect(store.dashboard).toEqual({})
  })
})

describe('admin accounts store', () => {
  it('loads accounts, with or without on-hold accounts', async () => {
    apiFetch.mockResolvedValue(apiOk([adminAccount(1)]))
    const store = useAdminAccountsStore()

    await store.loadAllAccounts()
    expect(apiFetch).toHaveBeenLastCalledWith(
      '/api/accounts?includeOnHold=false',
    )
    await store.loadAllAccounts(true)
    expect(apiFetch).toHaveBeenLastCalledWith(
      '/api/accounts?includeOnHold=true',
    )
    expect(store.accounts).toHaveLength(1)
  })

  it('adds a created account to the list', async () => {
    apiFetch.mockResolvedValue(apiOk(adminAccount(2)))
    const store = useAdminAccountsStore()
    store.accounts = [adminAccount(1)]

    await store.createAccount({ nationId: 102 })

    expect(apiFetch).toHaveBeenCalledWith('/api/accounts', {
      method: 'POST',
      body: { nationId: 102 },
    })
    expect(store.accounts.map((a) => a.id)).toEqual([1, 2])
  })

  it('updates the editable fields of an account in the list', async () => {
    apiFetch.mockResolvedValue(apiOk(null))
    const store = useAdminAccountsStore()
    store.accounts = [adminAccount(1), adminAccount(2)]
    const update = {
      id: 2,
      role: 'S',
      uniqueCode: '54321',
      discord: 'sue',
      discordUniqueId: 5n,
      hasForeignMinistry: true,
      hasFederalAidCommission: true,
      hasDisasterReliefAgency: true,
      rulerName: 'Ignored',
    }

    await store.updateAccount(update)

    expect(apiFetch).toHaveBeenCalledWith('/api/accounts', {
      method: 'PUT',
      body: update,
    })
    expect(store.accounts[1]).toMatchObject({
      role: 'S',
      uniqueCode: '54321',
      discord: 'sue',
      discordUniqueId: 5n,
      hasForeignMinistry: true,
      rulerName: 'Ruler2',
    })
    expect(store.accounts[0]).toEqual(adminAccount(1))
  })

  it("updates nothing locally for an account that isn't in the list", async () => {
    apiFetch.mockResolvedValue(apiOk(null))
    const store = useAdminAccountsStore()
    store.accounts = [adminAccount(1)]

    await store.updateAccount({ id: 9, role: 'S' })

    expect(store.accounts).toEqual([adminAccount(1)])
  })

  it('loads and saves slots', async () => {
    apiFetch.mockResolvedValueOnce(apiOk({ slots: [slot(3)] }))
    const store = useAdminAccountsStore()

    expect(await store.loadAccountSlots(1)).toEqual([slot(3)])
    expect(apiFetch).toHaveBeenLastCalledWith('/api/slots/account/1')

    apiFetch.mockResolvedValueOnce(apiOk(null))
    await store.updateSlot(
      slot(
        3,
        { tech: 2 },
        { techReceivedCycles: 1, cashSentCycles: 3, cashReceivedCycles: 4 },
      ),
    )
    expect(apiFetch).toHaveBeenLastCalledWith('/api/slots/3', {
      method: 'PUT',
      body: {
        techSentCycles: 2,
        techReceivedCycles: 1,
        cashSentCycles: 3,
        cashReceivedCycles: 4,
      },
    })
  })

  it('has no slots when the response is empty', async () => {
    apiFetch.mockResolvedValue(apiOk(null))

    expect(await useAdminAccountsStore().loadAccountSlots(1)).toEqual([])
  })

  it('removes a deleted account from the list', async () => {
    apiFetch.mockResolvedValue(apiOk(null))
    const store = useAdminAccountsStore()
    store.accounts = [adminAccount(1), adminAccount(2)]

    await store.deleteAccount(1)
    await store.deleteAccount(99)

    expect(apiFetch).toHaveBeenCalledWith('/api/accounts/1', {
      method: 'DELETE',
    })
    expect(store.accounts.map((a) => a.id)).toEqual([2])
  })

  it.each([
    ['loadAllAccounts', []],
    ['createAccount', [{}]],
    ['updateAccount', [{ id: 1 }]],
    ['loadAccountSlots', [1]],
    ['updateSlot', [slot(1)]],
    ['deleteAccount', [1]],
  ])('%s throws on failure', async (action, args) => {
    apiFetch.mockResolvedValue(apiError(failure))
    const store = useAdminAccountsStore()
    store.accounts = [adminAccount(1)]

    await expect(callAction(store, action, args)).rejects.toEqual(failure)
    expect(store.accounts).toEqual([adminAccount(1)])
  })
})

describe('admin deals store', () => {
  it('loads a page of active deals, with search and paging only when given', async () => {
    apiFetch.mockResolvedValue(apiOk({ totalCount: 1, results: [adminDeal()] }))
    const store = useAdminDealsStore()

    await store.loadActiveDeals()
    expect(apiFetch).toHaveBeenLastCalledWith('/api/deals/admin/active')
    await store.loadActiveDeals('bob', 25, 50)
    expect(apiFetch).toHaveBeenLastCalledWith(
      '/api/deals/admin/active?search=bob&limit=25&offset=50',
    )
    expect(store.activeDeals.totalCount).toBe(1)
  })

  it('loads the wait queue and flags', async () => {
    const store = useAdminDealsStore()
    apiFetch.mockResolvedValueOnce(apiOk([{ accountId: 1 }]))
    await store.loadWaitQueue()
    apiFetch.mockResolvedValueOnce(apiOk([dealFlag()]))
    await store.loadFlags()

    expect(apiFetch).toHaveBeenCalledWith('/api/deals/admin/wait-queue')
    expect(apiFetch).toHaveBeenCalledWith('/api/deals/admin/flagged')
    expect(store.waitQueue).toHaveLength(1)
    expect(store.flags).toEqual([dealFlag()])
  })

  it('cancels and creates deals', async () => {
    apiFetch.mockResolvedValue(apiOk(null))
    const store = useAdminDealsStore()

    await store.cancelDeal(10, 'partner left')
    await store.createDeal(3, 4)

    expect(apiFetch).toHaveBeenCalledWith('/api/deals/10/cancel', {
      method: 'PUT',
      body: { reason: 'partner left' },
    })
    expect(apiFetch).toHaveBeenCalledWith('/api/deals', {
      method: 'POST',
      body: { slot1Id: 3, slot2Id: 4 },
    })
  })

  it('drops a resolved flag from the list', async () => {
    apiFetch.mockResolvedValue(apiOk(null))
    const store = useAdminDealsStore()
    store.flags = [dealFlag({ id: 5 }), dealFlag({ id: 6 })]

    await store.resolveFlag(5, 'sorted')

    expect(apiFetch).toHaveBeenCalledWith('/api/deals/admin/flags/5/resolve', {
      method: 'PUT',
      body: { adminNotes: 'sorted' },
    })
    expect(store.flags.map((f) => f.id)).toEqual([6])
  })

  it("loads an account's open slots", async () => {
    apiFetch.mockResolvedValueOnce(apiOk([slot(3)]))
    const store = useAdminDealsStore()

    expect(await store.loadAvailableSlots(1)).toEqual([slot(3)])
    expect(apiFetch).toHaveBeenLastCalledWith('/api/slots/available/1')

    apiFetch.mockResolvedValueOnce(apiOk(null))
    expect(await store.loadAvailableSlots(1)).toEqual([])
  })

  it.each([
    ['loadActiveDeals', []],
    ['loadWaitQueue', []],
    ['loadFlags', []],
    ['cancelDeal', [10, 'x']],
    ['createDeal', [1, 2]],
    ['resolveFlag', [5, '']],
    ['loadAvailableSlots', [1]],
  ])('%s throws on failure', async (action, args) => {
    apiFetch.mockResolvedValue(apiError(failure))
    const store = useAdminDealsStore()
    store.flags = [dealFlag({ id: 5 })]

    await expect(callAction(store, action, args)).rejects.toEqual(failure)
    expect(store.flags).toHaveLength(1)
  })
})

describe('admin access store', () => {
  const setting = {
    accountId: 1,
    rulerName: 'Bob',
    nationName: 'Buyland',
    vepRole: 'B',
    adminRoleId: 1,
    adminRoleName: 'Super Admin',
  }

  it('loads the admin access settings, tracking loading', async () => {
    const store = useAdminAccessStore()
    apiFetch.mockImplementation(async () => {
      expect(store.loading).toBe(true)
      return apiOk([setting])
    })

    await store.loadAdminAccessSettings()

    expect(apiFetch).toHaveBeenCalledWith('/api/admin/settings/access')
    expect(store.adminAccessSettings).toEqual([setting])
    expect(store.loading).toBe(false)
  })

  it('throws and records an error when loading fails', async () => {
    apiFetch.mockResolvedValue(apiError(failure))
    const store = useAdminAccessStore()

    await expect(store.loadAdminAccessSettings()).rejects.toEqual(failure)
    expect(store.error).toBe('Failed to load admin access settings')
    expect(store.loading).toBe(false)
  })

  it('creates and deletes access, then reloads the list', async () => {
    apiFetch.mockImplementation(
      async (_url: string, options?: { method: string }) =>
        apiOk(options ? null : [setting]),
    )
    const store = useAdminAccessStore()

    expect(
      await store.createAdminAccess({ accountId: 1, adminRoleId: 1 }),
    ).toBe(true)
    expect(apiFetch).toHaveBeenCalledWith('/api/admin/settings/access', {
      method: 'POST',
      body: { accountId: 1, adminRoleId: 1 },
    })
    expect(
      await store.deleteAdminAccess({ accountId: 1, adminRoleId: 1 }),
    ).toBe(true)
    expect(apiFetch).toHaveBeenCalledWith('/api/admin/settings/access', {
      method: 'DELETE',
      body: { accountId: 1, adminRoleId: 1 },
    })
    expect(store.adminAccessSettings).toEqual([setting])
  })

  it.each([
    ['createAdminAccess', 'Failed to create admin access'],
    ['deleteAdminAccess', 'Failed to delete admin access'],
  ])('%s reports failure instead of throwing', async (action, message) => {
    apiFetch.mockResolvedValue(apiError(failure))
    const store = useAdminAccessStore()

    expect(
      await callAction(store, action, [{ accountId: 1, adminRoleId: 1 }]),
    ).toBe(false)
    expect(store.error).toBe(message)
  })
})

describe('admin mass messages store', () => {
  const summary = {
    cnDate: '2026-10-09T00:00:00',
    dealStatesUpdatedOn: null,
    messages: [massMessage(1)],
  }

  it('loads the messages', async () => {
    apiFetch.mockResolvedValue(apiOk(summary))
    const store = useAdminMassMessagesStore()

    await store.loadMassMessages()

    expect(apiFetch).toHaveBeenCalledWith('/api/mass-messages')
    expect(store.summary).toEqual(summary)
  })

  it('marks a message sent, then reloads to show who got the credit', async () => {
    apiFetch.mockImplementation(async (_url: string, options?: unknown) =>
      apiOk(options ? null : summary),
    )
    const store = useAdminMassMessagesStore()

    await store.setMessageSent(1, true)

    expect(apiFetch).toHaveBeenCalledWith('/api/mass-messages/1/sent', {
      method: 'PUT',
      body: { sent: true },
    })
    expect(apiFetch).toHaveBeenLastCalledWith('/api/mass-messages')
    expect(store.summary).toEqual(summary)
  })

  it('throws on failure', async () => {
    apiFetch.mockResolvedValue(apiError(failure))
    const store = useAdminMassMessagesStore()

    await expect(store.loadMassMessages()).rejects.toEqual(failure)
    await expect(store.setMessageSent(1, true)).rejects.toEqual(failure)
    expect(apiFetch).toHaveBeenCalledTimes(2)
  })

  it('ignores an empty response', async () => {
    apiFetch.mockResolvedValue(apiOk(null))
    const store = useAdminMassMessagesStore()

    await store.loadMassMessages()

    expect(store.summary).toBeNull()
  })
})

describe('admin aid activity store', () => {
  const page = { totalCount: 1, results: [{ aidId: 9 }] }
  const params = () =>
    new URL(apiFetch.mock.lastCall![0], 'http://x').searchParams

  it('loads a page with the toggles and paging', async () => {
    apiFetch.mockResolvedValue(apiOk(page))
    const store = useAdminAidActivityStore()

    await store.loadPage(25, 50)

    expect(apiFetch.mock.lastCall![0]).toBe(
      '/api/deals/admin/aid-activity?includeOnHold=false&unmatchedOnly=false&limit=25&offset=50',
    )
    expect(store.currentPage).toEqual(page)
  })

  it('sends the filters, with dates as local yyyy-mm-dd', async () => {
    apiFetch.mockResolvedValue(apiOk(page))
    const store = useAdminAidActivityStore()
    store.filters = {
      search: 'Bob',
      direction: 'sent',
      sentSince: new Date(2026, 0, 5),
      sentUntil: new Date(2026, 9, 9),
    }
    store.includeOnHold = true
    store.unmatchedOnly = true

    await store.loadPage(10, 0)

    expect(Object.fromEntries(params())).toEqual({
      search: 'Bob',
      direction: 'sent',
      sentSince: '2026-01-05',
      sentUntil: '2026-10-09',
      includeOnHold: 'true',
      unmatchedOnly: 'true',
      limit: '10',
      offset: '0',
    })
  })

  it('only sends a direction along with search text', async () => {
    apiFetch.mockResolvedValue(apiOk(page))
    const store = useAdminAidActivityStore()
    store.filters = { direction: 'received' }

    await store.loadPage(10, 0)

    expect(params().has('direction')).toBe(false)
  })

  it('throws on failure', async () => {
    apiFetch.mockResolvedValue(apiError(failure))

    await expect(useAdminAidActivityStore().loadPage(10, 0)).rejects.toEqual(
      failure,
    )
  })

  it('ignores an empty response', async () => {
    apiFetch.mockResolvedValue(apiOk(null))
    const store = useAdminAidActivityStore()

    await store.loadPage(10, 0)

    expect(store.currentPage).toEqual({ totalCount: 0, results: [] })
  })
})
