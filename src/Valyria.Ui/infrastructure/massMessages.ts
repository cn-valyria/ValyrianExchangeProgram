import type { MassMessageRecipient } from '~/stores/adminMassMessages'

// CN sends a message to one nation plus a list of carbon-copied rulers; VEP 2.5 found 25 per message works
export const MASS_MESSAGE_BATCH_SIZE = 25

const MONTHS = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
]

// Today's CN date as "Oct 09", from the API's "2026-10-09T00:00:00"
export function formatCnDate(cnDate: string): string {
  const [, month, day] = cnDate.split('T')[0].split('-')
  return `${MONTHS[Number(month) - 1]} ${day}`
}

// Fills in a message template's {{date}} and {{siteUrl}}
export function fillTemplate(
  template: string,
  values: { date: string; siteUrl: string },
): string {
  return template
    .replaceAll('{{date}}', values.date)
    .replaceAll('{{siteUrl}}', values.siteUrl)
}

// Splits recipients into CN messages: the first in each batch is the addressee, the rest are CC'd
export function getBatches(
  recipients: MassMessageRecipient[],
  batchSize = MASS_MESSAGE_BATCH_SIZE,
): MassMessageRecipient[][] {
  const batches: MassMessageRecipient[][] = []
  for (let i = 0; i < recipients.length; i += batchSize) {
    batches.push(recipients.slice(i, i + batchSize))
  }
  return batches
}

// What a recipient needs to do today, e.g. "2 to send, 1 to accept". Admins only
export function describeRecipientActions(
  recipient: MassMessageRecipient,
): string {
  const parts: string[] = []
  if (recipient.sendCount > 0) parts.push(`${recipient.sendCount} to send`)
  if (recipient.acceptCount > 0)
    parts.push(`${recipient.acceptCount} to accept`)
  if (recipient.blockedCount > 0)
    parts.push(`${recipient.blockedCount} blocked`)
  return parts.join(', ')
}

export function getSendMessageLink(nationId: number): string {
  return `https://www.cybernations.net/send_message.asp?Nation_ID=${nationId}`
}
