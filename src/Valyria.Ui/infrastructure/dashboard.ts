import { describeAid } from './deals'
import {
  DealHealth,
  type DealHealthByPairing,
  type SlotConfigSummary,
  type SlotCycles,
} from '~/stores/admin'

// A slot config and the slots it can be matched with. Matching only pairs slots whose cycles are exact
// inverses, so each market is its own pool: open Buyer slots can't help a shortage of 1:2 Sellers
export interface SlotMarketSide {
  summary: SlotConfigSummary
  name: string
  terms: string
}

export interface SlotMarket {
  key: string
  // The side that sends first in a deal (the cash sender, if either side sends cash)
  first: SlotMarketSide
  // The inverse config. Null when a config is its own inverse, so it pairs within itself
  second: SlotMarketSide | null
  // False when nothing could ever pair with these slots
  matchable: boolean
  totalSlots: number
  // Open slots on both sides that matching didn't pair, counting both sides. Usually the only open
  // accounts on each side already share a deal, or these slots were paired before
  unpairedSlots: number
  // Open slots on the side with more of them, beyond what the other side's open slots can take
  surplus: number
  surplusSide: 'first' | 'second' | null
  health: Record<DealHealth, number>
  activeDeals: number
}

export interface SlotMarkets {
  markets: SlotMarket[]
  // Active deals between slots that aren't exact inverses (only possible for manual deals)
  otherDeals: Record<DealHealth, number>
}

// Where every open slot stands. waiting + unpaired + unmatchable is what the wait queue counts
export interface OpenSlotBreakdown {
  waiting: number
  unpaired: number
  unmatchable: number
  flagged: number
  total: number
  waitQueue: number
}

export const DEAL_HEALTH_ORDER = [
  DealHealth.OnTrack,
  DealHealth.Starting,
  DealHealth.Stalled,
  DealHealth.AtRisk,
]

export const DEAL_HEALTH_INFO: Record<
  DealHealth,
  { label: string; detail: string }
> = {
  [DealHealth.OnTrack]: {
    label: 'On track',
    detail: "Aid is in flight, or it's the sender's first day to send",
  },
  [DealHealth.Starting]: {
    label: 'Starting',
    detail:
      'New, or waiting less than 5 days for both nations to have a free aid slot',
  },
  [DealHealth.Stalled]: {
    label: 'Stalled',
    detail:
      'Waiting 5+ days for a free aid slot on both nations. Nobody is flagged, but no aid is moving',
  },
  [DealHealth.AtRisk]: {
    label: 'At risk',
    detail:
      'Blocked, or held up by one side for a day or more. Cancelled after 5 days',
  },
}

const emptyHealth = (): Record<DealHealth, number> => ({
  [DealHealth.OnTrack]: 0,
  [DealHealth.Starting]: 0,
  [DealHealth.Stalled]: 0,
  [DealHealth.AtRisk]: 0,
})

const configKey = (c: SlotCycles): string =>
  [
    c.techSentCycles,
    c.techReceivedCycles,
    c.cashSentCycles,
    c.cashReceivedCycles,
  ].join('-')

export const inverseCycles = (c: SlotCycles): SlotCycles => ({
  techSentCycles: c.techReceivedCycles,
  techReceivedCycles: c.techSentCycles,
  cashSentCycles: c.cashReceivedCycles,
  cashReceivedCycles: c.cashSentCycles,
})

const isEmpty = (c: SlotCycles): boolean =>
  c.techSentCycles +
    c.techReceivedCycles +
    c.cashSentCycles +
    c.cashReceivedCycles ===
  0

// Positive when this config sends first in its market, negative when its inverse does, 0 when it's its own inverse
const leadOrder = (c: SlotCycles): number =>
  c.cashSentCycles - c.cashReceivedCycles ||
  c.techSentCycles - c.techReceivedCycles

const marketKey = (c: SlotCycles): string =>
  configKey(leadOrder(c) >= 0 ? c : inverseCycles(c))

// The role whose default slots look like this, e.g. "Buyer". Edited slots keep the closest role name
export function slotConfigName(c: SlotCycles): string {
  const sendsCash = c.cashSentCycles > 0
  const sendsTech = c.techSentCycles > 0
  const getsCash = c.cashReceivedCycles > 0
  const getsTech = c.techReceivedCycles > 0
  if (isEmpty(c)) return 'No aid'
  if (sendsCash && getsTech && !sendsTech && !getsCash) return 'Buyer'
  if (sendsTech && getsCash && !sendsCash && !getsTech) return 'Seller'
  if (sendsCash && !sendsTech && !getsCash && !getsTech) return 'Cash Donor'
  if (getsCash && !sendsCash && !sendsTech && !getsTech) return 'Cash Collector'
  if (sendsTech && !sendsCash && !getsCash && !getsTech) return 'Tech Farm'
  if (getsTech && !sendsCash && !sendsTech && !getsCash) return 'Tech Receiver'
  return 'Custom'
}

// What a slot sends and gets per 10 days, e.g. "Sends $9M, gets 200 tech"
export function slotConfigTerms(c: SlotCycles): string {
  if (isEmpty(c)) return 'Sends and gets nothing'
  const parts: string[] = []
  if (c.techSentCycles + c.cashSentCycles > 0) {
    parts.push(`sends ${describeAid(c.techSentCycles, c.cashSentCycles)}`)
  }
  if (c.techReceivedCycles + c.cashReceivedCycles > 0) {
    parts.push(
      `gets ${describeAid(c.techReceivedCycles, c.cashReceivedCycles)}`,
    )
  }
  const terms = parts.join(', ')
  return terms.charAt(0).toUpperCase() + terms.slice(1)
}

const side = (summary: SlotConfigSummary): SlotMarketSide => ({
  summary,
  name: slotConfigName(summary),
  terms: slotConfigTerms(summary),
})

// A config nobody has, so a market can show the side it's missing
const noSlots = (cycles: SlotCycles): SlotConfigSummary => ({
  ...cycles,
  accounts: 0,
  totalSlots: 0,
  slotsInDeals: 0,
  openSlots: 0,
  openAccounts: 0,
  flaggedOpenSlots: 0,
})

const pairingCycles = (row: DealHealthByPairing): [SlotCycles, SlotCycles] => [
  {
    techSentCycles: row.slot1TechSentCycles,
    techReceivedCycles: row.slot1TechReceivedCycles,
    cashSentCycles: row.slot1CashSentCycles,
    cashReceivedCycles: row.slot1CashReceivedCycles,
  },
  {
    techSentCycles: row.slot2TechSentCycles,
    techReceivedCycles: row.slot2TechReceivedCycles,
    cashSentCycles: row.slot2CashSentCycles,
    cashReceivedCycles: row.slot2CashReceivedCycles,
  },
]

// Groups slot configs into markets (a config and its inverse), largest first, unmatchable ones last
export function buildSlotMarkets(
  configs: SlotConfigSummary[] | undefined,
  dealHealth: DealHealthByPairing[] | undefined,
): SlotMarkets {
  const byKey = new Map((configs ?? []).map((c) => [configKey(c), c]))
  const markets = new Map<string, SlotMarket>()

  for (const config of configs ?? []) {
    const key = marketKey(config)
    if (markets.has(key)) continue

    const order = leadOrder(config)
    const inverse = inverseCycles(config)
    const inverseSummary = byKey.get(configKey(inverse)) ?? noSlots(inverse)
    const [first, second] =
      order >= 0 ? [config, inverseSummary] : [inverseSummary, config]
    const symmetric = order === 0
    const matchable =
      !isEmpty(config) &&
      first.totalSlots > 0 &&
      second.totalSlots > 0 &&
      // A config that is its own inverse needs two different accounts
      (!symmetric || first.accounts > 1)

    let unpairedSlots = 0
    let surplus = 0
    let surplusSide: SlotMarket['surplusSide'] = null
    if (matchable && symmetric) {
      unpairedSlots = first.openAccounts > 1 ? first.openSlots : 0
      surplus = first.openSlots - unpairedSlots
      surplusSide = surplus > 0 ? 'first' : null
    } else if (matchable) {
      unpairedSlots = 2 * Math.min(first.openSlots, second.openSlots)
      surplus = Math.abs(first.openSlots - second.openSlots)
      surplusSide =
        surplus === 0
          ? null
          : first.openSlots > second.openSlots
            ? 'first'
            : 'second'
    }

    markets.set(key, {
      key,
      first: side(first),
      second: symmetric ? null : side(second),
      matchable,
      totalSlots: first.totalSlots + (symmetric ? 0 : second.totalSlots),
      unpairedSlots,
      surplus,
      surplusSide,
      health: emptyHealth(),
      activeDeals: 0,
    })
  }

  const otherDeals = emptyHealth()
  for (const row of dealHealth ?? []) {
    const [slot1, slot2] = pairingCycles(row)
    const market =
      configKey(inverseCycles(slot1)) === configKey(slot2)
        ? markets.get(marketKey(slot1))
        : undefined
    if (market) {
      market.health[row.health] += row.deals
      market.activeDeals += row.deals
    } else {
      otherDeals[row.health] += row.deals
    }
  }

  return {
    markets: [...markets.values()].sort(
      (a, b) =>
        Number(b.matchable) - Number(a.matchable) ||
        b.totalSlots - a.totalSlots,
    ),
    otherDeals,
  }
}

export function getOpenSlotBreakdown(
  configs: SlotConfigSummary[] | undefined,
  markets: SlotMarket[],
): OpenSlotBreakdown {
  const flagged = (configs ?? []).reduce(
    (sum, c) => sum + c.flaggedOpenSlots,
    0,
  )
  let waiting = 0
  let unpaired = 0
  let unmatchable = 0
  for (const market of markets) {
    if (market.matchable) {
      waiting += market.surplus
      unpaired += market.unpairedSlots
    } else {
      unmatchable +=
        market.first.summary.openSlots + (market.second?.summary.openSlots ?? 0)
    }
  }
  const waitQueue = waiting + unpaired + unmatchable
  return {
    waiting,
    unpaired,
    unmatchable,
    flagged,
    total: waitQueue + flagged,
    waitQueue,
  }
}

export const sumHealth = (
  healths: Record<DealHealth, number>[],
): Record<DealHealth, number> => {
  const total = emptyHealth()
  for (const health of healths) {
    for (const bucket of DEAL_HEALTH_ORDER) total[bucket] += health[bucket]
  }
  return total
}

const plural = (count: number, word: string): string =>
  `${count} ${word}${count === 1 ? '' : 's'}`

// What a market needs, in recruiting terms, e.g. "Room for 4 more Buyer slots"
export function getMarketVerdict(market: SlotMarket): {
  label: string
  detail: string
  color: 'success' | 'warning' | 'neutral'
} {
  const { first, second } = market
  if (!market.matchable) {
    return {
      label: 'Never matched',
      color: 'neutral',
      detail: isEmpty(first.summary)
        ? 'These slots send and get nothing. Set their cycles to match them.'
        : second === null
          ? 'Only one account has these slots, and an account is never matched with itself.'
          : `No slot is set up the opposite way (${second.terms}).`,
    }
  }

  if (market.surplusSide === null) {
    return {
      label: 'Balanced',
      color: 'success',
      detail: 'Every open slot here has a partner waiting.',
    }
  }

  // Open slots on the surplus side are what new accounts on the other side would be matched with
  const [waitingSide, neededSide] =
    market.surplusSide === 'first' ? [first, second ?? first] : [second!, first]
  const accounts = waitingSide.summary.openAccounts
  return {
    label: `Room for ${plural(market.surplus, `${neededSide.name} slot`)}`,
    color: 'warning',
    detail:
      `${plural(market.surplus, `${waitingSide.name} slot`)} ${market.surplus === 1 ? 'is' : 'are'} waiting, on ${plural(accounts, 'account')}. ` +
      `A new ${neededSide.name} can be matched with each of those accounts once, so up to ${Math.min(accounts, market.surplus)} deals per new account.`,
  }
}
