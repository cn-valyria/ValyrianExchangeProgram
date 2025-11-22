import { allianceAcronyms } from "./dataLists"

export function convertToAllianceAcronym(fullAllianceName: string): string {
  if (!fullAllianceName) {
    return ''
  }

  const acronym = allianceAcronyms.find(
    (aa) => aa.name.localeCompare(fullAllianceName, 'en', { sensitivity: 'base' }) === 0
  )

  return acronym.code || fullAllianceName
    .split(/\s/)
    .reduce((result, word) => result + word.slice(0, 1), '')
}