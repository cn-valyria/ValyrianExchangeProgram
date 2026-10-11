import { describe, expect, it } from 'vitest'
import { describeRecipientActions, fillTemplate, formatCnDate, getBatches, getSendMessageLink } from '~/infrastructure/massMessages'
import type { MassMessageRecipient } from '~/stores/adminMassMessages'

const recipient = (id: number, overrides: Partial<MassMessageRecipient> = {}): MassMessageRecipient => ({
  accountId: id,
  nationId: 100 + id,
  rulerName: `Ruler${id}`,
  nationName: `Nation${id}`,
  allianceName: 'Valyria',
  discord: null,
  sendCount: 0,
  acceptCount: 0,
  blockedCount: 0,
  flaggedCount: 0,
  daysOverdue: null,
  cancelsOn: null,
  ...overrides
})

describe('formatCnDate', () => {
  it('formats the API date without shifting it to the local time zone', () => {
    expect(formatCnDate('2026-10-09T00:00:00')).toBe('Oct 09')
    expect(formatCnDate('2026-01-01')).toBe('Jan 01')
  })
})

describe('fillTemplate', () => {
  it('fills in every placeholder', () => {
    const filled = fillTemplate('{{date}}: log in at {{siteUrl}}, or {{siteUrl}}/login', {
      date: 'Oct 09',
      siteUrl: 'https://vep.example'
    })
    expect(filled).toBe('Oct 09: log in at https://vep.example, or https://vep.example/login')
  })
})

describe('getBatches', () => {
  it('splits recipients into CN-sized messages', () => {
    const recipients = Array.from({ length: 51 }, (_, i) => recipient(i))
    const batches = getBatches(recipients)
    expect(batches.map((batch) => batch.length)).toEqual([25, 25, 1])
    expect(batches[1][0].accountId).toBe(25)
  })

  it('has no batches when there are no recipients', () => {
    expect(getBatches([])).toEqual([])
  })
})

describe('describeRecipientActions', () => {
  it('lists only the actions the recipient has', () => {
    expect(describeRecipientActions(recipient(1, { sendCount: 2, blockedCount: 1 }))).toBe('2 to send, 1 blocked')
    expect(describeRecipientActions(recipient(1, { acceptCount: 1 }))).toBe('1 to accept')
  })
})

describe('getSendMessageLink', () => {
  it("links to CN's message form for the nation", () => {
    expect(getSendMessageLink(202)).toBe('https://www.cybernations.net/send_message.asp?Nation_ID=202')
  })
})

describe('describeRecipientActions with nothing to do', () => {
  it('is empty', () => {
    expect(describeRecipientActions(recipient(1))).toBe('')
  })
})
