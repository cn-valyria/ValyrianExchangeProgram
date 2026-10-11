import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { UPagination, USwitch, UTabs } from '#components'
import AccountsPage from '~/pages/admin/accounts.vue'
import AidActivityPage from '~/pages/admin/aid-activity.vue'
import DealsPage from '~/pages/admin/deals.vue'
import DashboardPage from '~/pages/admin/index.vue'
import MessagesPage from '~/pages/admin/messages.vue'
import SettingsPage from '~/pages/admin/settings.vue'
import { DealActivityState } from '~/stores/account'
import type { AidActivityItem } from '~/stores/adminAidActivity'
import { DealFlagReason, DealFlagType } from '~/stores/adminDeals'
import { MassMessageCode } from '~/stores/adminMassMessages'
import {
  accountSimplified,
  adminAccount,
  adminDeal,
  dealFlag,
  massMessage,
  recipient,
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
        generalProgramSummary: { totalAccounts: 120, activeDeals: 230 },
        aidRatioSummary: {
          totalIncomingCashSlots: 1,
          totalOutgoingCashSlots: 1,
          totalIncomingTechSlots: 1,
          totalOutgoingTechSlots: 1,
        },
        dealHealthSummary: {
          accountsWaiting: 3,
          slotsWaiting: 7,
          openFlags: 0,
        },
        slotUsageSummary: { maxCapacity: 400, currentSlotsUsed: 300 },
        dealAidPerDay: [],
        accountsPerRole: [],
        totalSlotsPerAlliance: [],
        openSlotsPerRole: [],
        accountsPerAlliance: [{ allianceName: 'Fark', totalAccounts: 9 }],
      }
    })

    await mountInApp(DashboardPage, {}, { route: '/admin' })

    expect(bodyText()).toContain('VEP Dashboard')
    expect(bodyText()).toContain('230')
    expect(bodyText()).toContain('75.00%')
    expect(bodyText()).toContain('Fark')
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
      flags: vi.spyOn(store, 'loadFlags').mockImplementation(async () => {
        store.flags = [
          dealFlag({ id: 5 }),
          dealFlag({
            id: 6,
            flagType: DealFlagType.FiveDayReplacement,
            flagReason: DealFlagReason.OutOfSequenceAid,
          }),
          dealFlag({
            id: 7,
            flagType: DealFlagType.AidFailed,
            flagReason: null,
          }),
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

  it('loads active deals, the wait queue and flags', async () => {
    signIn('Super Admin')
    const loads = stubDeals()

    await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    expect(loads.deals).toHaveBeenCalledWith('', 25, 0)
    expect(loads.queue).toHaveBeenCalled()
    expect(loads.flags).toHaveBeenCalled()
    expect(bodyText()).toContain('Active Deals (40)')
    expect(bodyText()).toContain('Wait Queue (1)')
    expect(bodyText()).toContain('Flags (3)')
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

  it('shows the wait queue and flags on their tabs', async () => {
    signIn('Super Admin')
    stubDeals()
    const wrapper = await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    await showTab(wrapper, 'queue')
    expect(tableRows()[0]).toContain('Ruler3')
    expect(tableRows()[0]).toContain('9.50')

    await showTab(wrapper, 'flags')
    const rows = tableRows()
    expect(rows[0]).toContain('1-Day Inactive')
    expect(rows[0]).toContain("Didn't send on their turn")
    expect(rows[1]).toContain('5-Day Replacement')
    expect(rows[1]).toContain('Sent aid out of turn')
    expect(rows[2]).toContain('Aid Failed')
    expect(rows[2]).toContain('—')
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
    expect(loads.flags).toHaveBeenCalled()
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

  it('resolves a flag', async () => {
    signIn('Super Admin')
    stubDeals()
    const resolveFlag = vi
      .spyOn(useAdminDealsStore(), 'resolveFlag')
      .mockResolvedValue()
    const wrapper = await mountInApp(DealsPage, {}, { route: '/admin/deals' })
    await showTab(wrapper, 'flags')

    await click(buttons('Resolve')[0])
    await click(button('Resolve', document.querySelector('[role="dialog"]')!))

    expect(resolveFlag).toHaveBeenCalledWith(5, '')
  })

  it('lets read-only admins look but not change anything', async () => {
    signIn('Admin (Read-only)')
    stubDeals()
    const wrapper = await mountInApp(DealsPage, {}, { route: '/admin/deals' })

    expect(hasButton('Create Deal')).toBe(false)
    expect(hasButton('Cancel')).toBe(false)
    await showTab(wrapper, 'flags')
    expect(hasButton('Resolve')).toBe(false)
  })

  it.each([
    ['deals', 'Failed to load active deals. Please try again.'],
    ['queue', 'Failed to load the wait queue. Please try again.'],
    ['flags', 'Failed to load flagged deals. Please try again.'],
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

  const stubAdmins = () => {
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

  describe('team events (placeholder data until the API exists)', () => {
    it('lists, filters and deletes team events', async () => {
      signIn('Super Admin')
      stubAdmins()
      await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

      expect(teamRows()).toHaveLength(3)
      expect(teamRows()[0]).toContain('+1 Aid Slot')
      await type(inputByPlaceholder('Search by team name...'), 'red')
      expect(teamRows()).toHaveLength(1)
      await type(inputByPlaceholder('Search by team name...'), '')

      await click(buttons('Delete')[0])
      expect(teamRows()).toHaveLength(2)
      expect(toasts()[0]).toMatchObject({
        description: 'Team event deleted successfully.',
      })
    })

    it('opens the dialog to add or edit a team event', async () => {
      signIn('Super Admin')
      stubAdmins()
      await mountInApp(SettingsPage, {}, { route: '/admin/settings' })

      await click(button('Add Team Event'))
      expect(bodyText()).toContain('Add Team Event')
      await click(button('Cancel', document.querySelector('[role="dialog"]')!))

      await click(buttons('Edit')[0])
      expect(bodyText()).toContain('Edit Team Event')
    })
  })
})
