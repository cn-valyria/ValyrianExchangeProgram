import { describe, expect, it } from 'vitest'
import {
  describeRecord,
  formatFlagsPerLeg,
  getStandingSummary,
} from '~/infrastructure/leaderboard'
import { leaderboard, leaderboardStanding } from '../helpers/fixtures'

describe('leaderboard text', () => {
  it('describes the record behind the rate', () => {
    expect(
      describeRecord(leaderboardStanding(1, { flags: 1, outgoingLegs: 11 })),
    ).toBe('1 flag across 11 outgoing legs')
    expect(
      describeRecord(leaderboardStanding(1, { flags: 2, outgoingLegs: 1 })),
    ).toBe('2 flags across 1 outgoing leg')
  })

  it('calls no flags a perfect record, and rounds the rate otherwise', () => {
    expect(formatFlagsPerLeg(leaderboardStanding(1))).toBe('Perfect record')
    expect(
      formatFlagsPerLeg(
        leaderboardStanding(1, {
          flags: 1,
          outgoingLegs: 11,
          flagsPerLeg: 0.0909,
        }),
      ),
    ).toBe('0.09 flags per leg')
  })

  it('tells an unranked account how many more legs it needs', () => {
    expect(
      getStandingSummary(
        leaderboard(),
        leaderboardStanding(1, {
          rank: null,
          legsToQualify: 1,
          outgoingLegs: 4,
        }),
      ),
    ).toEqual({
      title: 'Not ranked yet',
      detail: 'Send 1 more outgoing leg in the next 90 days to be ranked.',
    })
  })

  it('gives a ranked account its rank, and when its oldest flag drops off', () => {
    const lb = leaderboard({ rankedAccounts: 12 })

    expect(getStandingSummary(lb, leaderboardStanding(3)).title).toBe(
      '#3 of 12',
    )
    expect(
      getStandingSummary(
        lb,
        leaderboardStanding(7, {
          flags: 2,
          flagsPerLeg: 0.2,
          nextFlagDropsOn: '2026-12-01T00:00:00',
        }),
      ),
    ).toEqual({
      title: '#7 of 12',
      detail:
        'Send every leg on the day it opens to bring your rate down. Your oldest flag stops counting on 2026-12-01.',
    })
  })
})
