import { describe, expect, it, vi } from 'vitest'
import CancellationReasons from '~/components/admin/dashboard/CancellationReasons.vue'
import DealActivity from '~/components/admin/dashboard/DealActivity.vue'
import DealHealthByPairing from '~/components/admin/dashboard/DealHealthByPairing.vue'
import OpenSlotBreakdown from '~/components/admin/dashboard/OpenSlotBreakdown.vue'
import SlotMarkets from '~/components/admin/dashboard/SlotMarkets.vue'
import SummaryCards from '~/components/admin/dashboard/SummaryCards.vue'
import {
  buildSlotMarkets,
  getOpenSlotBreakdown,
} from '~/infrastructure/dashboard'
import { DealHealth, type SlotConfigSummary } from '~/stores/admin'
import { mountInApp } from '../helpers/dom'

// happy-dom has no canvas, so charts are stubbed to expose the data they're given
vi.mock('vue-chartjs', () => import('../helpers/chartStub'))

const config = (
  [ts, tr, cs, cr]: [number, number, number, number],
  counts: Partial<SlotConfigSummary>,
): SlotConfigSummary => ({
  techSentCycles: ts,
  techReceivedCycles: tr,
  cashSentCycles: cs,
  cashReceivedCycles: cr,
  accounts: 1,
  totalSlots: 4,
  slotsInDeals: 0,
  openSlots: 0,
  openAccounts: 0,
  flaggedOpenSlots: 0,
  ...counts,
})

// 1:1 buyers and sellers with 4 sellers' slots more open than buyers', and an unmatchable config
const configs = [
  config([0, 1, 1, 0], {
    accounts: 10,
    totalSlots: 40,
    slotsInDeals: 36,
    openSlots: 2,
    openAccounts: 2,
    flaggedOpenSlots: 2,
  }),
  config([1, 0, 0, 1], {
    accounts: 11,
    totalSlots: 42,
    slotsInDeals: 36,
    openSlots: 6,
    openAccounts: 5,
  }),
  config([0, 0, 0, 0], { totalSlots: 4, openSlots: 4, openAccounts: 1 }),
]

const dealHealth = [
  {
    slot1TechSentCycles: 0,
    slot1TechReceivedCycles: 1,
    slot1CashSentCycles: 1,
    slot1CashReceivedCycles: 0,
    slot2TechSentCycles: 1,
    slot2TechReceivedCycles: 0,
    slot2CashSentCycles: 0,
    slot2CashReceivedCycles: 1,
    health: DealHealth.OnTrack,
    deals: 30,
  },
  {
    slot1TechSentCycles: 1,
    slot1TechReceivedCycles: 0,
    slot1CashSentCycles: 0,
    slot1CashReceivedCycles: 1,
    slot2TechSentCycles: 0,
    slot2TechReceivedCycles: 1,
    slot2CashSentCycles: 1,
    slot2CashReceivedCycles: 0,
    health: DealHealth.AtRisk,
    deals: 6,
  },
]

const markets = buildSlotMarkets(configs, dealHealth)
const breakdown = getOpenSlotBreakdown(configs, markets.markets)

describe('SummaryCards', () => {
  it('show slot use, deal health, the wait queue and flags', async () => {
    const wrapper = await mountInApp(SummaryCards, {
      summary: {
        totalAccounts: 22,
        activeDeals: 36,
        openFlags: 3,
        flaggedAccounts: 1,
      },
      markets: markets.markets,
      otherDeals: markets.otherDeals,
      breakdown,
    })

    expect(wrapper.findAll('.text-3xl').map((f) => f.text())).toEqual([
      '84%',
      '83%',
      '12',
      '1',
    ])
    expect(wrapper.text()).toContain('72 of 86 slots, across 22 accounts')
    expect(wrapper.text()).toContain('36 active · 6 at risk · 0 stalled')
    expect(wrapper.text()).toContain('plus 2 on flagged accounts')
    expect(wrapper.find('.text-red-600').text()).toBe('1')
  })

  it('show zeros before the dashboard loads', async () => {
    const empty = buildSlotMarkets(undefined, undefined)
    const wrapper = await mountInApp(SummaryCards, {
      summary: undefined,
      markets: empty.markets,
      otherDeals: empty.otherDeals,
      breakdown: getOpenSlotBreakdown(undefined, []),
    })

    expect(wrapper.findAll('.text-3xl').map((f) => f.text())).toEqual([
      '0%',
      '0%',
      '0',
      '0',
    ])
    expect(wrapper.find('.text-red-600').exists()).toBe(false)
  })
})

describe('SlotMarkets', () => {
  it('shows each market with what it has room for', async () => {
    const wrapper = await mountInApp(SlotMarkets, { markets: markets.markets })

    const rows = wrapper.findAll('[data-market]').map((row) => row.text())
    expect(rows).toHaveLength(2)
    expect(rows[0]).toContain('Room for 4 Buyer slots')
    expect(rows[0]).toContain('4 unpaired')
    expect(rows[0]).toContain('Buyer · Sends $9M, gets 100 tech')
    expect(rows[0]).toContain('Seller · Sends 100 tech, gets $9M')
    expect(rows[0]).toContain('6 open on 5 accounts')
    expect(rows[0]).toContain('2 on flagged accounts')
    expect(rows[1]).toContain('Never matched')
    expect(rows[1]).toContain('No aid · Sends and gets nothing')
  })

  it('says so when there are no slots', async () => {
    const wrapper = await mountInApp(SlotMarkets, { markets: [] })

    expect(wrapper.text()).toContain('No slots yet.')
  })
})

describe('OpenSlotBreakdown', () => {
  it('reconciles open slots with the wait queue', async () => {
    const wrapper = await mountInApp(OpenSlotBreakdown, { breakdown })

    expect(wrapper.text()).toContain(
      '14 open slots. The wait queue counts 12 of them',
    )
    expect(wrapper.find('[role="img"]').attributes('aria-label')).toBe(
      'Waiting for partners: 4, Unpaired: 4, Never matched: 4, On flagged accounts: 2',
    )
  })
})

describe('DealHealthByPairing', () => {
  it('shows health per pairing', async () => {
    const wrapper = await mountInApp(DealHealthByPairing, {
      markets: markets.markets,
      otherDeals: markets.otherDeals,
    })

    const rows = wrapper.findAll('[data-pairing]').map((row) => row.text())
    expect(rows).toHaveLength(1)
    expect(rows[0]).toContain('Buyer ↔ Seller')
    expect(rows[0]).toContain('36 deals')
    expect(rows[0]).toContain('30 on track · 6 at risk')
  })

  it('adds manual deals between mismatched slots as other pairings', async () => {
    const wrapper = await mountInApp(DealHealthByPairing, {
      markets: [],
      otherDeals: {
        [DealHealth.OnTrack]: 0,
        [DealHealth.Starting]: 2,
        [DealHealth.Stalled]: 0,
        [DealHealth.AtRisk]: 0,
      },
    })

    expect(wrapper.find('[data-pairing]').text()).toContain('Other pairings')
  })

  it('says so when there are no deals', async () => {
    const empty = buildSlotMarkets(undefined, undefined)
    const wrapper = await mountInApp(DealHealthByPairing, {
      markets: empty.markets,
      otherDeals: empty.otherDeals,
    })

    expect(wrapper.text()).toContain('No active deals.')
  })
})

describe('DealActivity', () => {
  it('charts legs sent and failed per CN day, with totals', async () => {
    const wrapper = await mountInApp(DealActivity, {
      days: 14,
      data: [
        {
          day: '2026-10-08T00:00:00',
          legsSent: 3,
          legsFailed: 1,
          dealsCreated: 2,
          dealsCancelled: 1,
        },
        {
          day: '2026-10-09T00:00:00',
          legsSent: 5,
          legsFailed: 0,
          dealsCreated: 0,
          dealsCancelled: 0,
        },
      ],
    })

    const data = JSON.parse(
      wrapper.find('[data-chart]').attributes('data-chart-data')!,
    )
    expect(data.labels).toEqual(['Oct-08', 'Oct-09'])
    expect(data.datasets.map((d: { data: number[] }) => d.data)).toEqual([
      [3, 5],
      [1, 0],
    ])
    expect(wrapper.text()).toContain(
      '2 deals created · 1 cancelled · 1 of 8 legs failed',
    )
  })
})

describe('CancellationReasons', () => {
  it('lists reasons with their counts', async () => {
    const wrapper = await mountInApp(CancellationReasons, {
      days: 30,
      data: [
        { reason: 'Nation deleted', deals: 3 },
        { reason: 'Cancelled by an admin', deals: 1 },
      ],
    })

    expect(wrapper.text()).toContain('4 deals cancelled.')
    expect(wrapper.findAll('[data-reason]').map((r) => r.text())).toEqual([
      'Nation deleted3',
      'Cancelled by an admin1',
    ])
  })

  it('says so when nothing was cancelled', async () => {
    const wrapper = await mountInApp(CancellationReasons, {
      days: 30,
      data: undefined,
    })

    expect(wrapper.text()).toContain('No deals were cancelled.')
  })
})
