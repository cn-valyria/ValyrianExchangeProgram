// Mirrors Valyria.Core.DTOs.CN.Team (cybernations_db.nation.team), which is serialized as a number
export enum Team {
  Aqua = 1,
  White = 2,
  Red = 3,
  Green = 4,
  Brown = 5,
  Blue = 6,
  Purple = 7,
  Orange = 8,
  Maroon = 9,
  Black = 10,
  Yellow = 11,
  Pink = 12,
  None = 13,
}

// The teams that can pass senate events. None (Grey) has no senate
export const EVENT_TEAMS: { team: Team; name: string; swatch: string }[] = [
  { team: Team.Aqua, name: 'Aqua', swatch: 'bg-cyan-500' },
  {
    team: Team.White,
    name: 'White',
    swatch: 'bg-gray-100 border border-gray-300',
  },
  { team: Team.Red, name: 'Red', swatch: 'bg-red-500' },
  { team: Team.Green, name: 'Green', swatch: 'bg-green-500' },
  { team: Team.Brown, name: 'Brown', swatch: 'bg-amber-700' },
  { team: Team.Blue, name: 'Blue', swatch: 'bg-blue-500' },
  { team: Team.Purple, name: 'Purple', swatch: 'bg-purple-500' },
  { team: Team.Orange, name: 'Orange', swatch: 'bg-orange-500' },
  { team: Team.Maroon, name: 'Maroon', swatch: 'bg-red-900' },
  { team: Team.Black, name: 'Black', swatch: 'bg-gray-900' },
  { team: Team.Yellow, name: 'Yellow', swatch: 'bg-yellow-400' },
  { team: Team.Pink, name: 'Pink', swatch: 'bg-pink-500' },
]

export const teamName = (team: Team): string =>
  EVENT_TEAMS.find((t) => t.team === team)?.name ?? 'None (Grey)'

export const teamSwatch = (team: Team): string =>
  EVENT_TEAMS.find((t) => t.team === team)?.swatch ?? 'bg-gray-400'

// A passed proposal is in effect from its approval date through this many days later
export const TEAM_EVENT_DAYS = 30

const dateOnly = (date: string): string => date.split('T')[0]

// "2026-11-05T00:00:00" -> "11/5/2026", the way CN shows dates
export function formatCnDate(date: string): string {
  const [year, month, day] = dateOnly(date).split('-').map(Number)
  return `${month}/${day}/${year}`
}

// Adds days to a date-only string, returning "YYYY-MM-DD"
export function addDays(date: string, days: number): string {
  const [year, month, day] = dateOnly(date).split('-').map(Number)
  return new Date(Date.UTC(year, month - 1, day + days))
    .toISOString()
    .split('T')[0]
}

// Today's CN date ("YYYY-MM-DD"). CN runs on server time, UTC-5 all year
export function cnToday(now: Date = new Date()): string {
  return new Date(now.getTime() - 5 * 60 * 60 * 1000)
    .toISOString()
    .split('T')[0]
}

// Whole days from one date-only string to another
export function daysBetween(from: string, to: string): number {
  return Math.round(
    (Date.parse(dateOnly(to)) - Date.parse(dateOnly(from))) / 86_400_000,
  )
}

export const describeTemporaryDeal = (endsOn: string): string =>
  `Temporary: this deal uses a team event's bonus aid slot, so VEP cancels it once the event ends after ${formatCnDate(endsOn)}.`
