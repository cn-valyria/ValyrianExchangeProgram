import { describeAid } from './deals'
import {
  DealHealth,
  type AllianceDealHealth,
  type AllianceSlotConfigSummary,
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

// Status colors, always shown with the bucket's label
export const DEAL_HEALTH_COLORS: Record<DealHealth, string> = {
  [DealHealth.OnTrack]: 'bg-green-500',
  [DealHealth.Starting]: 'bg-gray-400 dark:bg-gray-500',
  [DealHealth.Stalled]: 'bg-amber-500',
  [DealHealth.AtRisk]: 'bg-red-500',
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

// The market a deal's pairing belongs to, or null for a manual deal between slots that aren't exact inverses
const pairingMarketKey = (row: DealHealthByPairing): string | null => {
  const [slot1, slot2] = pairingCycles(row)
  return configKey(inverseCycles(slot1)) === configKey(slot2)
    ? marketKey(slot1)
    : null
}

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
    const key = pairingMarketKey(row)
    const market = key === null ? undefined : markets.get(key)
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

// One market as an alliance uses it: its own slots on each side, and the health of its deals there
export interface AllianceMarket {
  key: string
  label: string
  terms: string
  sides: SlotMarketSide[]
  health: Record<DealHealth, number>
  activeDeals: number
}

export interface AllianceSummary {
  allianceName: string
  accounts: number
  totalSlots: number
  slotsInDeals: number
  openSlots: number
  flaggedOpenSlots: number
  // Each deal once, even when both accounts are in this alliance
  activeDeals: number
  withinAllianceDeals: number
  health: Record<DealHealth, number>
  // Largest first. Manual deals between mismatched slots are grouped under OTHER_PAIRINGS_KEY
  markets: AllianceMarket[]
}

export const OTHER_PAIRINGS_KEY = 'other'

// What a market is called, e.g. "Buyer ↔ Seller"
export const marketLabel = (market: SlotMarket): string =>
  market.second
    ? `${market.first.name} ↔ ${market.second.name}`
    : market.first.name

// Splits slots and deals by alliance, naming each market the way the program-wide markets do
export function buildAllianceSummaries(
  configs: AllianceSlotConfigSummary[] | undefined,
  dealHealth: AllianceDealHealth[] | undefined,
  programMarkets: SlotMarket[],
): AllianceSummary[] {
  const programByKey = new Map(programMarkets.map((m) => [m.key, m]))
  const alliances = new Map<string, AllianceSummary>()

  const allianceFor = (name: string, accounts = 0): AllianceSummary => {
    let alliance = alliances.get(name)
    if (!alliance) {
      alliance = {
        allianceName: name,
        accounts,
        totalSlots: 0,
        slotsInDeals: 0,
        openSlots: 0,
        flaggedOpenSlots: 0,
        activeDeals: 0,
        withinAllianceDeals: 0,
        health: emptyHealth(),
        markets: [],
      }
      alliances.set(name, alliance)
    }
    return alliance
  }

  const marketFor = (
    alliance: AllianceSummary,
    key: string,
  ): AllianceMarket => {
    let market = alliance.markets.find((m) => m.key === key)
    if (!market) {
      const program = programByKey.get(key)
      market = {
        key,
        label: program ? marketLabel(program) : 'Other pairings',
        terms: program
          ? program.first.terms
          : "Manual deals between slots that aren't exact opposites",
        sides: [],
        health: emptyHealth(),
        activeDeals: 0,
      }
      alliance.markets.push(market)
    }
    return market
  }

  for (const config of configs ?? []) {
    const alliance = allianceFor(config.allianceName, config.allianceAccounts)
    alliance.totalSlots += config.totalSlots
    alliance.slotsInDeals += config.slotsInDeals
    alliance.openSlots += config.openSlots
    alliance.flaggedOpenSlots += config.flaggedOpenSlots
    const market = marketFor(alliance, marketKey(config))
    market.sides.push(side(config))
    // The side that sends first stays first, as in the program-wide markets
    market.sides.sort((a, b) => leadOrder(b.summary) - leadOrder(a.summary))
  }

  for (const row of dealHealth ?? []) {
    const alliance = allianceFor(row.allianceName)
    const market = marketFor(
      alliance,
      pairingMarketKey(row) ?? OTHER_PAIRINGS_KEY,
    )
    for (const target of [alliance, market]) {
      target.health[row.health] += row.deals
      target.activeDeals += row.deals
    }
    if (row.withinAlliance) alliance.withinAllianceDeals += row.deals
  }

  const marketSlots = (m: AllianceMarket): number =>
    m.sides.reduce((sum, s) => sum + s.summary.totalSlots, 0)
  for (const alliance of alliances.values()) {
    alliance.markets.sort(
      (a, b) =>
        marketSlots(b) - marketSlots(a) || b.activeDeals - a.activeDeals,
    )
  }

  return [...alliances.values()].sort(
    (a, b) =>
      b.totalSlots - a.totalSlots ||
      a.allianceName.localeCompare(b.allianceName),
  )
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
