import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { UPagination } from '#components'
import ActionNeeded from '~/components/account/ActionNeeded.vue'
import ActiveDeals from '~/components/account/ActiveDeals.vue'
import DealHealth from '~/components/account/DealHealth.vue'
import DealHistory from '~/components/account/DealHistory.vue'
import SettingsDialog from '~/components/account/SettingsDialog.vue'
import SlotUsage from '~/components/account/SlotUsage.vue'
import {
  DealActivityState,
  DealBlockedReason,
  DealLegOutcome,
  DealStatus,
  useAccountStore,
  type DealHistoryItem,
} from '~/stores/account'
import { DealFlagReason } from '~/stores/adminDeals'
import {
  account,
  accountSimplified,
  dealDetail,
  dealFlag,
  teamBonusSlot,
} from '../helpers/fixtures'
import {
  addDays,
  cnToday,
  formatCnDate,
  Team,
} from '~/infrastructure/teamEvents'
import { button, click, mountInApp, toasts } from '../helpers/dom'

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {})
})

const myTurn = {
  isMyTurnToSend: true,
  iAmResponsible: true,
  daysResponsible: 0,
}
const flagged = {
  ...myTurn,
  flaggedOn: '2026-10-09T18:00:00',
  cancelsOn: '2026-10-12T00:00:00',
}

describe('SlotUsage', () => {
  it('shows open slots', async () => {
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 3,
      slotsTotal: 4,
      role: 'B',
    })

    expect(wrapper.text()).toContain('3 / 4')
    expect(wrapper.text()).toContain('1 Open')
    expect(wrapper.text()).toContain('You have 1 open slot.')
  })

  it('pluralizes open slots', async () => {
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 1,
      slotsTotal: 4,
      role: 'B',
    })

    expect(wrapper.text()).toContain('You have 3 open slots.')
  })

  it('shows when every slot is in a deal', async () => {
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 4,
      slotsTotal: 4,
      role: 'S',
    })

    expect(wrapper.text()).toContain('Full')
    expect(wrapper.text()).toContain('All of your slots are in active deals.')
  })

  it('hides the counts for accounts on hold', async () => {
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 0,
      slotsTotal: 4,
      role: 'H',
    })

    expect(wrapper.text()).toContain('--')
    expect(wrapper.text()).toContain('Hold')
    expect(wrapper.text()).toContain('Your account is on hold')
  })
})

describe('SlotUsage team bonus', () => {
  const event = (matchableUntil: string) => ({
    team: Team.Aqua,
    approvedOn: '2026-10-06T00:00:00',
    endsOn: addDays(cnToday(), 10),
    bonusSlotMatchableUntil: matchableUntil,
  })

  it('says where the bonus slot comes from and until when it can be matched', async () => {
    const until = addDays(cnToday(), 3)
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 4,
      slotsTotal: 5,
      role: 'B',
      teamEvent: event(until),
    })

    expect(wrapper.text()).toContain(
      `Includes 1 bonus slot from the Aqua team's event, in effect through ${formatCnDate(addDays(cnToday(), 10))}.`,
    )
    expect(wrapper.text()).toContain(
      `VEP can match it into a temporary deal until ${formatCnDate(until)}.`,
    )
  })

  it("says when it's too late to start a deal in the bonus slot", async () => {
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 4,
      slotsTotal: 5,
      role: 'B',
      teamEvent: event(addDays(cnToday(), -1)),
    })

    expect(wrapper.text()).toContain('too close to the end of the event')
  })

  it('says the deal in the bonus slot is temporary', async () => {
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 5,
      slotsTotal: 5,
      role: 'B',
      teamEvent: event(addDays(cnToday(), 3)),
      bonusSlotInDeal: true,
    })

    expect(wrapper.text()).toContain(
      'The deal using it is temporary and ends with the event.',
    )
  })

  it('says nothing without a team event', async () => {
    const wrapper = await mountInApp(SlotUsage, {
      slotsUsed: 3,
      slotsTotal: 4,
      role: 'B',
    })

    expect(wrapper.text()).not.toContain('bonus slot')
  })
})

describe('DealHealth', () => {
  it('has nothing to report without deals', async () => {
    const wrapper = await mountInApp(DealHealth, {
      deals: [],
      notifications: [],
    })

    expect(wrapper.text()).toContain('No Deals')
    expect(wrapper.text()).toContain("You don't have any active deals yet.")
  })

  it('is all good when no deal needs anything today', async () => {
    const wrapper = await mountInApp(DealHealth, {
      deals: [
        dealDetail({
          state: DealActivityState.InFlight,
          inFlightAccepted: true,
        }),
      ],
      notifications: [],
    })

    expect(wrapper.text()).toContain('All Good')
    expect(wrapper.text()).toContain('0')
  })

  it('counts deals needing action', async () => {
    const wrapper = await mountInApp(DealHealth, {
      deals: [dealDetail(myTurn), dealDetail({ id: 2 })],
      notifications: [],
    })

    expect(wrapper.text()).toContain('Action Today')
    expect(wrapper.text()).toContain(
      '2 of your deals need something from you today.',
    )
  })

  it('uses the singular for one deal needing action', async () => {
    const wrapper = await mountInApp(DealHealth, {
      deals: [dealDetail(myTurn)],
      notifications: [],
    })

    expect(wrapper.text()).toContain(
      '1 of your deals needs something from you today.',
    )
  })

  it('warns about flagged deals', async () => {
    const wrapper = await mountInApp(DealHealth, {
      deals: [dealDetail(flagged), dealDetail({ id: 2, ...flagged })],
      notifications: [],
    })

    expect(wrapper.text()).toContain('Flagged')
    expect(wrapper.text()).toContain(
      '2 of your deals are flagged. Fix them below',
    )
  })

  it('uses the singular for one flagged deal', async () => {
    const wrapper = await mountInApp(DealHealth, {
      deals: [dealDetail(flagged)],
      notifications: [],
    })

    expect(wrapper.text()).toContain('1 of your deals is flagged. Fix it below')
  })

  it('warns when a deal was cancelled over a flag on this account', async () => {
    const notifications = [
      { type: 'error', message: 'Your deal was cancelled', dealId: 1 },
      { type: 'info', message: 'x', dealId: 2 },
    ]
    const wrapper = await mountInApp(DealHealth, { deals: [], notifications })

    expect(wrapper.text()).toContain('Flagged')
    expect(wrapper.text()).toContain(
      'A deal was cancelled over a flag on your account.',
    )
  })
})

describe('ActionNeeded', () => {
  it('renders nothing when no deal needs action', async () => {
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [dealDetail({ state: DealActivityState.PendingStart })],
      myNationId: 101,
    })

    expect(wrapper.text()).toBe('')
  })

  it('links the sender to the aid form', async () => {
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [dealDetail(myTurn)],
      myNationId: 101,
    })

    expect(wrapper.text()).toContain('Action Needed Today')
    expect(wrapper.text()).toContain('1 deal')
    expect(wrapper.text()).toContain('Send $9M to Ruler2 of Nation2')
    expect(wrapper.text()).toMatch(/Slot #1\s+· Discord: partner#1/)
    expect(button('Open Aid Form', wrapper.element).getAttribute('href')).toBe(
      'https://www.cybernations.net/aid_form.asp?Nation_ID=102&bynation=101',
    )
  })

  it('links the receiver to accept aid', async () => {
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [
        dealDetail({
          state: DealActivityState.InFlight,
          inFlightIsIncoming: true,
          partnerDiscord: null,
        }),
      ],
      myNationId: 101,
    })

    expect(wrapper.text()).toContain('Accept 100 tech from Ruler2')
    expect(wrapper.text()).not.toContain('Discord:')
    expect(button('Accept Aid', wrapper.element).getAttribute('href')).toBe(
      'https://www.cybernations.net/aid_information.asp',
    )
  })

  it('lists blocked and incoming deals without a button', async () => {
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [
        dealDetail({ id: 1 }),
        dealDetail({
          id: 2,
          state: DealActivityState.Blocked,
          iAmResponsible: true,
          blockedReason: DealBlockedReason.SenderSlotsFull,
        }),
      ],
      myNationId: 101,
    })

    expect(wrapper.text()).toContain('2 deals')
    expect(wrapper.text()).toContain('Free an aid slot for Ruler2')
    expect(wrapper.text()).toContain('Expect 100 tech from Ruler2')
    expect(
      wrapper.findAll('a').filter((a) => a.text() === 'Open Aid Form'),
    ).toHaveLength(0)
  })

  it('calls out flagged deals', async () => {
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [dealDetail(flagged), dealDetail({ id: 2, ...flagged })],
      myNationId: 101,
    })

    expect(wrapper.text()).toContain(
      '2 of your deals are flagged for inactivity',
    )
  })

  it('uses the singular for one flagged deal', async () => {
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [dealDetail(flagged)],
      myNationId: 101,
    })

    expect(wrapper.text()).toContain(
      '1 of your deals is flagged for inactivity',
    )
  })

  it('explains a flag, with the aid holding the slots, when Flagged is clicked', async () => {
    const store = useAccountStore()
    const loadDealFlag = vi
      .spyOn(store, 'loadDealFlag')
      .mockImplementation(async (_accountId, dealId) => {
        store.dealFlags[dealId] = {
          flag: dealFlag({
            dealId,
            flagReason: DealFlagReason.SenderSlotsFull,
          }),
          evidence: {
            legs: [],
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
          },
        }
      })
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [dealDetail({ id: 3, ...flagged })],
      myNationId: 101,
      accountId: 1,
    })
    expect(wrapper.text()).not.toContain('What happened')

    await click(button('Flagged', wrapper.element))

    expect(loadDealFlag).toHaveBeenCalledWith(1, 3)
    expect(wrapper.text()).toContain('Slots full (sender)')
    expect(wrapper.text()).toContain(
      'Your aid slots were all taken, at least one by aid outside VEP',
    )
    expect(wrapper.text()).toContain('Your aid slots right now')
    expect(wrapper.text()).not.toContain('Ruler1')
    expect(wrapper.text()).toContain('Sent to Faraway (Farland)')
    expect(wrapper.text()).toContain('Outside VEP')

    await click(button('Flagged', wrapper.element))
    expect(wrapper.text()).not.toContain('What happened')
  })

  it('offers a retry when the flag fails to load', async () => {
    const store = useAccountStore()
    const loadDealFlag = vi
      .spyOn(store, 'loadDealFlag')
      .mockRejectedValue(new Error('boom'))
    const wrapper = await mountInApp(ActionNeeded, {
      deals: [dealDetail(flagged)],
      myNationId: 101,
      accountId: 1,
    })

    await click(button('Flagged', wrapper.element))
    expect(wrapper.text()).toContain("Couldn't load why this deal was flagged.")

    await click(button('Try Again', wrapper.element))
    expect(loadDealFlag).toHaveBeenCalledTimes(2)
  })
})

describe('ActiveDeals', () => {
  it('tells accounts on hold they are not matched', async () => {
    const wrapper = await mountInApp(ActiveDeals, {
      account: account({ role: 'H' }),
      deals: [dealDetail()],
      notifications: [],
    })

    expect(wrapper.text()).toContain('Your account is on hold')
  })

  it('tells new accounts they are waiting to be matched', async () => {
    const wrapper = await mountInApp(ActiveDeals, {
      account: account(),
      deals: [],
      notifications: [],
    })

    expect(wrapper.text()).toContain('Waiting to be matched')
  })

  it('shows notifications about cancelled deals even without active deals', async () => {
    const notifications = [
      { type: 'error', message: 'You held up your deal', dealId: 1 },
      { type: 'info', message: 'Your partner held up your deal', dealId: 2 },
    ]
    const wrapper = await mountInApp(ActiveDeals, {
      account: account(),
      deals: [],
      notifications,
    })

    expect(wrapper.text()).toContain('You held up your deal')
    expect(wrapper.text()).toContain('Your partner held up your deal')
  })

  it('lists deals by slot, with what each side sends and its status', async () => {
    const partner = accountSimplified(3, {
      nationId: 303,
      rulerName: 'Tia',
      nationName: 'Techland',
      allianceName: 'Fark',
    })
    const deals = [
      dealDetail({
        id: 2,
        mySlot: { ...dealDetail().mySlot, slotNumber: 2 },
        lastActivityDate: '2026-10-05T00:00:00',
      }),
      dealDetail({
        id: 1,
        ...myTurn,
        partnerSlot: { ...dealDetail().partnerSlot, account: partner },
      }),
    ]
    const wrapper = await mountInApp(ActiveDeals, {
      account: account({ nationId: 101 }),
      deals,
      notifications: [],
    })

    const rows = wrapper.findAll('tbody tr').map((row) => row.text())
    expect(rows).toHaveLength(2)
    expect(rows[0]).toContain('#1')
    expect(rows[0]).toContain('Tia of Techland')
    expect(rows[0]).toContain('You send $9M')
    expect(rows[0]).toContain('Your Turn')
    expect(rows[0]).toContain('None yet')
    expect(rows[1]).toContain('#2')
    expect(rows[1]).toContain('2026-10-05')
    expect(rows[1]).toContain("Partner's Turn")
    // Senders get a link straight to the aid form
    expect(wrapper.find('tbody a').attributes('href')).toBe(
      'https://www.cybernations.net/aid_form.asp?Nation_ID=303&bynation=101',
    )
  })

  it("links receive-only partners to their nation page, and shows what's received", async () => {
    const receiveOnly = dealDetail({
      mySlot: { ...dealDetail().mySlot, cashSentCycles: 0 },
      partnerDiscord: null,
    })
    const wrapper = await mountInApp(ActiveDeals, {
      account: account(),
      deals: [receiveOnly],
      notifications: [],
    })

    expect(wrapper.find('tbody a').attributes('href')).toBe(
      'https://www.cybernations.net/nation_drill_display.asp?Nation_ID=102',
    )
    expect(wrapper.find('tbody tr').text()).toContain('Nothing')
    expect(wrapper.find('tbody tr').text()).toContain('100 tech')
  })
})

describe('ActiveDeals temporary deals', () => {
  it('explains which deals end with a team event, whichever side has the bonus slot', async () => {
    const partner = accountSimplified(3, { nationName: 'Techland' })
    const other = accountSimplified(4, { nationName: 'Otherland' })
    const deals = [
      dealDetail({
        id: 1,
        mySlot: teamBonusSlot(1, '2026-11-05T00:00:00', { cash: 1 }),
        partnerSlot: { ...dealDetail().partnerSlot, account: partner },
        teamBonusEndsOn: '2026-11-05T00:00:00',
      }),
      dealDetail({
        id: 2,
        mySlot: { ...dealDetail().mySlot, slotNumber: 2 },
        partnerSlot: teamBonusSlot(
          4,
          '2026-11-20T00:00:00',
          { tech: 1 },
          {
            account: other,
          },
        ),
        teamBonusEndsOn: '2026-11-20T00:00:00',
      }),
      dealDetail({ id: 3, mySlot: { ...dealDetail().mySlot, slotNumber: 3 } }),
    ]
    const wrapper = await mountInApp(ActiveDeals, {
      account: account(),
      deals,
      notifications: [],
    })

    expect(wrapper.text()).toContain('2 of your deals are temporary')
    expect(wrapper.text()).toContain(
      "Your deal with Techland uses your team's bonus aid slot, so VEP cancels it once the team event ends after 11/5/2026.",
    )
    expect(wrapper.text()).toContain(
      "Your deal with Otherland uses Otherland's team bonus aid slot, so VEP cancels it once the team event ends after 11/20/2026.",
    )
    const rows = wrapper.findAll('tbody tr').map((row) => row.text())
    expect(rows[0]).toContain('#2')
    expect(rows[0]).toContain('Temporary, ends 11/20/2026')
    expect(rows[1]).not.toContain(', ends ')
    expect(rows[2]).toContain('#7')
    expect(rows[2]).toContain('Team bonus, ends 11/5/2026')
  })

  it('has no temporary deal notice without team bonus deals', async () => {
    const wrapper = await mountInApp(ActiveDeals, {
      account: account(),
      deals: [dealDetail()],
      notifications: [],
    })

    expect(wrapper.text()).not.toContain('temporary')
  })
})

describe('DealHistory', () => {
  const historyItem = (
    id: number,
    overrides: Partial<DealHistoryItem> = {},
  ): DealHistoryItem => ({
    id,
    status: DealStatus.Active,
    createdOn: '2026-09-01T00:00:00',
    cancelledOn: null,
    cancellationReason: null,
    mySlotNumber: 1,
    aidSentCount: 2,
    aidReceivedCount: 1,
    lastActivityDate: '2026-10-01T00:00:00',
    partner: accountSimplified(id + 10),
    ...overrides,
  })

  const withHistory = (
    results: DealHistoryItem[],
    totalCount = results.length,
  ) => {
    const store = useAccountStore()
    store.dealHistory = { totalCount, results }
    return store
  }

  it('lists every deal, active and ended', async () => {
    withHistory([
      historyItem(1),
      historyItem(2, {
        status: DealStatus.Cancelled,
        cancelledOn: '2026-09-20T00:00:00',
        lastActivityDate: null,
      }),
    ])

    const wrapper = await mountInApp(DealHistory, { accountId: 1 })

    expect(wrapper.text()).toContain('2 deals')
    const rows = wrapper.findAll('tbody tr').map((row) => row.text())
    expect(rows[0]).toContain('Ruler11 of Nation11')
    expect(rows[0]).toContain('Active')
    expect(rows[0]).toContain('2026-09-01')
    expect(rows[1]).toContain('Ended')
    expect(rows[1]).toContain('2026-09-20')
    expect(wrapper.text()).toContain('Showing 1 to 2 of 2 results')
  })

  it('uses the singular for one deal', async () => {
    withHistory([historyItem(1)])

    const wrapper = await mountInApp(DealHistory, { accountId: 1 })

    expect(wrapper.text()).toContain('1 deal')
  })

  it("loads a deal's aid activity the first time it's expanded", async () => {
    const store = withHistory([
      historyItem(1, { cancellationReason: 'Partner left VEP' }),
    ])
    const loadActivity = vi
      .spyOn(store, 'loadDealActivity')
      .mockImplementation(async (_accountId, dealId) => {
        store.dealActivity[dealId] = [
          {
            id: 1,
            aidId: 9,
            activityDate: '2026-09-02T00:00:00',
            isOutgoing: true,
            money: 9000000,
            technology: 0,
            outcome: DealLegOutcome.Accepted,
          },
          {
            id: 2,
            aidId: 10,
            activityDate: '2026-09-12T00:00:00',
            isOutgoing: false,
            money: 0,
            technology: 100,
            outcome: DealLegOutcome.Expired,
          },
          {
            id: 3,
            aidId: null,
            activityDate: '2026-09-22T00:00:00',
            isOutgoing: true,
            money: 0,
            technology: 0,
            outcome: DealLegOutcome.Pending,
          },
          {
            id: 4,
            aidId: null,
            activityDate: '2026-09-23T00:00:00',
            isOutgoing: false,
            money: 0,
            technology: 0,
            outcome: DealLegOutcome.Cancelled,
          },
        ]
      })
    const wrapper = await mountInApp(DealHistory, { accountId: 1 })
    const expand = () =>
      click(
        wrapper.find('button[aria-label="Show aid activity"]')
          .element as HTMLElement,
      )

    await expand()

    expect(loadActivity).toHaveBeenCalledWith(1, 1)
    expect(wrapper.text()).toContain('Ended: Partner left VEP')
    expect(wrapper.text()).toContain('$9,000,000.00')
    for (const label of [
      'Sent',
      'Received',
      'Accepted',
      'Expired',
      'Pending',
      'Cancelled',
    ]) {
      expect(wrapper.text()).toContain(label)
    }

    // Collapsing and expanding again doesn't reload it
    await expand()
    await expand()
    expect(loadActivity).toHaveBeenCalledOnce()
  })

  it('says when a deal has no recorded aid', async () => {
    const store = withHistory([historyItem(1)])
    vi.spyOn(store, 'loadDealActivity').mockImplementation(
      async (_accountId, dealId) => {
        store.dealActivity[dealId] = []
      },
    )
    const wrapper = await mountInApp(DealHistory, { accountId: 1 })

    await click(
      wrapper.find('button[aria-label="Show aid activity"]')
        .element as HTMLElement,
    )

    expect(wrapper.text()).toContain('No aid has been recorded for this deal.')
  })

  it("shows an error when a deal's activity can't be loaded", async () => {
    const store = withHistory([historyItem(1)])
    vi.spyOn(store, 'loadDealActivity').mockRejectedValue(new Error('down'))
    const wrapper = await mountInApp(DealHistory, { accountId: 1 })

    await click(
      wrapper.find('button[aria-label="Show aid activity"]')
        .element as HTMLElement,
    )

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description:
        'Failed to load aid activity for this deal. Please try again.',
    })
  })

  it('loads the next page of history', async () => {
    const store = withHistory([historyItem(1)], 25)
    const loadHistory = vi.spyOn(store, 'loadDealHistory').mockResolvedValue()
    const wrapper = await mountInApp(DealHistory, { accountId: 7 })

    wrapper.findComponent(UPagination).vm.$emit('update:page', 3)
    await flushPromises()

    expect(loadHistory).toHaveBeenCalledWith(7, 10, 20)
    expect(wrapper.text()).toContain('Showing 21 to 25 of 25 results')
  })

  it("shows an error when a page can't be loaded", async () => {
    const store = withHistory([historyItem(1)], 25)
    vi.spyOn(store, 'loadDealHistory').mockRejectedValue(new Error('down'))
    const wrapper = await mountInApp(DealHistory, { accountId: 7 })

    wrapper.findComponent(UPagination).vm.$emit('update:page', 2)
    await flushPromises()

    expect(toasts()[0]).toMatchObject({
      color: 'error',
      description: 'Failed to load deal history. Please try again.',
    })
  })
})

describe('SettingsDialog', () => {
  const discordSwitch = () =>
    document.body.querySelector<HTMLButtonElement>('button[role="switch"]')!

  const openDialog = async (discordNotificationsEnabled = true) => {
    const onClose = vi.fn()
    await mountInApp(SettingsDialog, {
      open: true,
      onClose,
      account: account({ id: 1, discordNotificationsEnabled }),
    })
    return onClose
  }

  it('only saves once a setting has changed', async () => {
    const updateSettings = vi
      .spyOn(useAccountStore(), 'updateSettings')
      .mockResolvedValue()
    const onClose = await openDialog(true)

    expect(discordSwitch().getAttribute('aria-checked')).toBe('true')
    expect(button('Save').disabled).toBe(true)

    await click(discordSwitch())
    await click(button('Save'))

    expect(updateSettings).toHaveBeenCalledWith(1, {
      discordNotificationsEnabled: false,
    })
    expect(toasts()[0]).toMatchObject({ color: 'success' })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it('turns notifications back on', async () => {
    const updateSettings = vi
      .spyOn(useAccountStore(), 'updateSettings')
      .mockResolvedValue()
    await openDialog(false)

    expect(discordSwitch().getAttribute('aria-checked')).toBe('false')
    await click(discordSwitch())
    await click(button('Save'))

    expect(updateSettings).toHaveBeenCalledWith(1, {
      discordNotificationsEnabled: true,
    })
  })

  it('stays open with an error when saving fails', async () => {
    vi.spyOn(useAccountStore(), 'updateSettings').mockRejectedValue(
      new Error('down'),
    )
    const onClose = await openDialog(true)

    await click(discordSwitch())
    await click(button('Save'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })
})
