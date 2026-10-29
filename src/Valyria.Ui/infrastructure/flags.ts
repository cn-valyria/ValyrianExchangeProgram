import { accountRoles } from './dataLists'
import { describeLeg, type BadgeColor } from './deals'
import {
  DealLegOutcome,
  DealStatus,
  type AccountSimplified,
} from '~/stores/account'
import {
  DealFlagReason,
  DealFlagType,
  type DealFlag,
  type FlagDealLeg,
  type FlagSlotAid,
} from '~/stores/adminDeals'

// How the flags page groups flags: the deal is still running, or the flag already ended it
export type FlagDealView = 'all' | 'active' | 'cancelled'

export type FlagResolution =
  | 'extendFiveDays'
  | 'extendUntilNextLeg'
  | 'clear'
  | 'clearBoth'
  | 'restore'
  | 'cancelDeal'

export interface FlagResolutionOption {
  value: FlagResolution
  label: string
  description: string
  // Set when the option can't be used, saying why
  disabledReason: string | null
}

export interface FlagOutlook {
  title: string
  detail: string
  color: BadgeColor
}

export interface LabelInfo {
  label: string
  color: BadgeColor
}

// Who flag text is written for: admins, who see the flagged account by name, or the flagged account itself, as "you"
export type FlagVoice = 'admin' | 'flagged'

// How flag text refers to the flagged account. Names keep their own case, so sentence starts get their own words
interface FlaggedWords {
  // "Ruler1 didn't send it" / "You didn't send it"
  name: string
  // "to Ruler1" / "to you"
  nameMid: string
  // "Ruler1's aid slots" / "Your aid slots"
  possessive: string
  // "on Ruler1's side" / "on your side"
  possessiveMid: string
  // "They've held up" / "You've held up"
  pronoun: string
  // "so they couldn't" / "so you couldn't"
  pronounMid: string
  // "send to them" / "send to you"
  objectPronoun: string
  // "Their last aid" / "Your last aid"
  possessivePronoun: string
  // "Ruler1 was" / "You were"
  was: string
}

function getFlaggedWords(flag: DealFlag, voice: FlagVoice): FlaggedWords {
  if (voice === 'flagged') {
    return {
      name: 'You',
      nameMid: 'you',
      possessive: 'Your',
      possessiveMid: 'your',
      pronoun: 'You',
      pronounMid: 'you',
      objectPronoun: 'you',
      possessivePronoun: 'Your',
      was: 'were',
    }
  }
  const name = getAccountName(flag.flaggedAccount)
  return {
    name,
    nameMid: name,
    possessive: `${name}'s`,
    possessiveMid: `${name}'s`,
    pronoun: 'They',
    pronounMid: 'they',
    objectPronoun: 'them',
    possessivePronoun: 'Their',
    was: 'was',
  }
}

// Column layout of a flag row from md up, shared by the rows and their header on the flags page
export const FLAG_ROW_GRID =
  'md:grid md:grid-cols-[minmax(0,1.1fr)_minmax(0,1.4fr)_minmax(0,1fr)_minmax(0,1.3fr)_minmax(0,1.6fr)_6rem] md:gap-x-4'

const MS_PER_DAY = 24 * 60 * 60 * 1000

const formatDate = (date: string | null): string =>
  date ? date.split('T')[0] : '—'

const plural = (count: number, word: string): string =>
  `${count} ${word}${count === 1 ? '' : 's'}`

// Today's CN date (server time is UTC; CN runs on UTC-5), as "2026-10-09"
export function getCnToday(now: Date = new Date()): string {
  return new Date(now.getTime() - 5 * 60 * 60 * 1000).toISOString().slice(0, 10)
}

// Whole days from one CN date to another
function daysBetween(from: string, to: string): number {
  return Math.round(
    (Date.parse(formatDate(to)) - Date.parse(formatDate(from))) / MS_PER_DAY,
  )
}

// A CN date some days later, e.g. addDays('2026-10-13', 5) is '2026-10-18'
export function addDays(date: string, days: number): string {
  return new Date(Date.parse(formatDate(date)) + days * MS_PER_DAY)
    .toISOString()
    .slice(0, 10)
}

// An account's ruler, or a placeholder once its nation has been deleted
export function getAccountName(account: AccountSimplified): string {
  return account.rulerName ?? `Nation #${account.nationId} (deleted)`
}

export function getRoleName(code: string): string {
  return accountRoles.find((role) => role.code === code)?.description ?? code
}

export const isOnActiveDeal = (flag: DealFlag): boolean =>
  flag.dealStatus === DealStatus.Active

// The flag is on a running deal, and its account is still the one holding it up
export const isStillHoldingUpDeal = (flag: DealFlag): boolean =>
  isOnActiveDeal(flag) &&
  flag.responsibleAccountId === flag.flaggedAccount.accountId

export function getFlagTypeInfo(flag: DealFlag): LabelInfo {
  switch (flag.flagType) {
    case DealFlagType.FiveDayReplacement:
      return { label: '5-Day Cancellation', color: 'error' }
    case DealFlagType.AidFailed:
      return { label: 'Aid Failed', color: 'error' }
    default:
      return { label: '1-Day Warning', color: 'warning' }
  }
}

export function getFlagReasonLabel(
  flag: DealFlag,
  voice: FlagVoice = 'admin',
): string {
  switch (flag.flagReason) {
    case DealFlagReason.AwaitingSend:
      return voice === 'flagged'
        ? "Didn't send on your turn"
        : "Didn't send on their turn"
    case DealFlagReason.SenderSlotsFull:
      return 'Slots full (sender)'
    case DealFlagReason.ReceiverSlotsFull:
      return 'Slots full (receiver)'
    case DealFlagReason.OutOfSequenceAid:
      return 'Sent aid out of turn'
    case DealFlagReason.AidCancelled:
      return 'Aid offer cancelled'
    case DealFlagReason.AidExpired:
      return 'Aid offer expired unaccepted'
    default:
      return 'No reason recorded'
  }
}

// Whether the deal is running, and whether this account is still the problem
export function getDealStatusInfo(flag: DealFlag): LabelInfo {
  if (!isOnActiveDeal(flag)) {
    return {
      label: `Cancelled ${formatDate(flag.dealCancelledOn)}`,
      color: 'neutral',
    }
  }
  if (flag.state === null) return { label: 'Active (restored)', color: 'info' }
  if (isStillHoldingUpDeal(flag)) {
    return { label: 'Active, still held up', color: 'error' }
  }
  return { label: 'Active, back on track', color: 'success' }
}

// What VEP expected of the flagged account
export function describeExpectation(
  flag: DealFlag,
  voice: FlagVoice = 'admin',
): string {
  const flagged = getFlaggedWords(flag, voice)
  const partner = getAccountName(flag.innocentAccount)
  const theirLeg = describeLeg(flag.flaggedSlot)
  const partnerLeg = describeLeg(flag.innocentSlot)

  switch (flag.flagReason) {
    case DealFlagReason.AwaitingSend:
      return `It was ${flagged.possessiveMid} turn to send ${theirLeg} to ${partner}, and both nations had a free aid slot.`
    case DealFlagReason.SenderSlotsFull:
      return `It was ${flagged.possessiveMid} turn to send ${theirLeg} to ${partner}, which needs a free aid slot on ${flagged.possessiveMid} side.`
    case DealFlagReason.ReceiverSlotsFull:
      return `It was ${partner}'s turn to send ${partnerLeg} to ${flagged.nameMid}, which needs a free aid slot on ${flagged.possessiveMid} side.`
    case DealFlagReason.OutOfSequenceAid:
      return `It was ${partner}'s turn to send ${partnerLeg}. ${flagged.name} ${flagged.was} expected to wait for it, since two nations can only have one aid offer between them at a time.`
    case DealFlagReason.AidCancelled:
    case DealFlagReason.AidExpired:
      return `Each aid offer in the deal had to be accepted within 10 days of being sent, and not cancelled.`
    default:
      return 'This flag was raised before VEP 3.0 tracked expected aid, so there is no record of what was expected.'
  }
}

// What the flagged account did instead
export function describeWhatHappened(
  flag: DealFlag,
  voice: FlagVoice = 'admin',
): string {
  const flagged = getFlaggedWords(flag, voice)
  const partner = getAccountName(flag.innocentAccount)
  const lastSent = flag.flaggedLastSentOn
    ? `${flagged.possessivePronoun} last aid counted in this deal was sent on ${formatDate(flag.flaggedLastSentOn)}.`
    : `${flagged.pronoun} haven't had any aid counted in this deal yet.`
  const heldUpFor =
    isStillHoldingUpDeal(flag) && flag.daysResponsible !== null
      ? ` ${flagged.pronoun}'ve held up the deal for ${plural(flag.daysResponsible, 'day')}.`
      : ''

  switch (flag.flagReason) {
    case DealFlagReason.AwaitingSend:
      return `${flagged.name} didn't send it. ${lastSent}${heldUpFor}`
    case DealFlagReason.SenderSlotsFull:
      return `${flagged.possessive} aid slots were all taken, at least one by aid outside VEP sent after the deal started, so ${flagged.pronounMid} couldn't send it. ${lastSent}${heldUpFor}`
    case DealFlagReason.ReceiverSlotsFull:
      return `${flagged.possessive} aid slots were all taken, at least one by aid outside VEP sent after the deal started, so ${partner} couldn't send to ${flagged.objectPronoun}.${heldUpFor}`
    case DealFlagReason.OutOfSequenceAid: {
      const until =
        flag.blockedUntil !== null
          ? `, which blocks ${partner} until ${formatDate(flag.blockedUntil)}`
          : ''
      return `${flagged.name} sent aid to ${partner} out of turn${until}.${heldUpFor}`
    }
    case DealFlagReason.AidCancelled:
      return `An offer between ${flagged.nameMid} and ${partner} was cancelled before it was accepted. VEP can't tell which side caused it, so it flagged both accounts and cancelled the deal.`
    case DealFlagReason.AidExpired:
      return `An offer between ${flagged.nameMid} and ${partner} expired without being accepted. VEP can't tell which side caused it, so it flagged both accounts and cancelled the deal.`
    default:
      return `${flagged.name} ${flagged.was} flagged on ${formatDate(flag.flaggedOn)}.`
  }
}

// How the flagged account stops holding up the deal, which clears a warning by itself
function describeFix(flag: DealFlag): string {
  const flagged = getAccountName(flag.flaggedAccount)
  const partner = getAccountName(flag.innocentAccount)
  switch (flag.flagReason) {
    case DealFlagReason.SenderSlotsFull:
      return `It clears itself once ${flagged} sends the aid. Freeing a slot alone isn't enough, since it's still their turn.`
    case DealFlagReason.ReceiverSlotsFull:
      return `It clears itself once ${flagged} frees an aid slot for ${partner}'s aid.`
    case DealFlagReason.OutOfSequenceAid: {
      const cancelsFirst =
        flag.blockedUntil !== null &&
        flag.cancelsOn !== null &&
        daysBetween(flag.cancelsOn, flag.blockedUntil) >= 0
          ? ` The aid clears on ${formatDate(flag.blockedUntil)}, after the deal is due to be cancelled, so it needs cancelling to save the deal.`
          : ''
      return `It clears itself once the out-of-turn aid is cancelled (if it hasn't been accepted) or runs out.${cancelsFirst}`
    }
    default:
      return `It clears itself once ${flagged} sends the aid.`
  }
}

function describeMatchingHold(flag: DealFlag): string {
  const flagged = getAccountName(flag.flaggedAccount)
  const others = flag.flaggedAccountOpenFlagCount - 1
  return others > 0
    ? `${flagged} isn't matched with new partners until this and their ${plural(others, 'other open flag')} are resolved.`
    : `${flagged} isn't matched with new partners until it's resolved.`
}

// What happens to the flag if no admin touches it, and when
export function getFlagOutlook(
  flag: DealFlag,
  today: string = getCnToday(),
): FlagOutlook {
  const flagged = getAccountName(flag.flaggedAccount)
  const partner = getAccountName(flag.innocentAccount)

  if (flag.flagType === DealFlagType.OneDayInactive) {
    if (!isOnActiveDeal(flag)) {
      return {
        title: 'Clears itself at the next daily check',
        detail: `The deal has ended, so the warning no longer applies. ${describeMatchingHold(flag)}`,
        color: 'neutral',
      }
    }
    if (!isStillHoldingUpDeal(flag)) {
      return {
        title: 'Clears itself at the next daily check',
        detail: `${flagged} isn't holding up the deal any more, so VEP clears the warning at its next daily check.`,
        color: 'success',
      }
    }

    const daysLeft =
      flag.cancelsOn !== null ? daysBetween(today, flag.cancelsOn) : null
    const when =
      daysLeft === null
        ? 'soon'
        : daysLeft <= 0
          ? "at today's daily check"
          : `on ${formatDate(flag.cancelsOn)} (in ${plural(daysLeft, 'day')})`
    const extended =
      flag.extensionKind !== null
        ? `${flag.extendedBy ?? 'An admin'} already gave ${flagged} more time on this deal. `
        : ''
    return {
      title: `Deal cancelled ${when}`,
      detail: `${extended}If ${flagged} is still holding up the deal at the daily check on ${formatDate(flag.cancelsOn)}, VEP cancels it, raises a 5-day flag against ${flagged}, and matches ${partner}'s slot with a new partner. ${describeFix(flag)} ${describeMatchingHold(flag)}`,
      color: daysLeft !== null && daysLeft <= 1 ? 'error' : 'warning',
    }
  }

  const partnerSlot =
    flag.innocentSlotOtherDealId !== null
      ? `${partner}'s slot has since been matched into deal #${flag.innocentSlotOtherDealId}.`
      : `${partner}'s slot is back in matching.`
  const partnerFlagged =
    flag.flagType === DealFlagType.AidFailed
      ? `${partner} was flagged for this deal too, and is held back the same way until their flag is resolved.`
      : partnerSlot
  return {
    title: 'Stays open until an admin resolves it',
    detail: `The deal was cancelled on ${formatDate(flag.dealCancelledOn)}. ${describeMatchingHold(flag)} ${partnerFlagged}`,
    color: 'error',
  }
}

// Why the flag's deal can't be restored, or null if it can
export function getRestoreBlocker(flag: DealFlag): string | null {
  const flagged = getAccountName(flag.flaggedAccount)
  const partner = getAccountName(flag.innocentAccount)

  if (isOnActiveDeal(flag)) return 'The deal is still active.'
  if (flag.flagType === DealFlagType.OneDayInactive) {
    return "This warning didn't cancel the deal."
  }
  if (flag.dealCancelledBy !== 'system') {
    return `The deal was cancelled by ${flag.dealCancelledBy ?? 'an admin'}, not by this flag.`
  }
  if (flag.flaggedAccountOnHold) return `${flagged} is on hold.`
  if (flag.innocentAccountOnHold) return `${partner} is on hold.`
  if (flag.flaggedSlotOtherDealId !== null) {
    return `${flagged}'s slot #${flag.flaggedSlot.slotNumber} is now in deal #${flag.flaggedSlotOtherDealId}.`
  }
  if (flag.innocentSlotOtherDealId !== null) {
    return `${partner}'s slot #${flag.innocentSlot.slotNumber} has been matched into deal #${flag.innocentSlotOtherDealId} since.`
  }
  if (flag.pairOtherDealId !== null) {
    return `${flagged} and ${partner} already have another deal together (#${flag.pairOtherDealId}).`
  }
  if (!flag.slotsStillMatch) {
    return "The two slots' aid no longer match up, so the deal couldn't run. Changing an account's role resets its slots."
  }
  return null
}

// More time for an account still holding up a running deal
function getExtensionOptions(flag: DealFlag): FlagResolutionOption[] {
  const flagged = getAccountName(flag.flaggedAccount)
  const partner = getAccountName(flag.innocentAccount)
  const deadline = formatDate(flag.cancelsOn ?? getCnToday())
  const newDeadline = addDays(deadline, 5)
  const outOfTurn = flag.flagReason === DealFlagReason.OutOfSequenceAid
  const aidClears =
    flag.blockedUntil !== null ? formatDate(flag.blockedUntil) : null

  const fiveDaysShort =
    outOfTurn && aidClears !== null && daysBetween(newDeadline, aidClears) >= 0
      ? ` That isn't enough here: the out-of-turn aid doesn't clear until ${aidClears}.`
      : ''
  const untilNextLegFit = outOfTurn
    ? ` Suits out-of-turn aid that won't clear before the deadline${aidClears !== null ? `, like this aid, which clears on ${aidClears}` : ''}.`
    : ` There's no deadline, so if ${flagged} never sends, the deal stays stuck until an admin steps in.`

  return [
    {
      value: 'extendFiveDays',
      label: `Give ${flagged} 5 more days`,
      description: `Moves the cancellation from ${deadline} to ${newDeadline} and clears this warning, so ${flagged} can be matched with new partners meanwhile. The usual rules then run against the new date: warned again from ${addDays(deadline, 1)}, and cancelled on ${newDeadline} if still held up. The extension ends once the next aid in the deal is recorded.${fiveDaysShort}`,
      disabledReason: null,
    },
    {
      value: 'extendUntilNextLeg',
      label: 'Hold off until the next aid is recorded',
      description: `Stops the clock and clears this warning: VEP won't warn ${flagged} again or cancel the deal until the next aid in it is recorded. If ${partner} becomes the one holding it up first (say, once it's their turn to send), the extension ends and ${partner} gets the usual 5 days.${untilNextLegFit}`,
      disabledReason: null,
    },
  ]
}

// The ways an admin can resolve a flag, given whether its deal is still running
export function getResolutionOptions(
  flag: DealFlag,
  partnerFlag?: DealFlag,
): FlagResolutionOption[] {
  const flagged = getAccountName(flag.flaggedAccount)
  const partner = getAccountName(flag.innocentAccount)
  const others = flag.flaggedAccountOpenFlagCount - 1
  const matchedAgain =
    others > 0
      ? `${flagged} can be matched with new partners once their ${plural(others, 'other open flag')} ${others === 1 ? 'is' : 'are'} resolved too.`
      : `${flagged} can be matched with new partners again from the next matching run.`

  if (isOnActiveDeal(flag)) {
    const clear: FlagResolutionOption = isStillHoldingUpDeal(flag)
      ? {
          value: 'clear',
          label: 'Clear the warning, keep the deal',
          description: `${matchedAgain} This doesn't stop the clock: if ${flagged} is still holding up the deal at the next daily check, VEP warns them again, and still cancels the deal on ${formatDate(flag.cancelsOn)}. To give them more time, extend the deadline instead.`,
          disabledReason: null,
        }
      : {
          value: 'clear',
          label: 'Clear the warning now',
          description: `${flagged} isn't holding up the deal any more, so VEP would clear this at its next daily check anyway. ${matchedAgain}`,
          disabledReason: null,
        }
    return [
      ...(isStillHoldingUpDeal(flag) ? getExtensionOptions(flag) : []),
      clear,
      {
        value: 'cancelDeal',
        label: 'Cancel the deal now',
        description: `Ends deal #${flag.dealId} and clears this warning. Both slots go back into matching. No 5-day flag is raised, so ${flagged} isn't held back over it.`,
        disabledReason: null,
      },
    ]
  }

  if (flag.flagType === DealFlagType.OneDayInactive) {
    return [
      {
        value: 'clear',
        label: 'Clear the warning now',
        description: `The deal has ended, so VEP would clear this at its next daily check anyway. ${matchedAgain}`,
        disabledReason: null,
      },
    ]
  }

  const bothFlagged =
    flag.flagType === DealFlagType.AidFailed && partnerFlag !== undefined
  const restoreAfter =
    flag.flagType === DealFlagType.AidFailed
      ? 'The failed offer no longer counts against the deal, and needs sending again. If it fails again, VEP cancels the deal again.'
      : `If ${flagged} still holds it up, they're warned after a day and the deal is cancelled again after 5.`
  const options: FlagResolutionOption[] = [
    {
      value: 'restore',
      label: 'Restore the deal',
      description: `Puts deal #${flag.dealId} between ${flagged} and ${partner} back into effect, and resolves ${bothFlagged ? "both accounts' flags" : 'its flags'}. VEP works out whose turn it is at its next run, and the 5-day clock starts fresh. ${restoreAfter}`,
      disabledReason: getRestoreBlocker(flag),
    },
    {
      value: 'clear',
      label: bothFlagged
        ? `Clear ${flagged}'s flag only, keep the deal cancelled`
        : 'Clear the flag, keep the deal cancelled',
      description: bothFlagged
        ? `${matchedAgain} ${partner}'s flag on this deal stays open, for when ${flagged} wasn't at fault but ${partner} was.`
        : matchedAgain,
      disabledReason: null,
    },
  ]
  if (bothFlagged) {
    options.push({
      value: 'clearBoth',
      label: "Clear both accounts' flags, keep the deal cancelled",
      description: `For when neither side was at fault. ${flagged} and ${partner} can both be matched with new partners again, once any other open flags they have are resolved.`,
      disabledReason: null,
    })
  }
  return options
}

// The other account's flag for the same failed offer, if it's still open
export function findPartnerFlag(
  flag: DealFlag,
  flags: DealFlag[],
): DealFlag | undefined {
  if (flag.flagType !== DealFlagType.AidFailed) return undefined
  return flags.find(
    (other) =>
      other.id !== flag.id &&
      other.dealId === flag.dealId &&
      other.flagType === DealFlagType.AidFailed &&
      other.flaggedAccount.accountId === flag.innocentAccount.accountId,
  )
}

export function matchesFlagView(flag: DealFlag, view: FlagDealView): boolean {
  if (view === 'active') return isOnActiveDeal(flag)
  if (view === 'cancelled') return !isOnActiveDeal(flag)
  return true
}

// Every word must match something: either account's ruler, nation, alliance or role, the deal number,
// or the flag's type or reason. So "buyer valyria" finds Valyrian buyers
export function matchesFlagSearch(flag: DealFlag, search: string): boolean {
  const terms = search.toLowerCase().split(/\s+/).filter(Boolean)
  if (terms.length === 0) return true

  const haystack = [
    flag.flaggedAccount.rulerName,
    flag.flaggedAccount.nationName,
    flag.flaggedAccount.allianceName,
    getRoleName(flag.flaggedAccountRole),
    flag.innocentAccount.rulerName,
    flag.innocentAccount.nationName,
    flag.innocentAccount.allianceName,
    getRoleName(flag.innocentAccountRole),
    `#${flag.dealId}`,
    getFlagTypeInfo(flag).label,
    getFlagReasonLabel(flag),
  ]
    .filter((value): value is string => !!value)
    .join('\n')
    .toLowerCase()

  return terms.every((term) => haystack.includes(term))
}

// Most urgent first: warnings on deals about to be cancelled, then cancelled deals (newest first),
// then warnings that will clear themselves
export function sortFlags(flags: DealFlag[]): DealFlag[] {
  const group = (flag: DealFlag): number =>
    isStillHoldingUpDeal(flag)
      ? 0
      : flag.flagType !== DealFlagType.OneDayInactive
        ? 1
        : 2
  return [...flags].sort(
    (a, b) =>
      group(a) - group(b) ||
      (group(a) === 0
        ? (a.cancelsOn ?? '').localeCompare(b.cancelsOn ?? '')
        : b.flaggedOn.localeCompare(a.flaggedOn)) ||
      a.id - b.id,
  )
}

// An aid's contents, e.g. "$9M + 100 tech"
export function describeAidContents(aid: {
  money: number
  technology: number
  soldiers?: number
}): string {
  const parts: string[] = []
  if (aid.money > 0) {
    parts.push(
      aid.money >= 1_000_000
        ? `$${(aid.money / 1_000_000).toLocaleString('en-US', { maximumFractionDigits: 2 })}M`
        : `$${aid.money.toLocaleString('en-US')}`,
    )
  }
  if (aid.technology > 0) parts.push(`${aid.technology} tech`)
  if ((aid.soldiers ?? 0) > 0) parts.push(`${aid.soldiers} soldiers`)
  return parts.length > 0 ? parts.join(' + ') : 'Nothing'
}

// Whether aid in the flagged nation's slots counts against it
export function getSlotAidKind(aid: FlagSlotAid, flag: DealFlag): LabelInfo {
  if (aid.dealId === flag.dealId) return { label: 'This deal', color: 'info' }
  if (aid.dealId !== null) {
    return { label: `VEP deal #${aid.dealId}`, color: 'neutral' }
  }
  if (aid.sentBeforeDeal) return { label: 'Before this deal', color: 'neutral' }
  return { label: 'Outside VEP', color: 'error' }
}

export function getLegOutcomeInfo(leg: FlagDealLeg): LabelInfo {
  switch (leg.outcome) {
    case DealLegOutcome.Accepted:
      return { label: 'Accepted', color: 'success' }
    case DealLegOutcome.Cancelled:
      return {
        label: leg.failureExcused ? 'Cancelled (excused)' : 'Cancelled',
        color: leg.failureExcused ? 'neutral' : 'error',
      }
    case DealLegOutcome.Expired:
      return {
        label: leg.failureExcused ? 'Expired (excused)' : 'Expired',
        color: leg.failureExcused ? 'neutral' : 'error',
      }
    default:
      return { label: 'Pending', color: 'warning' }
  }
}
