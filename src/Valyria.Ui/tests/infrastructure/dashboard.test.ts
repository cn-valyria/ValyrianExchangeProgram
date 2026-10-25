import { describe, expect, it } from 'vitest'
import {
  buildSlotMarkets,
  getMarketVerdict,
  getOpenSlotBreakdown,
  slotConfigName,
  slotConfigTerms,
} from '~/infrastructure/dashboard'
import {
  DealHealth,
  type DealHealthByPairing,
  type SlotConfigSummary,
  type SlotCycles,
} from '~/stores/admin'

// Cycles as [tech sent, tech received, cash sent, cash received]
type Cycles = [number, number, number, number]

const cycles = ([ts, tr, cs, cr]: Cycles): SlotCycles => ({
  techSentCycles: ts,
  techReceivedCycles: tr,
  cashSentCycles: cs,
  cashReceivedCycles: cr,
})

const config = (
  c: Cycles,
  counts: Partial<SlotConfigSummary> = {},
): SlotConfigSummary => ({
  ...cycles(c),
  accounts: 1,
  totalSlots: 4,
  slotsInDeals: 0,
  openSlots: 0,
  openAccounts: 0,
  flaggedOpenSlots: 0,
  ...counts,
})

const pairing = (
  slot1: Cycles,
  slot2: Cycles,
  health: DealHealth,
  deals: number,
): DealHealthByPairing => {
  const [a, b] = [cycles(slot1), cycles(slot2)]
  return {
    slot1TechSentCycles: a.techSentCycles,
    slot1TechReceivedCycles: a.techReceivedCycles,
    slot1CashSentCycles: a.cashSentCycles,
    slot1CashReceivedCycles: a.cashReceivedCycles,
    slot2TechSentCycles: b.techSentCycles,
    slot2TechReceivedCycles: b.techReceivedCycles,
    slot2CashSentCycles: b.cashSentCycles,
    slot2CashReceivedCycles: b.cashReceivedCycles,
    health,
    deals,
  }
}

const BUYER: Cycles = [0, 1, 1, 0]
const SELLER: Cycles = [1, 0, 0, 1]
const BUYER_2: Cycles = [0, 2, 1, 0]
const SELLER_2: Cycles = [2, 0, 0, 1]
const DONOR: Cycles = [0, 0, 1, 0]
const NONE: Cycles = [0, 0, 0, 0]

describe('slot config names', () => {
  it.each([
    [BUYER, 'Buyer', 'Sends $9M, gets 100 tech'],
    [SELLER_2, 'Seller', 'Sends 200 tech, gets $9M'],
    [DONOR, 'Cash Donor', 'Sends $9M'],
    [[0, 0, 0, 1] as Cycles, 'Cash Collector', 'Gets $9M'],
    [[1, 0, 0, 0] as Cycles, 'Tech Farm', 'Sends 100 tech'],
    [[0, 1, 0, 0] as Cycles, 'Tech Receiver', 'Gets 100 tech'],
    [[1, 1, 1, 0] as Cycles, 'Custom', 'Sends 100 tech + $9M, gets 100 tech'],
    [NONE, 'No aid', 'Sends and gets nothing'],
  ])('names %j', (c, name, terms) => {
    expect(slotConfigName(cycles(c))).toBe(name)
    expect(slotConfigTerms(cycles(c))).toBe(terms)
  })
})

describe('buildSlotMarkets', () => {
  it('pairs each config with its inverse, cash sender first, largest market first', () => {
    const { markets } = buildSlotMarkets(
      [
        config(SELLER_2, { totalSlots: 2 }),
        config(SELLER, { totalSlots: 10 }),
        config(BUYER, { totalSlots: 8 }),
        config(BUYER_2, { totalSlots: 6 }),
      ],
      [],
    )

    expect(
      markets.map((m) => [m.first.terms, m.second?.terms, m.totalSlots]),
    ).toEqual([
      ['Sends $9M, gets 100 tech', 'Sends 100 tech, gets $9M', 18],
      ['Sends $9M, gets 200 tech', 'Sends 200 tech, gets $9M', 8],
    ])
  })

  it('finds the side with too many open slots, and pairs left unmatched', () => {
    const { markets } = buildSlotMarkets(
      [
        config(BUYER, { openSlots: 1, openAccounts: 1 }),
        config(SELLER, { openSlots: 5, openAccounts: 3 }),
      ],
      [],
    )

    expect(markets[0]).toMatchObject({
      matchable: true,
      surplus: 4,
      surplusSide: 'second',
      unpairedSlots: 2,
    })
    expect(getMarketVerdict(markets[0])).toMatchObject({
      label: 'Room for 4 Buyer slots',
      color: 'warning',
    })
    expect(getMarketVerdict(markets[0]).detail).toContain('on 3 accounts')
    expect(getMarketVerdict(markets[0]).detail).toContain(
      'up to 3 deals per new account',
    )
  })

  it('calls a market with no open slots balanced', () => {
    const { markets } = buildSlotMarkets(
      [config(DONOR), config([0, 0, 0, 1])],
      [],
    )

    expect(getMarketVerdict(markets[0])).toMatchObject({
      label: 'Balanced',
      color: 'success',
    })
  })

  it('marks configs nothing can pair with as never matched, after the rest', () => {
    const { markets } = buildSlotMarkets(
      [
        config(NONE, { totalSlots: 20, openSlots: 20 }),
        config(BUYER_2, { totalSlots: 4, openSlots: 4 }),
        config(BUYER),
        config(SELLER),
      ],
      [],
    )

    expect(markets.map((m) => [m.first.name, m.matchable])).toEqual([
      ['Buyer', true],
      ['No aid', false],
      ['Buyer', false],
    ])
    expect(markets[1].second).toBeNull()
    expect(getMarketVerdict(markets[1]).detail).toContain(
      'send and get nothing',
    )
    expect(markets[2].second?.summary.totalSlots).toBe(0)
    expect(getMarketVerdict(markets[2]).detail).toBe(
      'No slot is set up the opposite way (Sends 200 tech, gets $9M).',
    )
  })

  it('puts deal health on the pairing’s market, whichever slot comes first', () => {
    const { markets, otherDeals } = buildSlotMarkets(
      [config(BUYER), config(SELLER), config(DONOR)],
      [
        pairing(BUYER, SELLER, DealHealth.OnTrack, 5),
        pairing(SELLER, BUYER, DealHealth.OnTrack, 2),
        pairing(SELLER, BUYER, DealHealth.AtRisk, 1),
        pairing(BUYER, DONOR, DealHealth.Stalled, 1),
      ],
    )

    expect(markets[0].health).toEqual({
      [DealHealth.OnTrack]: 7,
      [DealHealth.Starting]: 0,
      [DealHealth.Stalled]: 0,
      [DealHealth.AtRisk]: 1,
    })
    expect(markets[0].activeDeals).toBe(8)
    expect(otherDeals[DealHealth.Stalled]).toBe(1)
  })

  it('is empty before the dashboard loads', () => {
    expect(buildSlotMarkets(undefined, undefined).markets).toEqual([])
  })
})

describe('getOpenSlotBreakdown', () => {
  it('splits open slots the way the wait queue sees them', () => {
    const configs = [
      config(BUYER, { openSlots: 2, openAccounts: 2, flaggedOpenSlots: 3 }),
      config(SELLER, { openSlots: 7, openAccounts: 4 }),
      config(NONE, { openSlots: 4, flaggedOpenSlots: 1 }),
    ]
    const { markets } = buildSlotMarkets(configs, [])

    expect(getOpenSlotBreakdown(configs, markets)).toEqual({
      waiting: 5,
      unpaired: 4,
      unmatchable: 4,
      flagged: 4,
      total: 17,
      waitQueue: 13,
    })
  })
})
