import { describe, expect, it } from 'vitest'
import { accountRoles, getAccountRoleColor } from '~/infrastructure/dataLists'

describe('getAccountRoleColor', () => {
  it.each([
    [['B', 'D', 'W'], 'warning'],
    [['S', 'F', 'N', 'Q'], 'error'],
    [['C', 'R', 'P', 'V'], 'info'],
    [['H', 'Z', undefined], 'neutral'],
  ] as const)('colors %j %s', (codes, color) => {
    codes.forEach((code) => expect(getAccountRoleColor(code)).toBe(color))
  })

  it('has a color for every role but On Hold', () => {
    const uncolored = accountRoles.filter((role) => getAccountRoleColor(role.code) === 'neutral').map((role) => role.code)

    expect(uncolored).toEqual(['H'])
  })
})
