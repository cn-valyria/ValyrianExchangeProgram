import { AID_PACKAGE } from './constants'
import {
  DealActivityState,
  DealBlockedReason,
  DealExtensionKind,
  type AccountSimplified,
  type DealDetail,
  type SlotDetail,
} from '~/stores/account'
import type { AdminDeal } from '~/stores/adminDeals'
import type { NationSimplified } from '~/stores/shared/nationSimplified'

export type BadgeColor = 'error' | 'warning' | 'info' | 'success' | 'neutral'

export interface DealStatusInfo {
  label: string
  color: BadgeColor
  detail: string
}

export type DealActionKind =
  | 'send'
  | 'accept'
  | 'freeSlot'
  | 'outOfTurn'
  | 'receive'

export interface DealAction {
  deal: DealDetail
  kind: DealActionKind
  // True when this account has an open warning on the deal
  flagged: boolean
  title: string
  // How the partner relates to the action, e.g. "to" in "Send $9M to <partner>"
  preposition: 'to' | 'from' | 'for'
  detail: string
}

// Most urgent first, after flagged deals
const ACTION_ORDER: DealActionKind[] = [
  'send',
  'accept',
  'freeSlot',
  'outOfTurn',
  'receive',
]

const formatDate = (date: string | null): string =>
  date ? date.split('T')[0] : 'soon'

const plural = (count: number, word: string): string =>
  `${count} ${word}${count === 1 ? '' : 's'}`

// Warnings are only cleared by the daily flag check, so one can briefly outlive the problem
// (e.g. aid was sent since). Only count it while the account is still holding up the deal
export const isFlagged = (deal: DealDetail): boolean =>
  deal.flaggedOn !== null && deal.iAmResponsible

// Turns a slot's cycle counts into text like "100 tech + $9M"
export function describeAid(techCycles: number, cashCycles: number): string {
  const parts: string[] = []
  if (techCycles > 0) parts.push(`${techCycles * AID_PACKAGE.tech} tech`)
  if (cashCycles > 0) parts.push(`$${cashCycles * AID_PACKAGE.cashMillions}M`)
  return parts.length > 0 ? parts.join(' + ') : 'Nothing'
}

// What one aid offer from this slot holds, e.g. "$9M" or "100 tech"
export function describeLeg(senderSlot: SlotDetail): string {
  const parts: string[] = []
  if (senderSlot.cashSentCycles > 0) parts.push(`$${AID_PACKAGE.cashMillions}M`)
  if (senderSlot.techSentCycles > 0) parts.push(`${AID_PACKAGE.tech} tech`)
  return parts.length > 0 ? parts.join(' or ') : 'aid'
}

// Who sends next and what, e.g. "You send $9M"
export function describeNextAid(deal: DealDetail): string {
  if (deal.state === null) return 'Not known yet'
  return deal.isMyTurnToSend
    ? `You send ${describeLeg(deal.mySlot)}`
    : `Partner sends ${describeLeg(deal.partnerSlot)}`
}

export function getAidFormLink(
  partner: NationSimplified,
  myNationId: number,
): string {
  return `https://www.cybernations.net/aid_form.asp?Nation_ID=${partner.nationId}&bynation=${myNationId}`
}

// CN's page listing the signed-in nation's pending aid offers, where they're accepted
export const AID_INFORMATION_LINK =
  'https://www.cybernations.net/aid_information.asp'

export function getNationLink(nation: NationSimplified): string {
  return `https://www.cybernations.net/nation_drill_display.asp?Nation_ID=${nation.nationId}`
}

// The status badge for one of the account's own deals
export function getDealStatus(deal: DealDetail): DealStatusInfo {
  if (deal.state === null) {
    return {
      label: 'New',
      color: 'info',
      detail: "This deal's status will show after VEP's next daily update.",
    }
  }

  if (isFlagged(deal)) {
    return {
      label: 'Flagged',
      color: 'error',
      detail: `You're holding up this deal. It will be cancelled on ${formatDate(deal.cancelsOn)} unless it's fixed.`,
    }
  }

  const myLeg = describeLeg(deal.mySlot)
  const partnerLeg = describeLeg(deal.partnerSlot)

  switch (deal.state) {
    case DealActivityState.PendingStart:
      return {
        label: 'Starting',
        color: 'neutral',
        detail:
          'Waiting until you and your partner both have a free aid slot. Nothing to do yet.',
      }

    case DealActivityState.AwaitingSend:
      if (deal.isMyTurnToSend) {
        return {
          label: 'Your Turn',
          color: 'warning',
          detail: `Send ${myLeg} to your partner.`,
        }
      }
      if (deal.partnerIsResponsible && (deal.daysResponsible ?? 0) >= 1) {
        return {
          label: 'Partner Late',
          color: 'warning',
          detail: `Your partner is ${plural(deal.daysResponsible ?? 0, 'day')} late sending ${partnerLeg}.`,
        }
      }
      return {
        label: "Partner's Turn",
        color: 'info',
        detail: `Your partner sends ${partnerLeg} next. Keep an aid slot free.`,
      }

    case DealActivityState.InFlight:
      if (!deal.inFlightAccepted && deal.inFlightIsIncoming) {
        return {
          label: 'Accept Aid',
          color: 'warning',
          detail: `Accept your partner's offer before it expires on ${formatDate(deal.inFlightUntil)}.`,
        }
      }
      if (!deal.inFlightAccepted) {
        return {
          label: 'Awaiting Acceptance',
          color: 'info',
          detail: `Waiting for your partner to accept your offer. It expires on ${formatDate(deal.inFlightUntil)}.`,
        }
      }
      return {
        label: 'In Progress',
        color: 'success',
        detail: `Aid is in progress until ${formatDate(deal.inFlightUntil)}. Next: ${describeNextAid(deal).toLowerCase()}.`,
      }

    case DealActivityState.Blocked:
      if (deal.iAmResponsible) {
        return {
          label: 'Blocked by You',
          color: 'error',
          detail: getBlockedDetail(deal),
        }
      }
      return {
        label: 'Blocked by Partner',
        color: 'neutral',
        detail:
          deal.blockedReason === DealBlockedReason.OutOfSequenceAid
            ? `Your partner sent aid out of turn, which blocks the deal until ${formatDate(deal.blockedUntil)}.`
            : "Your partner's aid slots are full of aid outside VEP, so the deal can't continue until they free one.",
      }
  }
}

// What this account needs to do today for a deal, or null if nothing
export function getDealAction(deal: DealDetail): DealAction | null {
  const partner = deal.partnerSlot.account.rulerName
  const myLeg = describeLeg(deal.mySlot)
  const partnerLeg = describeLeg(deal.partnerSlot)
  const flagged = isFlagged(deal)

  switch (deal.state) {
    case DealActivityState.AwaitingSend:
      if (deal.isMyTurnToSend) {
        return {
          deal,
          kind: 'send',
          flagged,
          title: `Send ${myLeg}`,
          preposition: 'to',
          detail: `It's your turn to send. ${getDeadlineDetail(deal)}`,
        }
      }
      return {
        deal,
        kind: 'receive',
        flagged,
        title: `Expect ${partnerLeg}`,
        preposition: 'from',
        detail:
          deal.partnerIsResponsible && (deal.daysResponsible ?? 0) >= 1
            ? `${partner} is ${plural(deal.daysResponsible ?? 0, 'day')} late sending. Keep an aid slot free, and reach out to them if you haven't heard from them.`
            : `It's ${partner}'s turn to send. Keep an aid slot free and accept the offer when it arrives.`,
      }

    case DealActivityState.InFlight:
      if (deal.inFlightIsIncoming && !deal.inFlightAccepted) {
        return {
          deal,
          kind: 'accept',
          flagged,
          title: `Accept ${partnerLeg}`,
          preposition: 'from',
          detail: `${partner}'s offer is waiting for you in-game. If it expires on ${formatDate(deal.inFlightUntil)} without being accepted, the deal is cancelled and both of you are flagged.`,
        }
      }
      return null

    case DealActivityState.Blocked:
      if (!deal.iAmResponsible) return null
      return {
        deal,
        kind:
          deal.blockedReason === DealBlockedReason.OutOfSequenceAid
            ? 'outOfTurn'
            : 'freeSlot',
        flagged,
        title:
          deal.blockedReason === DealBlockedReason.OutOfSequenceAid
            ? 'Aid sent out of turn'
            : 'Free an aid slot',
        preposition:
          deal.blockedReason === DealBlockedReason.OutOfSequenceAid
            ? 'to'
            : 'for',
        detail: `${getBlockedDetail(deal)} ${getDeadlineDetail(deal)}`,
      }

    default:
      return null
  }
}

// Every deal needing action today, flagged deals first
export function getDealActions(deals: DealDetail[]): DealAction[] {
  return deals
    .map(getDealAction)
    .filter((action): action is DealAction => action !== null)
    .sort(
      (a, b) =>
        Number(b.flagged) - Number(a.flagged) ||
        ACTION_ORDER.indexOf(a.kind) - ACTION_ORDER.indexOf(b.kind) ||
        a.deal.mySlot.slotNumber - b.deal.mySlot.slotNumber,
    )
}

// Why this account is blocking a deal, and how to fix it
function getBlockedDetail(deal: DealDetail): string {
  const partner = deal.partnerSlot.account.rulerName
  switch (deal.blockedReason) {
    case DealBlockedReason.OutOfSequenceAid:
      return `It's ${partner}'s turn to send ${describeLeg(deal.partnerSlot)}, but your aid blocks them until ${formatDate(deal.blockedUntil)}. If it hasn't been accepted yet, cancel it so they can send.`
    case DealBlockedReason.ReceiverSlotsFull:
      return `Your in-game aid slots are all taken by aid outside VEP, so ${partner} can't send you ${describeLeg(deal.partnerSlot)}.`
    default:
      return `Your in-game aid slots are all taken by aid outside VEP, so you can't send ${describeLeg(deal.mySlot)}.`
  }
}

// What happens if this account keeps holding up the deal
function getDeadlineDetail(deal: DealDetail): string {
  if (isFlagged(deal)) {
    return `This deal is flagged and will be cancelled on ${formatDate(deal.cancelsOn)}.`
  }
  return 'Do this today to keep the deal from being flagged.'
}

// When a held-up deal is cancelled, with any extension an admin gave, e.g. "cancels 2026-10-18 (extended by Bob)"
export function describeAdminDeadline(deal: AdminDeal): string {
  const by = `extended by ${deal.extendedBy ?? 'an admin'}`
  switch (deal.extensionKind) {
    case DealExtensionKind.UntilNextLeg:
      return `no deadline until the next aid (${by})`
    case DealExtensionKind.FiveDays:
      return `cancels ${formatDate(deal.cancelsOn)} (${by})`
    default:
      return `cancels ${formatDate(deal.cancelsOn)}`
  }
}

// The status badge for a deal on the admin deals page
export function getAdminDealStatus(deal: AdminDeal): DealStatusInfo {
  const accountName = (accountId: number | null): string => {
    const account: AccountSimplified | undefined = [
      deal.slot1.account,
      deal.slot2.account,
    ].find((a) => a.accountId === accountId)
    return account?.rulerName ?? 'Unknown'
  }

  if (deal.state === null) {
    return {
      label: 'New',
      color: 'info',
      detail: 'Status will show after the next data loader run.',
    }
  }

  const daysResponsible = deal.daysResponsible ?? 0
  const heldUp =
    deal.responsibleAccountId !== null
      ? ` ${accountName(deal.responsibleAccountId)} has held it up for ${plural(daysResponsible, 'day')}; ${describeAdminDeadline(deal)}.`
      : ''
  const heldUpColor: BadgeColor = daysResponsible >= 1 ? 'error' : 'warning'

  switch (deal.state) {
    case DealActivityState.PendingStart:
      return {
        label: 'Starting',
        color: 'neutral',
        detail: 'Waiting until both nations have a free aid slot.',
      }
    case DealActivityState.InFlight:
      return {
        label: 'In Progress',
        color: 'success',
        detail: `Aid in flight until ${formatDate(deal.inFlightUntil)}. ${accountName(deal.nextSenderAccountId)} sends next.`,
      }
    case DealActivityState.AwaitingSend:
      return {
        label: 'Awaiting Send',
        color: heldUpColor,
        detail: `${accountName(deal.nextSenderAccountId)} should send.${heldUp}`,
      }
    case DealActivityState.Blocked: {
      const reason =
        deal.blockedReason === DealBlockedReason.OutOfSequenceAid
          ? 'Aid sent out of turn.'
          : 'Aid slots full of non-VEP aid.'
      return {
        label: 'Blocked',
        color: heldUpColor,
        detail: `${reason}${heldUp}`,
      }
    }
  }
}
