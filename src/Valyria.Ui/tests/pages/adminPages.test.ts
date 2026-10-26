import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { UPagination, USwitch, UTabs } from '#components'
import AccountsPage from '~/pages/admin/accounts.vue'
import AidActivityPage from '~/pages/admin/aid-activity.vue'
import DealsPage from '~/pages/admin/deals.vue'
import FlagsPage from '~/pages/admin/flags.vue'
import DashboardPage from '~/pages/admin/index.vue'
import MessagesPage from '~/pages/admin/messages.vue'
import SettingsPage from '~/pages/admin/settings.vue'
import { DealActivityState, DealLegOutcome } from '~/stores/account'
import type { AidActivityItem } from '~/stores/adminAidActivity'
import { DealFlagReason, DealFlagType } from '~/stores/adminDeals'
import { MassMessageCode } from '~/stores/adminMassMessages'
import {
  addDays,
  cnToday,
  formatCnDate,
  Team,
} from '~/infrastructure/teamEvents'
import {
  accountSimplified,
  adminAccount,
  adminDeal,
  cancelledDealFlag,
  dealFlag,
  massMessage,
  recipient,
  teamBonusSlot,
  teamEvent,
} from '../helpers/fixtures'
import {
  bodyText,
  button,
  buttons,
  click,
  hasButton,
  inputByPlaceholder,
  mountInApp,
  toasts,
  type,
} from '../helpers/dom'

// happy-dom has no canvas
vi.mock('vue-chartjs', () => import('../helpers/chartStub'))

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {})
  vi.spyOn(console, 'log').mockImplementation(() => {})
})

const signIn = (...adminRoles: string[]) => {
  const authStore = useAuthStore()
  authStore.isAuthenticated = true
  authStore.user = {
    accountId: 1,
    roles: adminRoles.map((name, id) => ({ id, name })),
  }
}

const tableRows = () =>
  Array.from(document.querySelectorAll('tbody tr')).map(
    (row) => row.textContent ?? '',
  )

describe('admin dashboard page', () => {
  it('loads and shows the dashboard', async () => {
    signIn('Super Admin')
    const store = useAdminStore()
    vi.spyOn(store, 'loadDashboard').mockImplementation(async () => {
      store.dashboard = {
        summary: {
          totalAccounts: 120,
          activeDeals: 230,
          openFlags: 0,
          flaggedAccounts: 0,
        },
        slotConfigs: [
          {
            techSentCycles: 0,
            techReceivedCycles: 1,
            cashSentCycles: 1,
            cashReceivedCycles: 0,
            accounts: 60,
            totalSlots: 240,
            slotsInDeals: 230,
            openSlots: 10,
            openAccounts: 4,
            flaggedOpenSlots: 0,
          },
          {
            techSentCycles: 1,
            techReceivedCycles: 0,
            cashSentCycles: 0,
            cashReceivedCycles: 1,
            accounts: 60,
            totalSlots: 230,
            slotsInDeals: 230,
            openSlots: 0,
            openAccounts: 0,
            flaggedOpenSlots: 0,
          },
        ],
        dealHealth: [],
        allianceSlotConfigs: [
          {
            allianceName: 'Fark',
            allianceAccounts: 60,
            techSentCycles: 0,
            techReceivedCycles: 1,
            cashSentCycles: 1,
            cashReceivedCycles: 0,
            accounts: 60,
            totalSlots: 240,
            slotsInDeals: 230,
            openSlots: 10,
            openAccounts: 4,
            flaggedOpenSlots: 0,
          },
        ],
        allianceDealHealth: [],
        activity: [],
        cancellationReasons: [{ reason: 'Nation deleted', deals: 2 }],
      }
    })

    await mountInApp(DashboardPage, {}, { route: '/admin' })

    expect(bodyText()).toContain('VEP Dashboard')
    expect(bodyText()).toContain('230 active')
    expect(bodyText()).toContain('98%')
    expect(bodyText()).toContain('Room for 10 Seller slots')
    expect(bodyText()).toContain('Nation deleted')
    expect(bodyText()).toContain('60 accounts')
    expect(document.querySelector('.animate-spin')).toBeNull()
  })

  it("shows an error when the dashboard can't be loaded", async () => {
    signIn('Super Admin')
    vi.spyOn(useAdminStore(), 'loadDashboard').mockRejectedValue(
      new Error('down'),
    )

    await mountInApp(DashboardPage, {}, { route: '/admin' })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load dashboard data. Please try again.',
    })
  })
})

describe('admin accounts page', () => {
  const accounts = [
    adminAccount(1, {
      rulerName: 'Bob',
      nationName: 'Buyland',
      allianceName: 'Fark',
      role: 'B',
      slotsUsed: 4,
      slotsTotal: 4,
    }),
    adminAccount(2, {
      rulerName: 'Sue',
      nationName: 'Sellland',
      allianceName: 'Valyria',
      role: 'S',
      openFlagCount: 2,
    }),
  ]

  const stubAccounts = () => {
    const store = useAdminAccountsStore()
    return vi.spyOn(store, 'loadAllAccounts').mockImplementation(async () => {
      store.accounts = accounts.map((a) => ({ ...a }))
    })
  }

  it('lists accounts with their role, slots and flags', async () => {
    signIn('Super Admin')
    const load = stubAccounts()

    await mountInApp(AccountsPage, {}, { route: '/admin/accounts' })

    expect(load).toHaveBeenCalledWith(false)
    const rows = tableRows()
    expect(rows).toHaveLength(2)
    expect(rows[0]).toContain('BobBuyland')
    expect(rows[0]).toContain('Buyer')
    expect(rows[0]).toContain('4 / 4')
    expect(rows[1]).toContain('Seller')
    expect(rows[1]).toContain('2')
    // Role badges use the same colors as the role chip
    const roleBadge = (row: number, text: string) =>
      Array.from(
        document.querySelectorAll('tbody tr')[row].querySelectorAll('span'),
      ).find((span) => span.textContent?.trim() === text)!
    expect(roleBadge(0, 'Buyer').className).toContain('text-warning')
    expect(roleBadge(1, 'Seller').className).toContain('text-error')
    expect(bodyText()).not.toContain('Nation deleted')
  })

  it('marks accounts whose nation was deleted', async () => {
    signIn('Super Admin')
    const store = useAdminAccountsStore()
    vi.spyOn(store, 'loadAllAccounts').mockImplementation(async () => {
      store.accounts = [
        { ...accounts[0] },
        { ...accounts[1], role: 'H', nationDeleted: true },
      ]
    })

    await mountInApp(AccountsPage, {}, { route: '/admin/accounts' })

    const rows = tableRows()
    expect(rows[0]).not.toContain('Nation deleted')
    expect(rows[1]).toContain('Nation deleted')
  })

  it('filters by nation, ruler, alliance or role', async () => {
    signIn('Super Admin')
    stubAccounts()
    await mountInApp(AccountsPage, {}, { route: '/admin/accounts' })
    const search = inputByPlaceholder(
      'Search by nation, ruler, alliance, or role...',
    )

    for (const [query, expected] of [
      ['sell', 'Sue'],
      ['FARK', 'Bob'],
      ['seller', 'Sue'],
      ['bob', 'Bob'],
    ]) {
      await type(search, query)
      expect(tableRows()).toHaveLength(1)
      expect(tableRows()[0]).toContain(expected)
    }
    await type(search, '')
    expect(tableRows()).toHaveLength(2)
  })

  it('reloads with on-hold accounts when asked', async () => {
    signIn('Super Admin')
    const load = stubAccounts()
    const wrapper = await mountInApp(
      AccountsPage,
      {},
      { route: '/admin/accounts' },
    )

    await click(
      wrapper.findComponent(USwitch).find('button').element as HTMLElement,
    )

    expect(load).toHaveBeenLastCalledWith(true)
  })

  it('opens the account dialog to create or edit an account', async () => {
    signIn('Super Admin')
    stubAccounts()
    vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockResolvedValue([])
    await mountInApp(AccountsPage, {}, { route: '/admin/accounts' })

    await click(button('Create Account'))
    expect(bodyText()).toContain('New Account')
    await click(button('Cancel'))

    await click(buttons('Edit')[1])
    expect(bodyText()).toContain("Sue's Account")
  })

  it('opens the account linked from another page', async () => {
    signIn('Super Admin')
    stubAccounts()
    vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockResolvedValue([])

    await mountInApp(AccountsPage, {}, { route: '/admin/accounts?account=2' })

    expect(bodyText()).toContain("Sue's Account")
  })

  it('looks in on-hold accounts for a linked account', async () => {
    signIn('Super Admin')
    const store = useAdminAccountsStore()
    const load = vi
      .spyOn(store, 'loadAllAccounts')
      .mockImplementation(async (includeOnHold) => {
        store.accounts = includeOnHold
          ? accounts.map((a) => ({ ...a }))
          : [{ ...accounts[0] }]
      })
    vi.spyOn(store, 'loadAccountSlots').mockResolvedValue([])

    await mountInApp(AccountsPage, {}, { route: '/admin/accounts?account=2' })

    expect(load).toHaveBeenLastCalledWith(true)
    expect(bodyText()).toContain("Sue's Account")
  })

  it('warns when the linked account is missing', async () => {
    signIn('Super Admin')
    stubAccounts()

    await mountInApp(AccountsPage, {}, { route: '/admin/accounts?account=99' })

    expect(toasts().at(-1)).toMatchObject({
      color: 'warning',
      title: 'Account not found',
    })
  })

  it('narrows the table to a linked account for read-only admins', async () => {
    signIn('Admin (Read-only)')
    stubAccounts()

    await mountInApp(AccountsPage, {}, { route: '/admin/accounts?account=2' })

    expect(bodyText()).not.toContain("Sue's Account")
    expect(tableRows()).toHaveLength(1)
    expect(tableRows()[0]).toContain('Sue')
  })

  it('deletes an account', async () => {
    signIn('Super Admin')
    stubAccounts()
    const deleteAccount = vi
      .spyOn(useAdminAccountsStore(), 'deleteAccount')
      .mockResolvedValue()
    await mountInApp(AccountsPage, {}, { route: '/admin/accounts' })

    await click(buttons('Delete')[0])

    expect(deleteAccount).toHaveBeenCalledWith(1)
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Account deleted successfully.',
    })
  })

  it('shows an error when deleting fails', async () => {
    signIn('Super Admin')
    stubAccounts()
    vi.spyOn(useAdminAccountsStore(), 'deleteAccount').mockRejectedValue(
      new Error('down'),
    )
    await mountInApp(AccountsPage, {}, { route: '/admin/accounts' })

    await click(buttons('Delete')[0])

    expect(toasts()[0]).toMatchObject({ color: 'error' })
  })

  it('lets read-only admins look but not change anything', async () => {
    signIn('Admin (Read-only)')
    stubAccounts()

    await mountInApp(AccountsPage, {}, { route: '/admin/accounts' })

    expect(tableRows()).toHaveLength(2)
    expect(hasButton('Create Account')).toBe(false)
    expect(hasButton('Edit')).toBe(false)
    expect(hasButton('Delete')).toBe(false)
  })

  it.each([
    ['loading', 'Failed to load accounts. Please try again.', false],
    ['reloading', 'Failed to reload accounts. Please try again.', true],
  ])('shows an error when %s fails', async (_when, description, toggle) => {
    signIn('Super Admin')
    const load = stubAccounts()
    if (!toggle) load.mockRejectedValue(new Error('down'))
    const wrapper = await mountInApp(
      AccountsPage,
      {},
      { route: '/admin/accounts' },
    )
    if (toggle) {
      load.mockRejectedValue(new Error('down'))
      await click(
        wrapper.findComponent(USwitch).find('button').element as HTMLElement,
      )
    }

    expect(toasts().at(-1)).toMatchObject({ color: 'error', description })
  })
})

describe('admin aid activity page', () => {
  const aid = (
    aidId: number,
    overrides: Partial<AidActivityItem> = {},
  ): AidActivityItem => ({
    aidId,
    sentOn: '2026-10-01T12:00:00',
    status: 2,
    money: 9000000,
    technology: 0,
    soldiers: 0,
    reason: 'VEP',
    dealId: 10,
    sender: { ...accountSimplified(1), role: 'B' },
    receiver: { ...accountSimplified(2), role: 'S' },
    ...overrides,
  })

  const stubPage = (
    results: AidActivityItem[],
    totalCount = results.length,
  ) => {
    const store = useAdminAidActivityStore()
    return vi.spyOn(store, 'loadPage').mockImplementation(async () => {
      store.currentPage = { totalCount, results }
    })
  }

  it('loads the first page, describing each aid and whether it counted toward a deal', async () => {
    signIn('Super Admin')
    const load = stubPage([
      aid(1),
      aid(2, {
        status: 1,
        money: 0,
        technology: 50,
        dealId: null,
        sentOn: null,
        reason: '',
        receiver: {
          nationId: 300,
          nationName: null as never,
          rulerName: null as never,
          allianceName: '',
          accountId: null,
          role: null,
        },
      }),
      aid(3, { status: 3, sender: { ...accountSimplified(3), role: 'H' } }),
      aid(4, { status: 4 }),
      aid(5, { status: 9 }),
    ])

    await mountInApp(AidActivityPage, {}, { route: '/admin/aid-activity' })

    expect(load).toHaveBeenCalledWith(25, 0)
    const rows = tableRows()
    expect(rows[0]).toContain('2026-10-01')
    expect(rows[0]).toContain('$9,000,000')
    expect(rows[0]).toContain('Approved')
    expect(rows[0]).toContain('Deal #10')
    expect(rows[0]).toContain('Buyer')
    expect(rows[1]).toContain('—')
    expect(rows[1]).toContain('Pending')
    expect(rows[1]).toContain('No deal')
    expect(rows[1]).toContain('Nation #300')
    expect(rows[1]).toContain('Not in VEP')
    expect(rows[2]).toContain('Cancelled')
    expect(rows[2]).toContain('On Hold')
    expect(rows[3]).toContain('Expired')
    expect(rows[4]).toContain('Unknown')
    expect(bodyText()).toContain('Showing 1 to 5 of 5 results')
  })

  it('pages through the results', async () => {
    signIn('Super Admin')
    const load = stubPage([aid(1)], 60)
    const wrapper = await mountInApp(
      AidActivityPage,
      {},
      { route: '/admin/aid-activity' },
    )

    wrapper.findComponent(UPagination).vm.$emit('update:page', 3)
    await flushPromises()

    expect(load).toHaveBeenLastCalledWith(25, 50)
  })

  it('goes back to the first page when a toggle changes', async () => {
    signIn('Super Admin')
    const load = stubPage([aid(1)], 60)
    const wrapper = await mountInApp(
      AidActivityPage,
      {},
      { route: '/admin/aid-activity' },
    )

    await click(
      wrapper.findAllComponents(USwitch)[1].find('button')
        .element as HTMLElement,
    )
    expect(useAdminAidActivityStore().unmatchedOnly).toBe(true)
    expect(load).toHaveBeenLastCalledWith(25, 0)

    wrapper.findComponent(UPagination).vm.$emit('update:page', 2)
    await flushPromises()
    load.mockClear()
    await click(
      wrapper.findAllComponents(USwitch)[0].find('button')
        .element as HTMLElement,
    )

    expect(load).toHaveBeenCalledOnce()
    expect(load).toHaveBeenLastCalledWith(25, 0)
  })

  it('summarizes the active filters', async () => {
    signIn('Super Admin')
    stubPage([])
    const store = useAdminAidActivityStore()

    await mountInApp(AidActivityPage, {}, { route: '/admin/aid-activity' })
    for (const [filters, summary] of [
      [{ search: 'Bob', direction: 'sent' }, 'sent by "Bob"'],
      [{ search: 'Bob', direction: 'received' }, 'received by "Bob"'],
      [
        {
          search: 'Bob',
          sentSince: new Date(2026, 9, 1),
          sentUntil: new Date(2026, 9, 1),
        },
        `"Bob", ${new Date(2026, 9, 1).toLocaleDateString()}`,
      ],
      [
        { sentSince: new Date(2026, 9, 1), sentUntil: new Date(2026, 9, 3) },
        `${new Date(2026, 9, 1).toLocaleDateString()} to ${new Date(2026, 9, 3).toLocaleDateString()}`,
      ],
    ] as const) {
      store.filters = { ...filters }
      await flushPromises()
      expect(bodyText()).toContain(`Filtered: ${summary}`)
    }
  })

  it('reloads after the filters are applied', async () => {
    signIn('Super Admin')
    const load = stubPage([])
    await mountInApp(AidActivityPage, {}, { route: '/admin/aid-activity' })
    load.mockClear()

    await click(button('Filters'))
    await type(inputByPlaceholder('Enter name to search...'), 'Sue')
    await click(button('Apply Filters'))
    await flushPromises()

    expect(useAdminAidActivityStore().filters.search).toBe('Sue')
    expect(load).toHaveBeenCalledWith(25, 0)
  })

  it("shows an error when a page can't be loaded", async () => {
    signIn('Super Admin')
    vi.spyOn(useAdminAidActivityStore(), 'loadPage').mockRejectedValue(
      new Error('down'),
    )

    await mountInApp(AidActivityPage, {}, { route: '/admin/aid-activity' })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'The current page failed to load',
    })
  })
})

describe('admin deals page', () => {
  const stubDeals = () => {
    const store = useAdminDealsStore()
    return {
      deals: vi.spyOn(store, 'loadActiveDeals').mockImplementation(async () => {
        store.activeDeals = {
          totalCount: 40,
          results: [
            adminDeal({
              id: 10,
              responsibleAccountId: 1,
              daysResponsible: 2,
              cancelsOn: '2026-10-14T00:00:00',
              hasOpenFlag: true,
              lastActivityDate: '2026-10-05T00:00:00',
            }),
            adminDeal({ id: 11, state: DealActivityState.PendingStart }),
          ],
        }
      }),
      queue: vi.spyOn(store, 'loadWaitQueue').mockImplementation(async () => {
        store.waitQueue = [
          {
            accountId: 3,
            slotsNeeded: 2,
            enteredQueueOn: '2026-10-01T00:00:00',
            nextSlotOpensOn: null,
            priorityScore: 9.5,
            account: accountSimplified(3),
          },
        ]
      }),
    }
  }

  const showTab = async (
    wrapper: Awaited<ReturnType<typeof mountInApp>>,
    tab: string,
  ) => {
    wrapper.findComponent(UTabs).vm.$emit('update:modelValue', tab)
    await flushPromises()
  }

  it('loads active deals and the wait queue', async () => {
    signIn('Super Admin')
    const loads = stubDeals()

    await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    expect(loads.deals).toHaveBeenCalledWith('', 25, 0)
    expect(loads.queue).toHaveBeenCalled()
    expect(bodyText()).toContain('Active Deals (40)')
    expect(bodyText()).toContain('Wait Queue (1)')
    expect(bodyText()).not.toContain('Flags (')
    const rows = tableRows()
    expect(rows[0]).toContain('#10')
    expect(rows[0]).toContain('A sends $9M')
    expect(rows[0]).toContain('B sends 100 tech')
    expect(rows[0]).toContain('2d · cancels 2026-10-14')
    expect(rows[0]).toContain('Awaiting Send')
    expect(rows[0]).toContain('Flagged')
    expect(rows[1]).toContain('Starting')
    expect(bodyText()).toContain('Showing 1 to 25 of 40 results')
  })

  it('marks deals on a team bonus slot as temporary', async () => {
    signIn('Super Admin')
    const store = useAdminDealsStore()
    vi.spyOn(store, 'loadActiveDeals').mockImplementation(async () => {
      store.activeDeals = {
        totalCount: 2,
        results: [
          adminDeal({
            id: 10,
            slot2: teamBonusSlot(2, '2026-11-05T00:00:00', { tech: 1 }),
            teamBonusEndsOn: '2026-11-05T00:00:00',
          }),
          adminDeal({ id: 11 }),
        ],
      }
    })
    vi.spyOn(store, 'loadWaitQueue').mockResolvedValue()

    await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    expect(tableRows()[0]).toContain('Slot #7 (team bonus)')
    expect(tableRows()[0]).toContain('Temporary, ends 11/5/2026')
    expect(tableRows()[1]).not.toContain('Temporary')
  })

  it('links a flagged deal to its flags, and the page to the flags page', async () => {
    signIn('Super Admin')
    stubDeals()
    await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    const links = Array.from(document.querySelectorAll('a')).map((a) =>
      a.getAttribute('href'),
    )
    expect(links).toContain('/admin/flags?search=%2310')
    expect(links).toContain('/admin/flags')
  })

  it('shows the wait queue on its tab', async () => {
    signIn('Super Admin')
    stubDeals()
    const wrapper = await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    await showTab(wrapper, 'queue')
    expect(tableRows()[0]).toContain('Ruler3')
    expect(tableRows()[0]).toContain('9.50')
  })

  it('pages through active deals', async () => {
    signIn('Super Admin')
    const loads = stubDeals()
    const wrapper = await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    wrapper.findComponent(UPagination).vm.$emit('update:page', 2)
    await flushPromises()

    expect(loads.deals).toHaveBeenLastCalledWith('', 25, 25)
  })

  it('searches after typing stops, from the first page', async () => {
    signIn('Super Admin')
    const loads = stubDeals()
    const wrapper = await mountInApp(DealsPage, {}, { route: '/admin/deals' })
    const waitForDebounce = () =>
      new Promise((resolve) => setTimeout(resolve, 350))

    await type(
      inputByPlaceholder('Search by nation, ruler, or alliance...'),
      ' bob ',
    )
    await waitForDebounce()
    await flushPromises()
    expect(loads.deals).toHaveBeenLastCalledWith('bob', 25, 0)

    wrapper.findComponent(UPagination).vm.$emit('update:page', 2)
    await flushPromises()
    loads.deals.mockClear()
    await type(
      inputByPlaceholder('Search by nation, ruler, or alliance...'),
      'sue',
    )
    await waitForDebounce()
    await flushPromises()
    expect(loads.deals).toHaveBeenCalledOnce()
    expect(loads.deals).toHaveBeenLastCalledWith('sue', 25, 0)
  })

  it('cancels a deal, then reloads everything', async () => {
    signIn('Super Admin')
    const loads = stubDeals()
    vi.spyOn(useAdminDealsStore(), 'cancelDeal').mockResolvedValue()
    await mountInApp(DealsPage, {}, { route: '/admin/deals' })
    Object.values(loads).forEach((load) => load.mockClear())

    await click(buttons('Cancel')[0])
    expect(bodyText()).toContain('Cancel Deal #10')
    await type(
      inputByPlaceholder('Why is this deal being cancelled?'),
      'Partner left',
    )
    await click(button('Cancel Deal'))
    await flushPromises()

    expect(loads.deals).toHaveBeenCalledWith('', 25, 0)
    expect(loads.queue).toHaveBeenCalled()
  })

  it("doesn't reload when a cancel is abandoned", async () => {
    signIn('Super Admin')
    const loads = stubDeals()
    await mountInApp(DealsPage, {}, { route: '/admin/deals' })
    loads.deals.mockClear()

    await click(buttons('Cancel')[0])
    await click(button('Keep Deal'))
    await flushPromises()

    expect(loads.deals).not.toHaveBeenCalled()
  })

  it('loads accounts to create a deal from, then reloads after creating one', async () => {
    signIn('Super Admin')
    const loads = stubDeals()
    const accountsStore = useAdminAccountsStore()
    const loadAccounts = vi
      .spyOn(accountsStore, 'loadAllAccounts')
      .mockImplementation(async () => {
        accountsStore.accounts = [adminAccount(1), adminAccount(2)]
      })
    await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    await click(button('Create Deal'))

    expect(loadAccounts).toHaveBeenCalled()
    expect(bodyText()).toContain('Pair an open slot from one account')
    await click(button('Cancel', document.querySelector('[role="dialog"]')!))
    await flushPromises()
    loads.deals.mockClear()

    // Accounts are only loaded once
    await click(button('Create Deal'))
    expect(loadAccounts).toHaveBeenCalledOnce()
  })

  it("shows an error instead of the dialog when accounts can't be loaded", async () => {
    signIn('Super Admin')
    stubDeals()
    vi.spyOn(useAdminAccountsStore(), 'loadAllAccounts').mockRejectedValue(
      new Error('down'),
    )
    await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    await click(button('Create Deal'))

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load accounts. Please try again.',
    })
    expect(bodyText()).not.toContain('Pair an open slot from one account')
  })

  it('lets read-only admins look but not change anything', async () => {
    signIn('Admin (Read-only)')
    stubDeals()
    await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    expect(hasButton('Create Deal')).toBe(false)
    expect(hasButton('Cancel')).toBe(false)
  })

  it.each([
    ['deals', 'Failed to load active deals. Please try again.'],
    ['queue', 'Failed to load the wait queue. Please try again.'],
  ] as const)(
    "shows an error when the %s can't be loaded",
    async (which, description) => {
      signIn('Super Admin')
      stubDeals()[which].mockRejectedValue(new Error('down'))

      await mountInApp(DealsPage, {}, { route: '/admin/deals' })

      expect(toasts()[0]).toMatchObject({ color: 'error', description })
    },
  )
})

describe('admin flags page', () => {
  // Ruler1 is holding up active deal 10; Ruler3's deal 11 was cancelled; deal 12's offer failed, flagging Ruler5 and Ruler6
  const stubFlags = (
    flags = [
      cancelledDealFlag({
        id: 6,
        dealId: 11,
        flaggedOn: '2026-10-08T18:00:00',
        flaggedAccount: accountSimplified(3),
        innocentAccount: accountSimplified(4, { allianceName: 'Rogues' }),
      }),
      dealFlag({ id: 5 }),
      cancelledDealFlag({
        id: 7,
        dealId: 12,
        flagType: DealFlagType.AidFailed,
        flagReason: DealFlagReason.AidExpired,
        flaggedAccount: accountSimplified(5),
        innocentAccount: accountSimplified(6),
      }),
      cancelledDealFlag({
        id: 8,
        dealId: 12,
        flagType: DealFlagType.AidFailed,
        flagReason: DealFlagReason.AidExpired,
        flaggedAccount: accountSimplified(6),
        innocentAccount: accountSimplified(5),
      }),
    ],
  ) => {
    const store = useAdminDealsStore()
    return vi.spyOn(store, 'loadFlags').mockImplementation(async () => {
      store.flags = flags
    })
  }

  const cards = () =>
    Array.from(document.querySelectorAll<HTMLElement>('[data-flag-id]'))
  const cardIds = () => cards().map((card) => Number(card.dataset.flagId))
  const card = (id: number) =>
    document.querySelector<HTMLElement>(`[data-flag-id="${id}"]`)!
  const dialog = () => document.querySelector<HTMLElement>('[role="dialog"]')!

  it('loads the flags, most urgent first, explaining each one', async () => {
    signIn('Super Admin')
    const loadFlags = stubFlags()

    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    expect(loadFlags).toHaveBeenCalled()
    expect(bodyText()).toContain('4 open flags across 3 deals')
    expect(bodyText()).toContain('1 still held up')
    expect(bodyText()).toContain('All (4)')
    expect(bodyText()).toContain('Active Deals (1)')
    expect(bodyText()).toContain('Cancelled Deals (3)')
    expect(cardIds()).toEqual([5, 7, 8, 6])

    // Collapsed rows just summarize each flag
    const row = card(5).textContent
    expect(row).toContain('1-Day Warning')
    expect(row).toContain('Ruler1')
    expect(row).toContain('Ruler2')
    expect(row).toContain('#10')
    expect(row).toContain('Active, still held up')
    expect(row).toContain('Deal cancelled')
    expect(row).not.toContain('What happened')
    expect(buttons('Resolve...')).toHaveLength(4)

    await click(button('Expand all'))

    const warning = card(5).textContent
    expect(warning).toContain(
      "It was Ruler1's turn to send $9M to Ruler2, and both nations had a free aid slot.",
    )
    expect(warning).toContain("Ruler1 didn't send it.")
    expect(warning).toContain(
      'If Ruler1 is still holding up the deal at the daily check on 2026-10-13, VEP cancels it',
    )
    const cancelled = card(6).textContent
    expect(cancelled).toContain('5-Day Cancellation')
    expect(cancelled).toContain('Cancelled 2026-10-09')
    expect(cancelled).toContain('Stays open until an admin resolves it')
    expect(cancelled).toContain(
      'Expected aid not sent for 5+ days - automatically cancelled',
    )
  })

  it('filters by search and by whether the deal is still active', async () => {
    signIn('Super Admin')
    stubFlags()
    const wrapper = await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    await type(
      inputByPlaceholder(
        'Search by nation, ruler, alliance, role, or deal #...',
      ),
      'rogues',
    )
    expect(cardIds()).toEqual([6])
    expect(bodyText()).toContain('All (1)')

    await type(
      inputByPlaceholder(
        'Search by nation, ruler, alliance, role, or deal #...',
      ),
      '',
    )
    wrapper.findComponent(UTabs).vm.$emit('update:modelValue', 'active')
    await flushPromises()
    expect(cardIds()).toEqual([5])
  })

  it('starts from the search in the link', async () => {
    signIn('Super Admin')
    stubFlags()

    await mountInApp(FlagsPage, {}, { route: '/admin/flags?search=%2312' })

    expect(cardIds()).toEqual([7, 8])
  })

  it('shows the aid behind a flag on request', async () => {
    signIn('Super Admin')
    stubFlags()
    const store = useAdminDealsStore()
    const loadEvidence = vi
      .spyOn(store, 'loadFlagEvidence')
      .mockImplementation(async (flagId) => {
        store.flagEvidence[flagId] = {
          legs: [
            {
              legNumber: 1,
              aidId: 1,
              activityDate: '2026-09-28T00:00:00',
              sentByFlaggedAccount: true,
              money: 9_000_000,
              technology: 0,
              outcome: DealLegOutcome.Accepted,
              failureExcused: false,
            },
          ],
          slotsAsOf: '2026-10-10T00:00:00',
          flaggedSlotsTotal: 4,
          flaggedSlotAid: [
            {
              aidId: 2,
              sentOn: '2026-10-08T12:00:00',
              isOutgoing: true,
              status: 2,
              money: 3_000_000,
              technology: 0,
              soldiers: 0,
              reason: 'War aid',
              dealId: null,
              sentBeforeDeal: false,
              otherNation: {
                nationId: 500,
                nationName: 'Farland',
                rulerName: 'Faraway',
                allianceName: 'Other',
              },
            },
          ],
        }
      })
    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    await click(
      card(5).querySelector<HTMLElement>('[aria-label="Show details"]')!,
    )
    await click(button('Show aid details', card(5)))

    expect(loadEvidence).toHaveBeenCalledWith(5)
    const text = card(5).textContent
    expect(text).toContain('Ruler1 → Ruler2')
    expect(text).toContain('Accepted')
    expect(text).toContain("Ruler1's aid slots right now")
    expect(text).toContain(
      '1 of 4 in use, 1 outside VEP since this deal started',
    )
    expect(text).toContain('Sent to Faraway (Farland)')
    expect(text).toContain('$3M')
    expect(text).toContain('Outside VEP')
    expect(text).toContain('"War aid"')
  })

  it('links each party to its admin account and its CN nation', async () => {
    signIn('Super Admin')
    stubFlags()
    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    await click(
      card(5).querySelector<HTMLElement>('[aria-label="Show details"]')!,
    )

    const hrefs = Array.from(card(5).querySelectorAll('a')).map((a) =>
      a.getAttribute('href'),
    )
    // Ruler1 is account 1, nation 101
    expect(hrefs).toContain('/admin/accounts?account=1')
    expect(hrefs).toContain(
      'https://www.cybernations.net/nation_drill_display.asp?Nation_ID=101',
    )
  })

  it('expands one row at a time, or all of them, remembering the choice', async () => {
    signIn('Super Admin')
    stubFlags()
    localStorage.removeItem('vep.adminFlags.expandAll')
    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })
    const expanded = () =>
      cardIds().filter((id) => card(id).textContent?.includes('What happened'))
    const toggle = (id: number, label: string) =>
      click(card(id).querySelector<HTMLElement>(`[aria-label="${label}"]`)!)

    // Clicking anywhere on a row opens it; the chevron closes it again
    await click(card(6).firstElementChild as HTMLElement)
    expect(expanded()).toEqual([6])
    await toggle(6, 'Hide details')
    expect(expanded()).toEqual([])

    await click(button('Expand all'))
    expect(expanded()).toEqual([5, 7, 8, 6])
    expect(localStorage.getItem('vep.adminFlags.expandAll')).toBe('true')
    await toggle(7, 'Hide details')
    expect(expanded()).toEqual([5, 8, 6])

    await click(button('Collapse all'))
    expect(expanded()).toEqual([])
    expect(localStorage.getItem('vep.adminFlags.expandAll')).toBe('false')
  })

  it('opens every row when the admin last chose to expand them all', async () => {
    signIn('Super Admin')
    stubFlags()
    localStorage.setItem('vep.adminFlags.expandAll', 'true')

    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    expect(card(6).textContent).toContain('What happened')
    expect(hasButton('Collapse all')).toBe(true)
    localStorage.removeItem('vep.adminFlags.expandAll')
  })

  it('clears a warning, then reloads the flags', async () => {
    signIn('Super Admin')
    const loadFlags = stubFlags()
    const resolveFlag = vi
      .spyOn(useAdminDealsStore(), 'resolveFlag')
      .mockResolvedValue()
    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })
    loadFlags.mockClear()

    await click(button('Resolve...', card(5)))
    expect(dialog().textContent).toContain('Cancel the deal now')
    expect(dialog().textContent).toContain('Give Ruler1 5 more days')
    await click(dialog().querySelectorAll<HTMLElement>('[role="radio"]')[2])
    await click(button('Clear Flag', dialog()))

    expect(resolveFlag).toHaveBeenCalledWith(5, '')
    expect(loadFlags).toHaveBeenCalled()
  })

  it("restores a deal, passing along the partner's flag for failed aid", async () => {
    signIn('Super Admin')
    stubFlags()
    const restore = vi
      .spyOn(useAdminDealsStore(), 'restoreFlaggedDeal')
      .mockResolvedValue()
    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    await click(button('Resolve...', card(7)))
    expect(dialog().textContent).toContain(
      "Clear both accounts' flags, keep the deal cancelled",
    )
    await click(dialog().querySelectorAll<HTMLElement>('[role="radio"]')[0])
    await click(button('Restore Deal', dialog()))

    expect(restore).toHaveBeenCalledWith(7, '')
  })

  it('lets read-only admins look but not resolve', async () => {
    signIn('Admin (Read-only)')
    stubFlags()

    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    expect(cards()).toHaveLength(4)
    expect(hasButton('Resolve...')).toBe(false)
  })

  it('says when there is nothing flagged', async () => {
    signIn('Super Admin')
    stubFlags([])

    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    expect(bodyText()).toContain('No open flags. Every account is clear.')
  })

  it("shows an error when the flags can't be loaded", async () => {
    signIn('Super Admin')
    stubFlags().mockRejectedValue(new Error('down'))

    await mountInApp(FlagsPage, {}, { route: '/admin/flags' })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load flagged deals. Please try again.',
    })
  })
})

describe('admin messages page', () => {
  const recipients = (count: number) =>
    Array.from({ length: count }, (_, i) =>
      recipient(i + 1, { sendCount: 1, discord: i === 0 ? 'ruler1#1' : null }),
    )

  const stubMessages = (
    messages = [
      massMessage(1, { name: 'Send Aid', recipients: recipients(27) }),
      massMessage(2, {
        name: 'Final Warning',
        code: MassMessageCode.FinalWarning,
        recipients: [
          recipient(40, {
            flaggedCount: 1,
            daysOverdue: 4,
            cancelsOn: '2026-10-10T00:00:00',
          }),
        ],
      }),
      massMessage(3, {
        name: 'Accept Aid',
        code: MassMessageCode.AcceptAid,
        recipients: [],
      }),
    ],
  ) => {
    const store = useAdminMassMessagesStore()
    return vi.spyOn(store, 'loadMassMessages').mockImplementation(async () => {
      store.summary = {
        cnDate: '2026-10-09T00:00:00',
        dealStatesUpdatedOn: '2026-10-09T13:45:00',
        messages,
      }
    })
  }

  const showTab = async (
    wrapper: Awaited<ReturnType<typeof mountInApp>>,
    id: number,
  ) => {
    wrapper.findComponent(UTabs).vm.$emit('update:modelValue', String(id))
    await flushPromises()
  }

  it('shows the first message, filled in for today, addressed to the first batch', async () => {
    signIn('Super Admin')
    stubMessages()

    await mountInApp(MessagesPage, {}, { route: '/admin/messages' })

    expect(bodyText()).toContain('Send Aid (27)')
    expect(bodyText()).toContain('Final Warning (1)')
    expect(bodyText()).toContain('Who gets message 1')
    expect(bodyText()).toMatch(
      /Deal Activity Updated On: Oct 09, 2026 \d\d:\d\d/,
    )
    expect(bodyText()).toMatch(/First Recipient\s*Ruler1\s*Nation1/)
    expect(bodyText()).toContain('Carbon Copy Rulers (24)')
    expect(
      button(
        'Send Message',
        document.querySelector('.bg-gray-50')!,
      ).getAttribute('href'),
    ).toBe('https://www.cybernations.net/send_message.asp?Nation_ID=101')
    const values = Array.from(
      document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>(
        'input[readonly], textarea[readonly]',
      ),
    ).map((el) => el.value)
    expect(values).toContain('VEP Oct 09')
    expect(values).toContain(`Log in at ${useRequestURL().origin} on Oct 09`)
    expect(
      values.find((v) => v.startsWith('Ruler2'))!.split('\r\n'),
    ).toHaveLength(24)
    expect(bodyText()).toContain('Batch 1 of 2')
    expect(tableRows()).toHaveLength(27)
    expect(tableRows()[0]).toContain('1 to send')
    expect(tableRows()[0]).toContain('ruler1#1')
  })

  it('moves between batches', async () => {
    signIn('Super Admin')
    stubMessages()
    await mountInApp(MessagesPage, {}, { route: '/admin/messages' })
    const [previous, next] = Array.from(
      document.querySelectorAll<HTMLButtonElement>('.border-t button'),
    )

    expect(previous.disabled).toBe(true)
    await click(next)

    expect(bodyText()).toContain('Batch 2 of 2')
    expect(bodyText()).toMatch(/First Recipient\s*Ruler26/)
    expect(bodyText()).toContain('Carbon Copy Rulers (1)')
    expect(next.disabled).toBe(true)
    await click(previous)
    expect(bodyText()).toContain('Batch 1 of 2')
  })

  it('shows overdue details for flagged messages, starting at their first batch', async () => {
    signIn('Super Admin')
    stubMessages()
    const wrapper = await mountInApp(
      MessagesPage,
      {},
      { route: '/admin/messages' },
    )
    await click(
      Array.from(
        document.querySelectorAll<HTMLButtonElement>('.border-t button'),
      )[1],
    )

    await showTab(wrapper, 2)

    expect(bodyText()).toContain('Days Overdue')
    expect(tableRows()[0]).toContain('4')
    expect(tableRows()[0]).toContain('2026-10-10')
    expect(bodyText()).not.toContain('Carbon Copy Rulers')
    expect(bodyText()).not.toContain('Batch')
  })

  it('says when no one needs a message today', async () => {
    signIn('Super Admin')
    stubMessages()
    const wrapper = await mountInApp(
      MessagesPage,
      {},
      { route: '/admin/messages' },
    )

    await showTab(wrapper, 3)

    expect(bodyText()).toContain('No one needs this message today.')
  })

  it('marks a message sent', async () => {
    signIn('Admin - Lists')
    stubMessages()
    const setSent = vi
      .spyOn(useAdminMassMessagesStore(), 'setMessageSent')
      .mockResolvedValue()
    const wrapper = await mountInApp(
      MessagesPage,
      {},
      { route: '/admin/messages' },
    )

    await click(
      wrapper.findComponent(USwitch).find('button').element as HTMLElement,
    )

    expect(setSent).toHaveBeenCalledWith(1, true)
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Messages marked as sent.',
    })
  })

  it('shows who sent a message, and can mark it not sent', async () => {
    signIn('Super Admin')
    stubMessages([
      massMessage(1, {
        sentOn: '2026-10-09T15:30:00',
        sentBy: accountSimplified(9, { rulerName: 'Ada' }),
        recipients: recipients(1),
      }),
    ])
    const setSent = vi
      .spyOn(useAdminMassMessagesStore(), 'setMessageSent')
      .mockResolvedValue()
    const wrapper = await mountInApp(
      MessagesPage,
      {},
      { route: '/admin/messages' },
    )

    expect(bodyText()).toMatch(/Sent by Ada on Oct 09, 2026 \d\d:\d\d/)
    await click(
      wrapper.findComponent(USwitch).find('button').element as HTMLElement,
    )

    expect(setSent).toHaveBeenCalledWith(1, false)
    expect(toasts()[0]).toMatchObject({
      description: 'Messages marked as not sent.',
    })
  })

  it('shows an error when marking a message fails', async () => {
    signIn('Super Admin')
    stubMessages()
    vi.spyOn(useAdminMassMessagesStore(), 'setMessageSent').mockRejectedValue(
      new Error('down'),
    )
    const wrapper = await mountInApp(
      MessagesPage,
      {},
      { route: '/admin/messages' },
    )

    await click(
      wrapper.findComponent(USwitch).find('button').element as HTMLElement,
    )

    expect(toasts()[0]).toMatchObject({ color: 'error' })
  })

  it("doesn't let read-only admins mark messages sent", async () => {
    signIn('Admin (Read-only)')
    stubMessages()
    const wrapper = await mountInApp(
      MessagesPage,
      {},
      { route: '/admin/messages' },
    )

    expect(wrapper.findComponent(USwitch).props('disabled')).toBe(true)
  })

  it('copies the subject, message and carbon copy list', async () => {
    signIn('Super Admin')
    stubMessages()
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText },
      configurable: true,
    })
    await mountInApp(MessagesPage, {}, { route: '/admin/messages' })

    for (const field of Array.from(
      document.querySelectorAll<HTMLElement>(
        'input[readonly], textarea[readonly]',
      ),
    )) {
      await click(field)
    }

    expect(writeText).toHaveBeenCalledWith('VEP Oct 09')
    expect(writeText).toHaveBeenCalledTimes(3)
    expect(toasts().map((t) => t.description)).toEqual(
      expect.arrayContaining([
        'Subject copied to clipboard.',
        'Message copied to clipboard.',
        'Carbon Copy Rulers copied to clipboard.',
      ]),
    )
  })

  it('says when copying fails', async () => {
    signIn('Super Admin')
    stubMessages()
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
      configurable: true,
    })
    await mountInApp(MessagesPage, {}, { route: '/admin/messages' })

    await click(document.querySelector<HTMLElement>('input[readonly]')!)

    expect(toasts()[0]).toMatchObject({ title: 'Copy Failed', color: 'error' })
  })

  it("shows an error when the messages can't be loaded", async () => {
    signIn('Super Admin')
    vi.spyOn(useAdminMassMessagesStore(), 'loadMassMessages').mockRejectedValue(
      new Error('down'),
    )

    await mountInApp(MessagesPage, {}, { route: '/admin/messages' })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load mass messages. Please try again.',
    })
  })
})

describe('admin settings page', () => {
  const admins = [
    {
      accountId: 1,
      rulerName: 'Ada',
      nationName: 'Adaland',
      vepRole: 'B',
      adminRoleId: 1,
      adminRoleName: 'Super Admin',
    },
    {
      accountId: 2,
      rulerName: 'Lee',
      nationName: 'Leeland',
      vepRole: 'S',
      adminRoleId: 3,
      adminRoleName: 'Admin - Lists',
    },
  ]

  const today = cnToday()
  const teamEvents = [
    teamEvent({
      id: 1,
      team: Team.Red,
      approvedOn: addDays(today, -40),
      endsOn: addDays(today, -10),
      isActive: false,
      accountsWithBonusSlot: 0,
      activeBonusDeals: 0,
    }),
    teamEvent({
      id: 2,
      team: Team.Aqua,
      approvedOn: addDays(today, -26),
      endsOn: addDays(today, 4),
    }),
  ]

  const stubTeamEvents = (events = teamEvents) => {
    const store = useAdminTeamEventsStore()
    return vi.spyOn(store, 'loadTeamEvents').mockImplementation(async () => {
      store.teamEvents = events
    })
  }

  const stubAdmins = () => {
    stubTeamEvents()
    const store = useAdminAccessStore()
    return vi
      .spyOn(store, 'loadAdminAccessSettings')
      .mockImplementation(async () => {
        store.adminAccessSettings = admins
      })
  }

  const adminRows = () =>
    Array.from(
      document.querySelectorAll('table')[0].querySelectorAll('tbody tr'),
    ).map((r) => r.textContent ?? '')
  const teamRows = () =>
    Array.from(
      document.querySelectorAll('table')[1].querySelectorAll('tbody tr'),
    ).map((r) => r.textContent ?? '')

  it('lists admins, filtered by name or role', async () => {
    signIn('Super Admin')
    stubAdmins()
    await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

    expect(adminRows()).toHaveLength(2)
    expect(adminRows()[0]).toContain('AdaAdalandSuper Admin')
    await type(inputByPlaceholder('Search by account name or role...'), 'lists')
    expect(adminRows()).toHaveLength(1)
    expect(adminRows()[0]).toContain('Lee')
  })

  it('removes an admin role', async () => {
    signIn('Super Admin')
    stubAdmins()
    const deleteAccess = vi
      .spyOn(useAdminAccessStore(), 'deleteAdminAccess')
      .mockResolvedValue(true)
    await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

    await click(buttons('Remove')[1])

    expect(deleteAccess).toHaveBeenCalledWith({ accountId: 2, adminRoleId: 3 })
    expect(toasts()[0]).toMatchObject({
      color: 'success',
      description: 'Admin access removed successfully.',
    })
  })

  it('shows an error when removing fails', async () => {
    signIn('Super Admin')
    stubAdmins()
    vi.spyOn(useAdminAccessStore(), 'deleteAdminAccess').mockResolvedValue(
      false,
    )
    await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

    await click(buttons('Remove')[0])

    expect(toasts()[0]).toMatchObject({ color: 'error' })
  })

  it('opens the dialog to add an admin', async () => {
    signIn('Super Admin')
    stubAdmins()
    useAdminAccountsStore().accounts = [adminAccount(1)]
    await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

    await click(button('Add Admin'))

    expect(bodyText()).toContain('Add Admin Access')
  })

  it("shows an error when admins can't be loaded", async () => {
    signIn('Super Admin')
    stubTeamEvents()
    vi.spyOn(
      useAdminAccessStore(),
      'loadAdminAccessSettings',
    ).mockRejectedValue(new Error('down'))

    await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load admin access settings.',
    })
  })

  describe('team events', () => {
    it('lists events in effect first, with when they end and who has the bonus', async () => {
      signIn('Super Admin')
      stubAdmins()
      await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

      expect(teamRows()).toHaveLength(2)
      expect(teamRows()[0]).toContain('Aqua')
      expect(teamRows()[0]).toContain(formatCnDate(addDays(today, 4)))
      expect(teamRows()[0]).toContain('Active, 5 days left')
      expect(teamRows()[0]).toContain('12 accounts, 3 in deals')
      expect(teamRows()[0]).toContain('lilweirdward')
      expect(teamRows()[1]).toContain('Red')
      expect(teamRows()[1]).toContain('Expired')

      await type(inputByPlaceholder('Search by team name...'), 'red')
      expect(teamRows()).toHaveLength(1)
      expect(teamRows()[0]).toContain('Red')
    })

    it('deletes an event only once the deletion is confirmed', async () => {
      signIn('Super Admin')
      stubAdmins()
      const deleteTeamEvent = vi
        .spyOn(useAdminTeamEventsStore(), 'deleteTeamEvent')
        .mockResolvedValue()
      await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

      await click(buttons('Delete')[0])
      expect(teamRows()[0]).toContain('Cancels 3 deals.')
      await click(button('Keep'))
      expect(deleteTeamEvent).not.toHaveBeenCalled()

      await click(buttons('Delete')[0])
      await click(button('Confirm Delete'))

      expect(deleteTeamEvent).toHaveBeenCalledWith(2)
      expect(toasts()[0]).toMatchObject({
        color: 'success',
        description: 'Team event deleted, and its bonus deals cancelled.',
      })
    })

    it('shows an error when deleting fails', async () => {
      signIn('Super Admin')
      stubAdmins()
      vi.spyOn(useAdminTeamEventsStore(), 'deleteTeamEvent').mockRejectedValue(
        new Error('down'),
      )
      await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

      await click(buttons('Delete')[1])
      await click(button('Confirm Delete'))

      expect(toasts()[0]).toMatchObject({ color: 'error' })
    })

    it('opens the dialog to add an event', async () => {
      signIn('Super Admin')
      stubAdmins()
      await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

      await click(button('Add Team Event'))

      expect(bodyText()).toContain('Add +1 Aid Slot Team Event')
    })

    it("shows an error when events can't be loaded", async () => {
      signIn('Super Admin')
      stubAdmins()
      vi.spyOn(useAdminTeamEventsStore(), 'loadTeamEvents').mockRejectedValue(
        new Error('down'),
      )

      await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

      expect(toasts()[0]).toMatchObject({
        color: 'error',
        description: 'Failed to load team events.',
      })
    })
  })
})
