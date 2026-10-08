<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Active Deals</h2>
        <UBadge v-if="props.deals.length > 0" color="primary" size="lg" variant="subtle">
          {{ props.deals.length }} {{ props.deals.length === 1 ? 'deal' : 'deals' }}
        </UBadge>
      </div>
    </template>

    <!-- On Hold Message -->
    <div v-if="isOnHold" class="text-center py-8 space-y-4">
      <UIcon name="i-heroicons-pause-circle" class="w-16 h-16 text-gray-400 mx-auto" />
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        Your account is on hold
      </h3>
      <p class="text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
        Accounts on hold are not matched into deals. If you feel this is an error, please contact VEP management for support.
      </p>
    </div>

    <!-- No Deals Message -->
    <div v-else-if="props.deals.length === 0" class="text-center py-8 space-y-4">
      <UIcon name="i-heroicons-clock" class="w-16 h-16 text-blue-500 mx-auto" />
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        Waiting to be matched
      </h3>
      <p class="text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
        You don't have any active deals yet. VEP matches open slots with new partners daily.
        Please check again tomorrow for your updated deals.
      </p>
    </div>

    <!-- Deals Content -->
    <div v-else class="space-y-4">
      <UAlert
        v-for="(notification, index) in props.notifications"
        :key="`notification-${index}`"
        :color="notification.type === 'info' ? 'info' : notification.type"
        icon="i-heroicons-exclamation-triangle"
        :title="notification.message"
      />

      <UAlert
        color="info"
        icon="i-heroicons-information-circle"
        title="Keeping your deals active"
        description="Each deal pairs one of your slots with a partner's slot. Send what's listed under 'You Send' to each partner, and accept what they send you, every 10 days. Deals that go too long without aid are cancelled and your slot is re-matched. Click a partner you send aid to and you'll go straight to the aid form."
      />

      <UTable :data="data" :columns="columns" />
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'
import { describeAid, getDealStateColor, getDealStateFromActivity, getDealStateLabel, type DealState } from '~/infrastructure/deals'
import type { Account, DealDetail, DealNotification } from '~/stores/account'
import type { NationSimplified } from '~/stores/shared/nationSimplified'

interface Props {
  account: Account
  deals: DealDetail[]
  notifications: DealNotification[]
}

interface ActiveDealRow {
  slotNumber: number
  partner: NationSimplified
  alliance: string
  discord: string
  youSend: string
  youReceive: string
  sendsAid: boolean
  lastActivity: string
  state: DealState
  stateDetail: string | null
}

const props = withDefaults(defineProps<Props>(), {
  account: () => ({} as Account),
  deals: () => [],
  notifications: () => []
})

const isOnHold = computed(() => props.account.role === ACCOUNT_ROLE_CODES.onHold)

const flaggedDealIds = computed(() => new Set(props.notifications.map((n) => n.dealId)))

const data = computed<ActiveDealRow[]>(() => props.deals
  .map((deal) => ({
    slotNumber: deal.mySlot.slotNumber,
    partner: deal.partnerSlot.account,
    alliance: deal.partnerSlot.account.allianceName,
    discord: deal.partnerDiscord || '',
    youSend: describeAid(deal.mySlot.techSentCycles, deal.mySlot.cashSentCycles),
    youReceive: describeAid(deal.mySlot.techReceivedCycles, deal.mySlot.cashReceivedCycles),
    sendsAid: deal.mySlot.techSentCycles + deal.mySlot.cashSentCycles > 0,
    lastActivity: deal.lastActivityDate ? deal.lastActivityDate.split('T')[0] : 'None yet',
    state: getDealState(deal),
    stateDetail: deal.actionNeeded
  }))
  .sort((a, b) => a.slotNumber - b.slotNumber))

const columns: TableColumn<ActiveDealRow>[] = [
  {
    accessorKey: 'slotNumber',
    header: 'Slot',
    cell: ({ row }) => `#${row.original.slotNumber}`
  },
  {
    accessorKey: 'partner',
    header: 'Partner',
    cell: ({ row }) => {
      const deal = row.original
      return h('a', {
        href: deal.sendsAid ? getAidFormLink(deal.partner) : getNationLink(deal.partner),
        target: '_blank',
        class: 'font-medium text-fg-brand hover:underline'
      }, `${deal.partner.rulerName} of ${deal.partner.nationName}`)
    }
  },
  {
    accessorKey: 'alliance',
    header: 'Alliance',
    cell: ({ row }) => h(resolveComponent('ChipAllianceAcronym'), {
      allianceName: row.original.alliance
    })
  },
  {
    accessorKey: 'discord',
    header: 'Discord Tag'
  },
  {
    accessorKey: 'youSend',
    header: 'You Send',
    cell: ({ row }) => h('span', { class: row.original.sendsAid ? 'font-semibold' : 'text-gray-400' }, row.original.youSend)
  },
  {
    accessorKey: 'youReceive',
    header: 'You Receive'
  },
  {
    accessorKey: 'lastActivity',
    header: 'Last Aid'
  },
  {
    accessorKey: 'state',
    header: 'Status',
    cell: ({ row }) => {
      const badge = h(resolveComponent('UBadge'), {
        color: getDealStateColor(row.original.state),
        variant: 'subtle'
      }, () => getDealStateLabel(row.original.state))

      return row.original.stateDetail
        ? h(resolveComponent('UTooltip'), { text: row.original.stateDetail }, () => badge)
        : badge
    }
  }
]

const getDealState = (deal: DealDetail): DealState =>
  getDealStateFromActivity(flaggedDealIds.value.has(deal.id), deal.daysSinceActivity, deal.lastActivityDate)

const getAidFormLink = (nation: NationSimplified) => {
  return `https://www.cybernations.net/aid_form.asp?Nation_ID=${nation.nationId}&bynation=${props.account.nationId}`
}

const getNationLink = (nation: NationSimplified) => {
  return `https://www.cybernations.net/nation_drill_display.asp?Nation_ID=${nation.nationId}`
}
</script>
