import { describe, expect, it } from 'vitest'
import {
  describeAidContents,
  describeExpectation,
  describeWhatHappened,
  findPartnerFlag,
  getAccountName,
  getCnToday,
  getDealStatusInfo,
  getFlagOutlook,
  getLegOutcomeInfo,
  getResolutionOptions,
  getRestoreBlocker,
  getSlotAidKind,
  matchesFlagSearch,
  matchesFlagView,
  sortFlags,
} from '~/infrastructure/flags'
import { DealBlockedReason, DealLegOutcome } from '~/stores/account'
import {
  DealFlagReason,
  DealFlagType,
  type FlagDealLeg,
  type FlagSlotAid,
} from '~/stores/adminDeals'
import {
  accountSimplified,
  cancelledDealFlag,
  dealFlag,
} from '../helpers/fixtures'

const TODAY = '2026-10-10'

const slotAid = (overrides: Partial<FlagSlotAid> = {}): FlagSlotAid => ({
  aidId: 1,
  sentOn: '2026-10-08T12:00:00',
  isOutgoing: true,
  status: 2,
  money: 0,
  technology: 0,
  soldiers: 0,
  reason: null,
  dealId: null,
  sentBeforeDeal: false,
  otherNation: {
    nationId: 500,
    nationName: 'Elsewhere',
    rulerName: 'Someone',
    allianceName: 'Other',
  },
  ...overrides,
})

const leg = (overrides: Partial<FlagDealLeg> = {}): FlagDealLeg => ({
  legNumber: 1,
  aidId: 1,
  activityDate: '2026-10-01T00:00:00',
  sentByFlaggedAccount: true,
  money: 9_000_000,
  technology: 0,
  outcome: DealLegOutcome.Accepted,
  failureExcused: false,
  ...overrides,
})

describe('getCnToday', () => {
  it('is the UTC date shifted back 5 hours', () => {
    expect(getCnToday(new Date('2026-10-10T04:59:00Z'))).toBe('2026-10-09')
    expect(getCnToday(new Date('2026-10-10T05:00:00Z'))).toBe('2026-10-10')
  })
})

describe('getAccountName', () => {
  it('falls back to the nation id once the nation is deleted', () => {
    expect(
      getAccountName(
        accountSimplified(1, {
          rulerName: null as unknown as string,
          nationId: 77,
        }),
      ),
    ).toBe('Nation #77 (deleted)')
  })
})

describe('getDealStatusInfo', () => {
  it.each([
    [dealFlag(), 'Active, still held up'],
    [dealFlag({ responsibleAccountId: 2 }), 'Active, back on track'],
    [dealFlag({ state: null }), 'Active (restored)'],
    [cancelledDealFlag(), 'Cancelled 2026-10-09'],
  ])('labels the deal %#', (flag, label) => {
    expect(getDealStatusInfo(flag).label).toBe(label)
  })
})

describe('describeExpectation and describeWhatHappened', () => {
  it('says what the flagged account should have sent, and when it last did', () => {
    const flag = dealFlag()
    expect(describeExpectation(flag)).toBe(
      "It was Ruler1's turn to send $9M to Ruler2, and both nations had a free aid slot.",
    )
    expect(describeWhatHappened(flag)).toBe(
      "Ruler1 didn't send it. Their last aid counted in this deal was sent on 2026-09-28. They've held up the deal for 2 days.",
    )
  })

  it("says when the flagged account hasn't sent anything yet, and leaves out days once it's resolved", () => {
    expect(
      describeWhatHappened(cancelledDealFlag({ flaggedLastSentOn: null })),
    ).toBe(
      "Ruler1 didn't send it. They haven't had any aid counted in this deal yet.",
    )
  })

  it('explains a receiver whose slots are full of other aid', () => {
    const flag = dealFlag({
      flagReason: DealFlagReason.ReceiverSlotsFull,
      responsibleAccountId: 1,
    })
    expect(describeExpectation(flag)).toBe(
      "It was Ruler2's turn to send 100 tech to Ruler1, which needs a free aid slot on Ruler1's side.",
    )
    expect(describeWhatHappened(flag)).toContain(
      "so Ruler2 couldn't send to them",
    )
  })

  it('says when out-of-turn aid stops blocking the partner', () => {
    const flag = dealFlag({
      flagReason: DealFlagReason.OutOfSequenceAid,
      blockedReason: DealBlockedReason.OutOfSequenceAid,
      blockedUntil: '2026-10-17T00:00:00',
    })
    expect(describeExpectation(flag)).toContain(
      "It was Ruler2's turn to send 100 tech. Ruler1 was expected to wait for it",
    )
    expect(describeWhatHappened(flag)).toBe(
      "Ruler1 sent aid to Ruler2 out of turn, which blocks Ruler2 until 2026-10-17. They've held up the deal for 2 days.",
    )
  })

  it('explains that failed aid flags both accounts', () => {
    const flag = cancelledDealFlag({
      flagType: DealFlagType.AidFailed,
      flagReason: DealFlagReason.AidExpired,
    })
    expect(describeExpectation(flag)).toContain('accepted within 10 days')
    expect(describeWhatHappened(flag)).toBe(
      "An offer between Ruler1 and Ruler2 expired without being accepted. VEP can't tell which side caused it, so it flagged both accounts and cancelled the deal.",
    )
  })

  it('handles flags raised before reasons were recorded', () => {
    const flag = dealFlag({ flagReason: null })
    expect(describeExpectation(flag)).toContain('before VEP 3.0')
    expect(describeWhatHappened(flag)).toBe('Ruler1 was flagged on 2026-10-09.')
  })
})

describe('getFlagOutlook', () => {
  it('counts down to the cancellation of a deal that is still held up', () => {
    const outlook = getFlagOutlook(dealFlag(), TODAY)
    expect(outlook.title).toBe('Deal cancelled on 2026-10-13 (in 3 days)')
    expect(outlook.color).toBe('warning')
    expect(outlook.detail).toBe(
      "If Ruler1 is still holding up the deal at the daily check on 2026-10-13, VEP cancels it, raises a 5-day flag against Ruler1, and matches Ruler2's slot with a new partner. It clears itself once Ruler1 sends the aid. Ruler1 isn't matched with new partners until it's resolved.",
    )
  })

  it("warns on the last day, and counts the account's other flags", () => {
    const outlook = getFlagOutlook(
      dealFlag({
        cancelsOn: '2026-10-10T00:00:00',
        flaggedAccountOpenFlagCount: 3,
      }),
      TODAY,
    )
    expect(outlook.title).toBe("Deal cancelled at today's daily check")
    expect(outlook.color).toBe('error')
    expect(outlook.detail).toContain(
      "Ruler1 isn't matched with new partners until this and their 2 other open flags are resolved.",
    )
  })

  it.each([
    [
      DealFlagReason.SenderSlotsFull,
      "Freeing a slot alone isn't enough, since it's still their turn.",
    ],
    [
      DealFlagReason.ReceiverSlotsFull,
      "It clears itself once Ruler1 frees an aid slot for Ruler2's aid.",
    ],
  ])('says how a %s warning clears itself', (flagReason, fix) => {
    expect(getFlagOutlook(dealFlag({ flagReason }), TODAY).detail).toContain(
      fix,
    )
  })

  it('points out when out-of-turn aid outlasts the deal', () => {
    const outlook = getFlagOutlook(
      dealFlag({
        flagReason: DealFlagReason.OutOfSequenceAid,
        blockedUntil: '2026-10-17T00:00:00',
      }),
      TODAY,
    )
    expect(outlook.detail).toContain(
      'The aid clears on 2026-10-17, after the deal is due to be cancelled, so it needs cancelling to save the deal.',
    )
  })

  it('clears itself once the account stops holding up the deal, or the deal ends', () => {
    expect(
      getFlagOutlook(dealFlag({ responsibleAccountId: 2 }), TODAY),
    ).toMatchObject({
      title: 'Clears itself at the next daily check',
      color: 'success',
    })
    expect(
      getFlagOutlook(
        cancelledDealFlag({ flagType: DealFlagType.OneDayInactive }),
        TODAY,
      ).title,
    ).toBe('Clears itself at the next daily check')
  })

  it('stays open after cancelling the deal, saying where the partner stands', () => {
    expect(getFlagOutlook(cancelledDealFlag(), TODAY)).toMatchObject({
      title: 'Stays open until an admin resolves it',
      detail:
        "The deal was cancelled on 2026-10-09. Ruler1 isn't matched with new partners until it's resolved. Ruler2's slot is back in matching.",
    })
    expect(
      getFlagOutlook(cancelledDealFlag({ innocentSlotOtherDealId: 44 }), TODAY)
        .detail,
    ).toContain("Ruler2's slot has since been matched into deal #44.")
    expect(
      getFlagOutlook(
        cancelledDealFlag({ flagType: DealFlagType.AidFailed }),
        TODAY,
      ).detail,
    ).toContain('Ruler2 was flagged for this deal too')
  })
})

describe('getRestoreBlocker', () => {
  it.each([
    [cancelledDealFlag(), null],
    [dealFlag(), 'The deal is still active.'],
    [
      cancelledDealFlag({ flagType: DealFlagType.OneDayInactive }),
      "This warning didn't cancel the deal.",
    ],
    [
      cancelledDealFlag({ dealCancelledBy: 'Bob' }),
      'The deal was cancelled by Bob, not by this flag.',
    ],
    [cancelledDealFlag({ flaggedAccountOnHold: true }), 'Ruler1 is on hold.'],
    [cancelledDealFlag({ innocentAccountOnHold: true }), 'Ruler2 is on hold.'],
    [
      cancelledDealFlag({ flaggedSlotOtherDealId: 30 }),
      "Ruler1's slot #1 is now in deal #30.",
    ],
    [
      cancelledDealFlag({ innocentSlotOtherDealId: 31 }),
      "Ruler2's slot #1 has been matched into deal #31 since.",
    ],
    [
      cancelledDealFlag({ pairOtherDealId: 32 }),
      'Ruler1 and Ruler2 already have another deal together (#32).',
    ],
    [
      cancelledDealFlag({ slotsStillMatch: false }),
      "The two slots' aid no longer match up, so the deal couldn't run. Changing an account's role resets its slots.",
    ],
  ])('explains blocker %#', (flag, expected) => {
    expect(getRestoreBlocker(flag)).toBe(expected)
  })
})

describe('getResolutionOptions', () => {
  const values = (options: ReturnType<typeof getResolutionOptions>) =>
    options.map((o) => o.value)

  it("offers clearing or cancelling a held-up deal, and says clearing doesn't stop the clock", () => {
    const options = getResolutionOptions(dealFlag())
    expect(values(options)).toEqual(['clear', 'cancelDeal'])
    expect(options[0].description).toContain(
      'VEP warns them again, and still cancels the deal on 2026-10-13',
    )
    expect(options[1].description).toContain('No 5-day flag is raised')
  })

  it('offers clearing early once the account stops holding up the deal, or the deal ends', () => {
    const backOnTrack = getResolutionOptions(
      dealFlag({ responsibleAccountId: 2 }),
    )
    expect(backOnTrack[0].label).toBe('Clear the warning now')
    const ended = getResolutionOptions(
      cancelledDealFlag({ flagType: DealFlagType.OneDayInactive }),
    )
    expect(values(ended)).toEqual(['clear'])
  })

  it('offers restoring or clearing a deal cancelled by a flag, with why restoring is unavailable', () => {
    const options = getResolutionOptions(cancelledDealFlag())
    expect(values(options)).toEqual(['restore', 'clear'])
    expect(options[0].disabledReason).toBeNull()
    expect(options[0].description).toContain('the 5-day clock starts fresh')

    const blocked = getResolutionOptions(
      cancelledDealFlag({ innocentSlotOtherDealId: 31 }),
    )
    expect(blocked[0].disabledReason).toBe(
      "Ruler2's slot #1 has been matched into deal #31 since.",
    )
  })

  it("offers clearing one or both accounts' flags for failed aid", () => {
    const flag = cancelledDealFlag({ flagType: DealFlagType.AidFailed })
    const partner = cancelledDealFlag({
      id: 6,
      flagType: DealFlagType.AidFailed,
      flaggedAccount: accountSimplified(2),
      innocentAccount: accountSimplified(1),
    })
    const options = getResolutionOptions(flag, partner)
    expect(values(options)).toEqual(['restore', 'clear', 'clearBoth'])
    expect(options[0].description).toContain("resolves both accounts' flags")
    expect(options[1].label).toBe(
      "Clear Ruler1's flag only, keep the deal cancelled",
    )
  })

  it("mentions the account's other flags", () => {
    expect(
      getResolutionOptions(
        cancelledDealFlag({ flaggedAccountOpenFlagCount: 2 }),
      )[1].description,
    ).toBe(
      'Ruler1 can be matched with new partners once their 1 other open flag is resolved too.',
    )
  })
})

describe('findPartnerFlag', () => {
  it("finds the other account's flag for the same failed offer", () => {
    const flag = cancelledDealFlag({ flagType: DealFlagType.AidFailed })
    const partner = cancelledDealFlag({
      id: 6,
      flagType: DealFlagType.AidFailed,
      flaggedAccount: accountSimplified(2),
      innocentAccount: accountSimplified(1),
    })
    const elsewhere = { ...partner, id: 7, dealId: 11 }
    expect(findPartnerFlag(flag, [flag, elsewhere, partner])).toBe(partner)
    expect(findPartnerFlag(dealFlag(), [partner])).toBeUndefined()
  })
})

describe('matchesFlagSearch and matchesFlagView', () => {
  const flag = dealFlag({
    flaggedAccount: accountSimplified(1, {
      rulerName: 'Bob',
      allianceName: 'Valyria',
    }),
    innocentAccount: accountSimplified(2, {
      rulerName: 'Sue',
      allianceName: 'Rogues',
    }),
  })

  it.each([
    ['', true],
    ['bob', true],
    ['ROGUES', true],
    ['buyer valyria', true],
    ['seller', true],
    ['#10', true],
    ['#11', false],
    ['warning', true],
    ['buyer rogues', true],
    ['carol', false],
  ])('matches %j: %s', (search, expected) => {
    expect(matchesFlagSearch(flag, search)).toBe(expected)
  })

  it('splits flags by whether their deal is still active', () => {
    expect(matchesFlagView(dealFlag(), 'active')).toBe(true)
    expect(matchesFlagView(dealFlag(), 'cancelled')).toBe(false)
    expect(matchesFlagView(cancelledDealFlag(), 'cancelled')).toBe(true)
    expect(matchesFlagView(cancelledDealFlag(), 'all')).toBe(true)
  })
})

describe('sortFlags', () => {
  it('puts deals about to be cancelled first, then cancelled deals, then warnings clearing themselves', () => {
    const later = dealFlag({ id: 1, cancelsOn: '2026-10-14T00:00:00' })
    const sooner = dealFlag({ id: 2, cancelsOn: '2026-10-11T00:00:00' })
    const clearing = dealFlag({ id: 3, responsibleAccountId: 2 })
    const olderCancel = cancelledDealFlag({
      id: 4,
      flaggedOn: '2026-10-01T00:00:00',
    })
    const newerCancel = cancelledDealFlag({
      id: 5,
      flaggedOn: '2026-10-05T00:00:00',
    })

    expect(
      sortFlags([clearing, olderCancel, later, newerCancel, sooner]).map(
        (f) => f.id,
      ),
    ).toEqual([2, 1, 5, 4, 3])
  })
})

describe('describeAidContents', () => {
  it.each([
    [{ money: 9_000_000, technology: 0 }, '$9M'],
    [{ money: 3_500_000, technology: 100 }, '$3.5M + 100 tech'],
    [{ money: 250_000, technology: 0, soldiers: 50 }, '$250,000 + 50 soldiers'],
    [{ money: 0, technology: 0 }, 'Nothing'],
  ])('describes %j', (aid, expected) => {
    expect(describeAidContents(aid)).toBe(expected)
  })
})

describe('getSlotAidKind and getLegOutcomeInfo', () => {
  it('says whether slot aid counts against the nation', () => {
    const flag = dealFlag()
    expect(getSlotAidKind(slotAid({ dealId: 10 }), flag).label).toBe(
      'This deal',
    )
    expect(getSlotAidKind(slotAid({ dealId: 12 }), flag).label).toBe(
      'VEP deal #12',
    )
    expect(getSlotAidKind(slotAid({ sentBeforeDeal: true }), flag).label).toBe(
      'Before this deal',
    )
    expect(getSlotAidKind(slotAid(), flag)).toEqual({
      label: 'Outside VEP',
      color: 'error',
    })
  })

  it('labels leg outcomes, noting excused failures', () => {
    expect(getLegOutcomeInfo(leg()).label).toBe('Accepted')
    expect(
      getLegOutcomeInfo(leg({ outcome: DealLegOutcome.Pending })).label,
    ).toBe('Pending')
    expect(getLegOutcomeInfo(leg({ outcome: DealLegOutcome.Expired }))).toEqual(
      { label: 'Expired', color: 'error' },
    )
    expect(
      getLegOutcomeInfo(
        leg({ outcome: DealLegOutcome.Cancelled, failureExcused: true }),
      ),
    ).toEqual({ label: 'Cancelled (excused)', color: 'neutral' })
  })
})
