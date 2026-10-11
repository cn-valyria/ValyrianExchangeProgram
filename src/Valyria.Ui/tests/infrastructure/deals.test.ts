import { describe, expect, it } from 'vitest'
import {
  describeAid,
  describeLeg,
  describeNextAid,
  getAdminDealStatus,
  getAidFormLink,
  getDealAction,
  getDealActions,
  getDealStatus,
  getNationLink,
  isFlagged,
} from '~/infrastructure/deals'
import { adminDeal } from '../helpers/fixtures'
import {
  DealActivityState,
  DealBlockedReason,
  type DealDetail,
  type SlotDetail,
} from '~/stores/account'

const slot = (
  id: number,
  sends: { tech?: number; cash?: number },
  slotNumber = 1,
): SlotDetail => ({
  id,
  slotNumber,
  techSentCycles: sends.tech ?? 0,
  techReceivedCycles: 0,
  cashSentCycles: sends.cash ?? 0,
  cashReceivedCycles: 0,
  hasActiveDeal: true,
  account: {
    accountId: id,
    nationId: 100 + id,
    nationName: `Nation${id}`,
    rulerName: `Ruler${id}`,
    allianceName: 'Valyria',
  },
})

// A buyer's deal with a seller: the buyer sends $9M, the seller sends 100 tech
const deal = (overrides: Partial<DealDetail> = {}): DealDetail => ({
  id: 1,
  createdOn: '2026-10-01T00:00:00',
  mySlot: slot(1, { cash: 1 }),
  partnerSlot: slot(2, { tech: 1 }),
  partnerDiscord: 'partner#1',
  lastActivityDate: null,
  state: DealActivityState.AwaitingSend,
  blockedReason: null,
  stateSince: '2026-10-09T00:00:00',
  isMyTurnToSend: false,
  iAmResponsible: false,
  partnerIsResponsible: false,
  daysResponsible: null,
  cancelsOn: null,
  inFlightUntil: null,
  inFlightIsIncoming: false,
  inFlightAccepted: false,
  blockedUntil: null,
  flaggedOn: null,
  ...overrides,
})

describe('getDealAction', () => {
  it('asks the sender to send on their turn', () => {
    const action = getDealAction(
      deal({ isMyTurnToSend: true, iAmResponsible: true, daysResponsible: 0 }),
    )
    expect(action?.kind).toBe('send')
    expect(action?.title).toBe('Send $9M')
    expect(action?.flagged).toBe(false)
  })

  it('marks a send as flagged with its cancellation date', () => {
    const action = getDealAction(
      deal({
        isMyTurnToSend: true,
        iAmResponsible: true,
        daysResponsible: 2,
        cancelsOn: '2026-10-12T00:00:00',
        flaggedOn: '2026-10-09T18:00:00',
      }),
    )
    expect(action?.flagged).toBe(true)
    expect(action?.detail).toContain('2026-10-12')
  })

  it("tells the receiver to expect aid on the partner's turn, noting when the partner is late", () => {
    const onTime = getDealAction(deal())
    expect(onTime?.kind).toBe('receive')
    expect(onTime?.title).toBe('Expect 100 tech')

    const late = getDealAction(
      deal({ partnerIsResponsible: true, daysResponsible: 2 }),
    )
    expect(late?.detail).toContain('2 days late')
  })

  it('asks the receiver to accept a pending offer, but not an accepted one', () => {
    const inFlight = {
      state: DealActivityState.InFlight,
      inFlightIsIncoming: true,
      inFlightUntil: '2026-10-19T00:00:00',
    }
    expect(getDealAction(deal(inFlight))?.kind).toBe('accept')
    expect(
      getDealAction(deal({ ...inFlight, inFlightAccepted: true })),
    ).toBeNull()
    expect(
      getDealAction(deal({ ...inFlight, inFlightIsIncoming: false })),
    ).toBeNull()
  })

  it('only asks the responsible account to unblock a deal', () => {
    const blocked = {
      state: DealActivityState.Blocked,
      blockedReason: DealBlockedReason.OutOfSequenceAid,
      blockedUntil: '2026-10-17T00:00:00',
    }
    expect(
      getDealAction(deal({ ...blocked, iAmResponsible: true }))?.kind,
    ).toBe('outOfTurn')
    expect(
      getDealAction(deal({ ...blocked, partnerIsResponsible: true })),
    ).toBeNull()

    const slotsFull = {
      state: DealActivityState.Blocked,
      blockedReason: DealBlockedReason.SenderSlotsFull,
      iAmResponsible: true,
    }
    expect(getDealAction(deal(slotsFull))?.kind).toBe('freeSlot')
  })

  it('has nothing for deals that are starting, new, or waiting on the partner', () => {
    expect(
      getDealAction(deal({ state: DealActivityState.PendingStart })),
    ).toBeNull()
    expect(getDealAction(deal({ state: null }))).toBeNull()
  })
})

describe('getDealActions', () => {
  it('lists flagged deals first, then by urgency', () => {
    const receive = deal({ id: 1 })
    const send = deal({
      id: 2,
      isMyTurnToSend: true,
      iAmResponsible: true,
      daysResponsible: 0,
    })
    const flagged = deal({
      id: 3,
      state: DealActivityState.Blocked,
      blockedReason: DealBlockedReason.ReceiverSlotsFull,
      iAmResponsible: true,
      flaggedOn: '2026-10-09T18:00:00',
    })
    const idle = deal({
      id: 4,
      state: DealActivityState.InFlight,
      inFlightAccepted: true,
    })

    expect(
      getDealActions([receive, send, flagged, idle]).map((a) => a.deal.id),
    ).toEqual([3, 2, 1])
  })
})

describe('getDealStatus', () => {
  it('ignores a warning that outlived the problem', () => {
    // The flag check clears warnings once a day, so state can move on first
    const sentSinceWarning = deal({
      state: DealActivityState.InFlight,
      inFlightAccepted: true,
      flaggedOn: '2026-10-09T18:00:00',
    })
    expect(getDealStatus(sentSinceWarning).label).toBe('In Progress')
    expect(getDealAction(sentSinceWarning)).toBeNull()
  })
})

describe('describeAid', () => {
  it('describes tech, cash, both, or nothing', () => {
    expect(describeAid(1, 0)).toBe('100 tech')
    expect(describeAid(0, 2)).toBe('$18M')
    expect(describeAid(2, 1)).toBe('200 tech + $9M')
    expect(describeAid(0, 0)).toBe('Nothing')
  })
})

describe('describeLeg', () => {
  it('describes one offer from a slot', () => {
    expect(describeLeg(slot(1, { cash: 3 }))).toBe('$9M')
    expect(describeLeg(slot(1, { tech: 2 }))).toBe('100 tech')
    expect(describeLeg(slot(1, { tech: 1, cash: 1 }))).toBe('$9M or 100 tech')
    expect(describeLeg(slot(1, {}))).toBe('aid')
  })
})

describe('describeNextAid', () => {
  it('names who sends next', () => {
    expect(describeNextAid(deal({ isMyTurnToSend: true }))).toBe('You send $9M')
    expect(describeNextAid(deal())).toBe('Partner sends 100 tech')
    expect(describeNextAid(deal({ state: null }))).toBe('Not known yet')
  })
})

describe('links', () => {
  const partner = {
    nationId: 202,
    nationName: 'Sellland',
    rulerName: 'Sue',
    allianceName: 'Valyria',
  }

  it('links to the aid form from my nation to the partner', () => {
    expect(getAidFormLink(partner, 101)).toBe(
      'https://www.cybernations.net/aid_form.asp?Nation_ID=202&bynation=101',
    )
  })

  it("links to the partner's nation page", () => {
    expect(getNationLink(partner)).toBe(
      'https://www.cybernations.net/nation_drill_display.asp?Nation_ID=202',
    )
  })
})

describe('getDealStatus', () => {
  it('calls a deal the data loader has not seen yet new', () => {
    expect(getDealStatus(deal({ state: null })).label).toBe('New')
  })

  it('shows flagged deals with their cancellation date', () => {
    const status = getDealStatus(
      deal({
        iAmResponsible: true,
        flaggedOn: '2026-10-09T18:00:00',
        cancelsOn: '2026-10-12T00:00:00',
      }),
    )
    expect(status).toMatchObject({ label: 'Flagged', color: 'error' })
    expect(status.detail).toContain('2026-10-12')
  })

  it('falls back to "soon" when a flagged deal has no cancellation date', () => {
    expect(
      getDealStatus(
        deal({ iAmResponsible: true, flaggedOn: '2026-10-09T18:00:00' }),
      ).detail,
    ).toContain('cancelled on soon')
  })

  it.each([
    [{ state: DealActivityState.PendingStart }, 'Starting', 'neutral'],
    [{ isMyTurnToSend: true }, 'Your Turn', 'warning'],
    [
      { partnerIsResponsible: true, daysResponsible: 1 },
      'Partner Late',
      'warning',
    ],
    [
      { partnerIsResponsible: true, daysResponsible: 0 },
      "Partner's Turn",
      'info',
    ],
    [{}, "Partner's Turn", 'info'],
    [
      { state: DealActivityState.InFlight, inFlightIsIncoming: true },
      'Accept Aid',
      'warning',
    ],
    [{ state: DealActivityState.InFlight }, 'Awaiting Acceptance', 'info'],
    [
      { state: DealActivityState.InFlight, inFlightAccepted: true },
      'In Progress',
      'success',
    ],
    [
      { state: DealActivityState.Blocked, iAmResponsible: true },
      'Blocked by You',
      'error',
    ],
    [
      { state: DealActivityState.Blocked, partnerIsResponsible: true },
      'Blocked by Partner',
      'neutral',
    ],
  ] as [Partial<DealDetail>, string, string][])(
    'labels %o as %s',
    (overrides, label, color) => {
      expect(getDealStatus(deal(overrides))).toMatchObject({ label, color })
    },
  )

  it('pluralizes how late the partner is', () => {
    expect(
      getDealStatus(deal({ partnerIsResponsible: true, daysResponsible: 1 }))
        .detail,
    ).toContain('1 day late')
    expect(
      getDealStatus(deal({ partnerIsResponsible: true, daysResponsible: 3 }))
        .detail,
    ).toContain('3 days late')
  })

  it('says what comes next once in-flight aid is accepted', () => {
    const status = getDealStatus(
      deal({
        state: DealActivityState.InFlight,
        inFlightAccepted: true,
        isMyTurnToSend: true,
        inFlightUntil: '2026-10-19T00:00:00',
      }),
    )
    expect(status.detail).toBe(
      'Aid is in progress until 2026-10-19. Next: you send $9m.',
    )
  })

  it("explains why the partner's side is blocked", () => {
    const outOfTurn = getDealStatus(
      deal({
        state: DealActivityState.Blocked,
        blockedReason: DealBlockedReason.OutOfSequenceAid,
        blockedUntil: '2026-10-17T00:00:00',
      }),
    )
    expect(outOfTurn.detail).toContain('out of turn')
    expect(outOfTurn.detail).toContain('2026-10-17')

    const slotsFull = getDealStatus(
      deal({
        state: DealActivityState.Blocked,
        blockedReason: DealBlockedReason.ReceiverSlotsFull,
      }),
    )
    expect(slotsFull.detail).toContain('aid slots are full')
  })

  it.each([
    [DealBlockedReason.OutOfSequenceAid, 'your aid blocks them'],
    [DealBlockedReason.ReceiverSlotsFull, "can't send you 100 tech"],
    [DealBlockedReason.SenderSlotsFull, "you can't send $9M"],
  ])('explains how to unblock a deal blocked by %s', (blockedReason, text) => {
    expect(
      getDealStatus(
        deal({
          state: DealActivityState.Blocked,
          iAmResponsible: true,
          blockedReason,
        }),
      ).detail,
    ).toContain(text)
  })
})

describe('getDealAction details', () => {
  it('warns that an unflagged send keeps the deal from being flagged', () => {
    expect(
      getDealAction(deal({ isMyTurnToSend: true, iAmResponsible: true }))
        ?.detail,
    ).toContain('keep the deal from being flagged')
  })

  it('uses the right preposition for each kind of action', () => {
    const blocked = { state: DealActivityState.Blocked, iAmResponsible: true }
    expect(getDealAction(deal({ isMyTurnToSend: true }))?.preposition).toBe(
      'to',
    )
    expect(getDealAction(deal())?.preposition).toBe('from')
    expect(
      getDealAction(
        deal({ ...blocked, blockedReason: DealBlockedReason.OutOfSequenceAid }),
      )?.preposition,
    ).toBe('to')
    expect(
      getDealAction(
        deal({ ...blocked, blockedReason: DealBlockedReason.SenderSlotsFull }),
      )?.preposition,
    ).toBe('for')
  })

  it('sorts equally urgent actions by slot number', () => {
    const second = deal({ id: 1, mySlot: slot(1, { cash: 1 }, 2) })
    const first = deal({ id: 2, mySlot: slot(1, { cash: 1 }, 1) })
    expect(getDealActions([second, first]).map((a) => a.deal.id)).toEqual([
      2, 1,
    ])
  })
})

describe('isFlagged', () => {
  it('only counts a warning while the account is still responsible', () => {
    expect(
      isFlagged(
        deal({ flaggedOn: '2026-10-09T18:00:00', iAmResponsible: true }),
      ),
    ).toBe(true)
    expect(
      isFlagged(
        deal({ flaggedOn: '2026-10-09T18:00:00', iAmResponsible: false }),
      ),
    ).toBe(false)
    expect(isFlagged(deal({ iAmResponsible: true }))).toBe(false)
  })
})

describe('getAdminDealStatus', () => {
  it('calls a deal the data loader has not seen yet new', () => {
    expect(getAdminDealStatus(adminDeal({ state: null })).label).toBe('New')
  })

  it('shows who sends next once aid is in flight', () => {
    const status = getAdminDealStatus(
      adminDeal({
        state: DealActivityState.InFlight,
        nextSenderAccountId: 2,
        inFlightUntil: '2026-10-19T00:00:00',
      }),
    )
    expect(status).toMatchObject({ label: 'In Progress', color: 'success' })
    expect(status.detail).toBe(
      'Aid in flight until 2026-10-19. Ruler2 sends next.',
    )
  })

  it('shows deals that are starting', () => {
    expect(
      getAdminDealStatus(adminDeal({ state: DealActivityState.PendingStart }))
        .label,
    ).toBe('Starting')
  })

  it('shows who is holding up a deal, turning red after a day', () => {
    const today = getAdminDealStatus(
      adminDeal({
        responsibleAccountId: 1,
        daysResponsible: 0,
        cancelsOn: '2026-10-14T00:00:00',
      }),
    )
    expect(today).toMatchObject({ label: 'Awaiting Send', color: 'warning' })
    expect(today.detail).toBe(
      'Ruler1 should send. Ruler1 has held it up for 0 days; cancels on 2026-10-14.',
    )

    const late = getAdminDealStatus(
      adminDeal({ responsibleAccountId: 1, daysResponsible: 1 }),
    )
    expect(late.color).toBe('error')
    expect(late.detail).toContain('1 day;')
  })

  it("leaves out who's holding up a deal when no one is", () => {
    expect(getAdminDealStatus(adminDeal()).detail).toBe('Ruler1 should send.')
  })

  it('explains why a deal is blocked', () => {
    const outOfTurn = getAdminDealStatus(
      adminDeal({
        state: DealActivityState.Blocked,
        blockedReason: DealBlockedReason.OutOfSequenceAid,
      }),
    )
    expect(outOfTurn.detail).toBe('Aid sent out of turn.')
    const slotsFull = getAdminDealStatus(
      adminDeal({
        state: DealActivityState.Blocked,
        blockedReason: DealBlockedReason.SenderSlotsFull,
        responsibleAccountId: 2,
        daysResponsible: 2,
      }),
    )
    expect(slotsFull).toMatchObject({ label: 'Blocked', color: 'error' })
    expect(slotsFull.detail).toContain(
      'Aid slots full of non-VEP aid. Ruler2 has held it up for 2 days',
    )
  })

  it("names an unknown account when the sender isn't in the deal", () => {
    expect(
      getAdminDealStatus(adminDeal({ nextSenderAccountId: 99 })).detail,
    ).toBe('Unknown should send.')
  })
})
