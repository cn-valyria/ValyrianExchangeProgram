import { describe, expect, it } from 'vitest'
import {
  addDays,
  cnToday,
  daysBetween,
  describeTemporaryDeal,
  EVENT_TEAMS,
  formatCnDate,
  Team,
  teamName,
  teamSwatch,
} from '~/infrastructure/teamEvents'

describe('team events', () => {
  it('names every team that can pass events, and grey for None', () => {
    expect(EVENT_TEAMS).toHaveLength(12)
    expect(teamName(Team.Maroon)).toBe('Maroon')
    expect(teamName(Team.None)).toBe('None (Grey)')
    expect(teamSwatch(Team.Aqua)).toBe('bg-cyan-500')
    expect(teamSwatch(Team.None)).toBe('bg-gray-400')
  })

  it('formats dates the way CN does', () => {
    expect(formatCnDate('2026-10-06T00:00:00')).toBe('10/6/2026')
    expect(formatCnDate('2026-11-05')).toBe('11/5/2026')
  })

  it('adds days across month and year ends', () => {
    expect(addDays('2026-10-06', 30)).toBe('2026-11-05')
    expect(addDays('2026-12-20T00:00:00', 15)).toBe('2027-01-04')
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28')
  })

  it('counts whole days between dates', () => {
    expect(daysBetween('2026-10-06', '2026-11-05T00:00:00')).toBe(30)
    expect(daysBetween('2026-11-05', '2026-11-05')).toBe(0)
  })

  it("uses CN's UTC-5 date for today", () => {
    expect(cnToday(new Date('2026-10-27T04:59:00Z'))).toBe('2026-10-26')
    expect(cnToday(new Date('2026-10-27T05:00:00Z'))).toBe('2026-10-27')
  })

  it('explains temporary deals', () => {
    expect(describeTemporaryDeal('2026-11-05T00:00:00')).toContain(
      'once the event ends after 11/5/2026',
    )
  })
})
