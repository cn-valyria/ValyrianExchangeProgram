import type { Leaderboard, LeaderboardStanding } from '~/stores/leaderboard'

const plural = (count: number, word: string): string =>
  `${count} ${word}${count === 1 ? '' : 's'}`

const formatDate = (date: string): string => date.split('T')[0]

// "1 flag across 11 outgoing legs"
export const describeRecord = (standing: LeaderboardStanding): string =>
  `${plural(standing.flags, 'flag')} across ${plural(standing.outgoingLegs, 'outgoing leg')}`

// "Perfect record", or the rate to 2 decimal places: "0.09 flags per leg"
export const formatFlagsPerLeg = (standing: LeaderboardStanding): string =>
  standing.flags === 0
    ? 'Perfect record'
    : standing.outgoingLegs === 0
      ? 'No outgoing legs yet'
      : `${standing.flagsPerLeg.toFixed(2)} flags per leg`

export interface StandingSummary {
  title: string
  detail: string
}

// What the "Your standing" card says about the signed-in account
export function getStandingSummary(
  leaderboard: Leaderboard,
  mine: LeaderboardStanding,
): StandingSummary {
  const dropsOff = mine.nextFlagDropsOn
    ? ` Your oldest flag stops counting on ${formatDate(mine.nextFlagDropsOn)}.`
    : ''

  if (mine.rank === null) {
    return {
      title: 'Not ranked yet',
      detail: `Send ${plural(mine.legsToQualify, 'more outgoing leg')} in the next ${leaderboard.windowDays} days to be ranked.${dropsOff}`,
    }
  }

  const ofRanked = `of ${leaderboard.rankedAccounts}`
  if (mine.flags === 0) {
    return {
      title: `#${mine.rank} ${ofRanked}`,
      detail:
        'A perfect record: every leg sent on time, to the right partner. Keep sending to climb the ties.',
    }
  }

  return {
    title: `#${mine.rank} ${ofRanked}`,
    detail: `Send every leg on the day it opens to bring your rate down.${dropsOff}`,
  }
}
