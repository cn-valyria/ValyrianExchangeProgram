<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">
          Active Deals
        </h2>
        <UBadge
          v-if="props.deals.length > 0"
          color="primary"
          size="lg"
          variant="subtle"
        >
          {{ props.deals.length }}
          {{ props.deals.length === 1 ? 'deal' : 'deals' }}
        </UBadge>
      </div>
    </template>

    <div class="space-y-4">
      <!-- Deals cancelled over a flag. Shown even with no active deals, since they can pause matching -->
      <UAlert
        v-for="(notification, index) in props.notifications"
        :key="`notification-${index}`"
        :color="notification.type === 'error' ? 'error' : 'info'"
        :icon="
          notification.type === 'error'
            ? 'i-heroicons-flag'
            : 'i-heroicons-information-circle'
        "
        :title="notification.message"
      />

      <!-- On Hold Message -->
      <div v-if="isOnHold" class="text-center py-8 space-y-4">
        <UIcon
          name="i-heroicons-pause-circle"
          class="w-16 h-16 text-gray-400 mx-auto"
        />
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
          Your account is on hold
        </h3>
        <p class="text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
          Accounts on hold are not matched into deals. If you feel this is an
          error, please contact VEP management for support.
        </p>
      </div>

      <!-- No Deals Message -->
      <div
        v-else-if="props.deals.length === 0"
        class="text-center py-8 space-y-4"
      >
        <UIcon
          name="i-heroicons-clock"
          class="w-16 h-16 text-blue-500 mx-auto"
        />
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
          Waiting to be matched
        </h3>
        <p class="text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
          You don't have any active deals yet. VEP matches open slots with new
          partners daily. Please check again tomorrow for your updated deals.
        </p>
      </div>

      <!-- Deals Content -->
      <template v-else>
        <UAlert
          color="info"
          icon="i-heroicons-information-circle"
          title="Keeping your deals active"
          description="You and each partner take turns: whoever sends cash goes first, then the other side sends, and so on. Each aid offer fills an aid slot on both nations for 10 days, so a deal only needs you when it's your turn and you both have a free slot. Accept offers before they expire, and keep your slots free of aid outside VEP. Click a partner you send aid to and you'll go straight to the aid form."
        />

        <UAlert
          v-if="temporaryDeals.length > 0"
          color="warning"
          icon="i-heroicons-clock"
          :title="temporaryDealsTitle"
          :description="temporaryDealsDescription"
        />

        <UTable :data="data" :columns="columns" />
      </template>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'
import {
  describeAid,
  describeNextAid,
  getAidFormLink,
  getDealStatus,
  getNationLink,
  type DealStatusInfo,
} from '~/infrastructure/deals'
import {
  describeTemporaryDeal,
  formatCnDate,
} from '~/infrastructure/teamEvents'
import type { Account, DealDetail, DealNotification } from '~/stores/account'
import type { NationSimplified } from '~/stores/shared/nationSimplified'

interface Props {
  account: Account
  deals: DealDetail[]
  notifications: DealNotification[]
}

interface ActiveDealRow {
  slotNumber: number
  isTeamBonusSlot: boolean
  // Set when the deal uses a team bonus slot on either side
  teamBonusEndsOn: string | null
  partner: NationSimplified
  alliance: string
  discord: string
  youSend: string
  youReceive: string
  sendsAid: boolean
  nextAid: string
  isMyTurnToSend: boolean
  lastActivity: string
  status: DealStatusInfo
}

const props = withDefaults(defineProps<Props>(), {
  account: () => ({}) as Account,
  deals: () => [],
  notifications: () => [],
})

const isOnHold = computed(
  () => props.account.role === ACCOUNT_ROLE_CODES.onHold,
)

const data = computed<ActiveDealRow[]>(() =>
  props.deals
    .map((deal) => ({
      slotNumber: deal.mySlot.slotNumber,
      isTeamBonusSlot: deal.mySlot.isTeamBonus,
      teamBonusEndsOn: deal.teamBonusEndsOn,
      partner: deal.partnerSlot.account,
      alliance: deal.partnerSlot.account.allianceName,
      discord: deal.partnerDiscord || '',
      youSend: describeAid(
        deal.mySlot.techSentCycles,
        deal.mySlot.cashSentCycles,
      ),
      youReceive: describeAid(
        deal.mySlot.techReceivedCycles,
        deal.mySlot.cashReceivedCycles,
      ),
      sendsAid: deal.mySlot.techSentCycles + deal.mySlot.cashSentCycles > 0,
      nextAid: describeNextAid(deal),
      isMyTurnToSend: deal.isMyTurnToSend,
      lastActivity: deal.lastActivityDate
        ? deal.lastActivityDate.split('T')[0]
        : 'None yet',
      status: getDealStatus(deal),
    }))
    .sort((a, b) => a.slotNumber - b.slotNumber),
)

// Deals on a team event's bonus slot, mine or the partner's, end with the event
const temporaryDeals = computed(() =>
  props.deals.filter((deal) => deal.teamBonusEndsOn),
)

const temporaryDealsTitle = computed(() =>
  temporaryDeals.value.length === 1
    ? '1 of your deals is temporary'
    : `${temporaryDeals.value.length} of your deals are temporary`,
)

const temporaryDealsDescription = computed(() =>
  temporaryDeals.value
    .map((deal) => {
      const partner = deal.partnerSlot.account.nationName
      const whose = deal.mySlot.isTeamBonus
        ? "your team's bonus aid slot"
        : `${partner}'s team bonus aid slot`
      return `Your deal with ${partner} uses ${whose}, so VEP cancels it once the team event ends after ${formatCnDate(deal.teamBonusEndsOn!)}.`
    })
    .join(' '),
)

const columns: TableColumn<ActiveDealRow>[] = [
  {
    accessorKey: 'slotNumber',
    header: 'Slot',
    cell: ({ row }) => {
      const deal = row.original
      if (!deal.teamBonusEndsOn) return `#${deal.slotNumber}`
      return h('div', { class: 'flex items-center gap-2' }, [
        h('span', `#${deal.slotNumber}`),
        h(
          resolveComponent('UTooltip'),
          { text: describeTemporaryDeal(deal.teamBonusEndsOn) },
          () =>
            h(
              resolveComponent('UBadge'),
              { color: 'warning', variant: 'subtle', size: 'sm' },
              () =>
                `${deal.isTeamBonusSlot ? 'Team bonus' : 'Temporary'}, ends ${formatCnDate(deal.teamBonusEndsOn!)}`,
            ),
        ),
      ])
    },
  },
  {
    accessorKey: 'partner',
    header: 'Partner',
    cell: ({ row }) => {
      const deal = row.original
      return h(
        'a',
        {
          href: deal.sendsAid
            ? getAidFormLink(deal.partner, props.account.nationId)
            : getNationLink(deal.partner),
          target: '_blank',
          class: 'font-medium text-fg-brand hover:underline',
        },
        `${deal.partner.rulerName} of ${deal.partner.nationName}`,
      )
    },
  },
  {
    accessorKey: 'alliance',
    header: 'Alliance',
    cell: ({ row }) =>
      h(resolveComponent('ChipAllianceAcronym'), {
        allianceName: row.original.alliance,
      }),
  },
  {
    accessorKey: 'discord',
    header: 'Discord Tag',
  },
  {
    accessorKey: 'youSend',
    header: 'You Send',
    cell: ({ row }) =>
      h(
        'span',
        { class: row.original.sendsAid ? 'font-semibold' : 'text-gray-400' },
        row.original.youSend,
      ),
  },
  {
    accessorKey: 'youReceive',
    header: 'You Receive',
  },
  {
    accessorKey: 'nextAid',
    header: 'Next Aid',
    cell: ({ row }) =>
      h(
        'span',
        { class: row.original.isMyTurnToSend ? 'font-semibold' : '' },
        row.original.nextAid,
      ),
  },
  {
    accessorKey: 'lastActivity',
    header: 'Last Aid',
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => {
      const status = row.original.status
      return h(resolveComponent('UTooltip'), { text: status.detail }, () =>
        h(
          resolveComponent('UBadge'),
          {
            color: status.color,
            variant: 'subtle',
          },
          () => status.label,
        ),
      )
    },
  },
]
</script>
