import { describe, expect, it } from 'vitest'
import { getDealAction, getDealActions, getDealStatus } from '~/infrastructure/deals'
import { DealActivityState, DealBlockedReason, type DealDetail, type SlotDetail } from '~/stores/account'

const slot = (id: number, sends: { tech?: number, cash?: number }, slotNumber = 1): SlotDetail => ({
  id,
  slotNumber,
  techSentCycles: sends.tech ?? 0,
  techReceivedCycles: 0,
  cashSentCycles: sends.cash ?? 0,
  cashReceivedCycles: 0,
  hasActiveDeal: true,
  account: { accountId: id, nationId: 100 + id, nationName: `Nation${id}`, rulerName: `Ruler${id}`, allianceName: 'Valyria' }
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
  ...overrides
})

describe('getDealAction', () => {
  it('asks the sender to send on their turn', () => {
    const action = getDealAction(deal({ isMyTurnToSend: true, iAmResponsible: true, daysResponsible: 0 }))
    expect(action?.kind).toBe('send')
    expect(action?.title).toBe('Send $9M')
    expect(action?.flagged).toBe(false)
  })

  it('marks a send as flagged with its cancellation date', () => {
    const action = getDealAction(deal({
      isMyTurnToSend: true,
      iAmResponsible: true,
      daysResponsible: 2,
      cancelsOn: '2026-10-12T00:00:00',
      flaggedOn: '2026-10-09T18:00:00'
    }))
    expect(action?.flagged).toBe(true)
    expect(action?.detail).toContain('2026-10-12')
  })

  it("tells the receiver to expect aid on the partner's turn, noting when the partner is late", () => {
    const onTime = getDealAction(deal())
    expect(onTime?.kind).toBe('receive')
    expect(onTime?.title).toBe('Expect 100 tech')

    const late = getDealAction(deal({ partnerIsResponsible: true, daysResponsible: 2 }))
    expect(late?.detail).toContain('2 days late')
  })

  it('asks the receiver to accept a pending offer, but not an accepted one', () => {
    const inFlight = { state: DealActivityState.InFlight, inFlightIsIncoming: true, inFlightUntil: '2026-10-19T00:00:00' }
    expect(getDealAction(deal(inFlight))?.kind).toBe('accept')
    expect(getDealAction(deal({ ...inFlight, inFlightAccepted: true }))).toBeNull()
    expect(getDealAction(deal({ ...inFlight, inFlightIsIncoming: false }))).toBeNull()
  })

  it('only asks the responsible account to unblock a deal', () => {
    const blocked = { state: DealActivityState.Blocked, blockedReason: DealBlockedReason.OutOfSequenceAid, blockedUntil: '2026-10-17T00:00:00' }
    expect(getDealAction(deal({ ...blocked, iAmResponsible: true }))?.kind).toBe('outOfTurn')
    expect(getDealAction(deal({ ...blocked, partnerIsResponsible: true }))).toBeNull()

    const slotsFull = { state: DealActivityState.Blocked, blockedReason: DealBlockedReason.SenderSlotsFull, iAmResponsible: true }
    expect(getDealAction(deal(slotsFull))?.kind).toBe('freeSlot')
  })

  it('has nothing for deals that are starting, new, or waiting on the partner', () => {
    expect(getDealAction(deal({ state: DealActivityState.PendingStart }))).toBeNull()
    expect(getDealAction(deal({ state: null }))).toBeNull()
  })
})

describe('getDealActions', () => {
  it('lists flagged deals first, then by urgency', () => {
    const receive = deal({ id: 1 })
    const send = deal({ id: 2, isMyTurnToSend: true, iAmResponsible: true, daysResponsible: 0 })
    const flagged = deal({ id: 3, state: DealActivityState.Blocked, blockedReason: DealBlockedReason.ReceiverSlotsFull, iAmResponsible: true, flaggedOn: '2026-10-09T18:00:00' })
    const idle = deal({ id: 4, state: DealActivityState.InFlight, inFlightAccepted: true })

    expect(getDealActions([receive, send, flagged, idle]).map((a) => a.deal.id)).toEqual([3, 2, 1])
  })
})

describe('getDealStatus', () => {
  it('ignores a warning that outlived the problem', () => {
    // The flag check clears warnings once a day, so state can move on first
    const sentSinceWarning = deal({ state: DealActivityState.InFlight, inFlightAccepted: true, flaggedOn: '2026-10-09T18:00:00' })
    expect(getDealStatus(sentSinceWarning).label).toBe('In Progress')
    expect(getDealAction(sentSinceWarning)).toBeNull()
  })
})
