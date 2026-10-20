import { ref } from 'vue'
import {
  DealActivityState,
  type Account,
  type AccountSimplified,
  type DealDetail,
  type SlotDetail,
} from '~/stores/account'
import type { AdminAccount } from '~/stores/adminAccounts'
import {
  DealFlagReason,
  DealFlagType,
  type AdminDeal,
  type DealFlag,
} from '~/stores/adminDeals'
import {
  MassMessageCode,
  type MassMessage,
  type MassMessageRecipient,
} from '~/stores/adminMassMessages'

// Builders for the shapes the API returns. Each takes overrides for whatever a test cares about

export const accountSimplified = (
  id: number,
  overrides: Partial<AccountSimplified> = {},
): AccountSimplified => ({
  accountId: id,
  nationId: 100 + id,
  nationName: `Nation${id}`,
  rulerName: `Ruler${id}`,
  allianceName: 'Valyria',
  ...overrides,
})

export const slot = (
  id: number,
  sends: { tech?: number; cash?: number } = {},
  overrides: Partial<SlotDetail> = {},
): SlotDetail => ({
  id,
  slotNumber: 1,
  techSentCycles: sends.tech ?? 0,
  techReceivedCycles: 0,
  cashSentCycles: sends.cash ?? 0,
  cashReceivedCycles: 0,
  hasActiveDeal: true,
  account: accountSimplified(id),
  ...overrides,
})

// A buyer's deal with a seller: the buyer (me) sends $9M, the seller sends 100 tech
export const dealDetail = (
  overrides: Partial<DealDetail> = {},
): DealDetail => ({
  id: 1,
  createdOn: '2026-10-01T00:00:00',
  mySlot: slot(1, { cash: 1 }, { techReceivedCycles: 1 }),
  partnerSlot: slot(2, { tech: 1 }, { cashReceivedCycles: 1 }),
  partnerDiscord: 'partner#1',
  lastActivityDate: null,
  state: DealActivityState.AwaitingSend,
  blockedReason: null,
  stateSince: '2026-10-09T00:00:00',
  isMyTurnToSend: false,
  iAmResponsible: false,
  partnerIsResponsible: false,
  daysResponsible: null,
  cancelsOn: null,
  inFlightUntil: null,
  inFlightIsIncoming: false,
  inFlightAccepted: false,
  blockedUntil: null,
  flaggedOn: null,
  ...overrides,
})

export const account = (overrides: Partial<Account> = {}): Account => ({
  id: 1,
  nationId: 101,
  nationName: 'Buyland',
  rulerName: 'Bob',
  allianceName: 'Valyria',
  discord: 'bob',
  discordUniqueId: 111,
  uniqueCode: '12345',
  role: 'B',
  activity: 1,
  strength: 1000,
  infra: 2000,
  tech: 300,
  warStatus: 2,
  hasForeignMinistry: false,
  hasFederalAidCommission: false,
  hasDisasterReliefAgency: false,
  ...overrides,
})

export const adminAccount = (
  id: number,
  overrides: Partial<AdminAccount> = {},
): AdminAccount => ({
  id,
  nationId: 100 + id,
  nationName: `Nation${id}`,
  rulerName: `Ruler${id}`,
  allianceName: 'Valyria',
  discord: null,
  discordUniqueId: null,
  uniqueCode: '12345',
  role: 'B',
  activity: 1,
  strength: 1000,
  infra: 2000,
  tech: 300,
  warStatus: 2,
  nationDeleted: false,
  hasForeignMinistry: false,
  hasFederalAidCommission: false,
  hasDisasterReliefAgency: false,
  slotsUsed: 1,
  slotsTotal: 4,
  openFlagCount: 0,
  ...overrides,
})

// Deal between Ruler1 (slot 1, sends $9M) and Ruler2 (slot 2, sends 100 tech)
export const adminDeal = (overrides: Partial<AdminDeal> = {}): AdminDeal => ({
  id: 10,
  createdOn: '2026-10-01T00:00:00',
  createdBy: 'system',
  lastActivityDate: null,
  state: DealActivityState.AwaitingSend,
  blockedReason: null,
  stateSince: '2026-10-09T00:00:00',
  nextSenderAccountId: 1,
  responsibleAccountId: null,
  daysResponsible: null,
  cancelsOn: null,
  inFlightUntil: null,
  hasOpenFlag: false,
  slot1: slot(1, { cash: 1 }),
  slot2: slot(2, { tech: 1 }),
  ...overrides,
})

export const dealFlag = (overrides: Partial<DealFlag> = {}): DealFlag => ({
  id: 5,
  dealId: 10,
  flagType: DealFlagType.OneDayInactive,
  flagReason: DealFlagReason.AwaitingSend,
  flaggedAccount: accountSimplified(1),
  innocentAccount: accountSimplified(2),
  flaggedOn: '2026-10-09T18:00:00',
  resolvedOn: null,
  resolvedBy: null,
  adminNotes: null,
  ...overrides,
})

export const recipient = (
  id: number,
  overrides: Partial<MassMessageRecipient> = {},
): MassMessageRecipient => ({
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
  ...overrides,
})

export const massMessage = (
  id: number,
  overrides: Partial<MassMessage> = {},
): MassMessage => ({
  id,
  code: MassMessageCode.SendAid,
  name: `Message ${id}`,
  description: `Who gets message ${id}`,
  subject: 'VEP {{date}}',
  message: 'Log in at {{siteUrl}} on {{date}}',
  sentOn: null,
  sentBy: null,
  recipients: [],
  ...overrides,
})

// What useApiClient's apiFetch resolves to
export const apiOk = <T>(data: T) => ({ data: ref(data), error: ref(null) })
export const apiError = (
  error: Record<string, unknown> = { statusCode: 500, message: 'Server Error' },
) => ({
  data: ref(null),
  error: ref(error),
})
