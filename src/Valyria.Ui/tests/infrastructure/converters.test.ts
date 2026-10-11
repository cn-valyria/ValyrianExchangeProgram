import { describe, expect, it } from 'vitest'
import { convertToAllianceAcronym } from '~/infrastructure/converters'

describe('convertToAllianceAcronym', () => {
  it('uses the known acronym, ignoring case', () => {
    expect(convertToAllianceAcronym('Christian Coalition Of Countries')).toBe('CCC')
    expect(convertToAllianceAcronym('the imperial order')).toBe('TIO')
  })

  it("builds an acronym from each word's first letter for unknown alliances", () => {
    expect(convertToAllianceAcronym('Valyrian Exchange Program')).toBe('VEP')
    expect(convertToAllianceAcronym('Valyria')).toBe('V')
  })

  it('is empty for nations without an alliance', () => {
    expect(convertToAllianceAcronym('')).toBe('')
    expect(convertToAllianceAcronym(null as unknown as string)).toBe('')
  })
})
