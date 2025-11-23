<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">
          {{ headerTitle }}
        </h2>
        <UBadge v-if="slotsFree > 0" color="primary" size="lg" variant="subtle">
          {{ props.aidList.length }} nations
        </UBadge>
      </div>
    </template>

    <!-- No Slots Free Message -->
    <div v-if="slotsFree === 0" class="text-center py-8 space-y-4">
      <UIcon name="i-heroicons-check-circle" class="w-16 h-16 text-green-500 mx-auto" />
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        You are not expected to interact with VEP today
      </h3>
      <p class="text-gray-600 dark:text-gray-400 max-w-2xl mx-auto">
        Your nation does not have any free foreign aid slots, so you are not expected to send or receive any transactions today.
        Please check again tomorrow for the updated status of your nation.
      </p>
    </div>

    <!-- Aid List Content -->
    <div v-else class="space-y-4">
      <UAlert
        :color="alertColor"
        :icon="alertIcon"
        :title="alertTitle"
        :description="alertDescription"
      />

      <UTable :data="data" :columns="columns" />
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui';
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'
import type { Account, AidItem } from '~/stores/account';
import type { NationSimplified } from '~/stores/shared/nationSimplified';

interface Props {
  account: Account
  aidList: AidItem[]
}

interface AidListTableRow {
  nation: NationSimplified
  alliance: string 
  slotsFree: number 
  discord: string
}

const props = withDefaults(defineProps<Props>(), {
  account: () => ({} as Account),
  aidList: () => []
})

const data = computed<AidListTableRow[]>(() => props.aidList.map((item) => ({
  nation: item.nation,
  alliance: item.nation.allianceName,
  slotsFree: item.slotsFree,
  discord: item.discord
})));

const columns: TableColumn<AidListTableRow>[] = [
  {
    accessorKey: 'nation',
    header: 'Nation',
    cell: ({ row }) => {
      const nation = row.getValue<NationSimplified>('nation');
      return `${nation.rulerName} of ${nation.nationName}`;
    }
  },
  {
    accessorKey: 'alliance',
    header: 'Alliance',
    cell: ({ row }) => {
      const aidList = row.original
      return h('div', { class: 'flex items-center gap-3 flex-1' }, [
        h(resolveComponent('ChipAllianceAcronym'), {
          allianceName: aidList.alliance
        }),
        h('span', aidList.alliance)
      ])
    }
  },
  {
    accessorKey: 'slotsFree',
    header: 'Slots Free'
  },
  {
    accessorKey: 'discord',
    header: 'Discord Tag'
  }
]

const headerTitle = computed(() => {
  if (owesTech.value) return 'Tech Targets'
  else if (owesCash.value) return 'Cash Targets'
  else if (expectsCash.value) return 'Incoming Cash'
  else if (expectsTech.value) return 'Incoming Tech'
  else return 'Aid List'
})

const alertColor = computed(() => {
  if (expectsCash.value || expectsTech.value) return 'error'
  else if (owesCash.value || owesTech.value) return 'primary'
  else return 'info'
})

const alertIcon = computed(() => {
  if (expectsCash.value || expectsTech.value) return 'i-heroicons-exclamation-triangle'
  else if (owesCash.value || owesTech.value) return 'i-heroicons-paper-airplane'
  else return 'i-heroicons-information-circle'
})

const alertTitle = computed(() => {
  if (owesTech.value) return 'Send tech to these nations'
  else if (owesCash.value) return 'Send cash to these nations'
  else if (expectsCash.value) return 'Accept cash from these nations'
  else if (expectsTech.value) return 'Accept tech from these nations'
  else return 'Potential Transactions'
})

const alertDescription = computed(() => {
  let baseMessage = 'Please '
  if (owesTech.value) baseMessage += 'send tech to '
  else if (owesCash.value) baseMessage += 'send cash to '
  else if (expectsCash.value) baseMessage += 'accept cash from '
  else if (expectsTech.value) baseMessage += 'accept tech from '
  baseMessage += 'any of the nations in the following list.'

  if (expectsCash.value || expectsTech.value) {
    baseMessage += ' IMPORTANT: Do not use this list to send aid. It should only be used to verify that nations sending you aid are doing so legitimately.'
  }

  return baseMessage
})

const slotsFree = computed(() => {
  return props.account.slotsFull - props.account.slotsUsed
})

const owesCash = computed(() => {
  const rolesThatSendCash: string[] = [ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.cashDonor, ACCOUNT_ROLE_CODES.temporaryDonor]
  return rolesThatSendCash.includes(props.account.role) && props.account.credit === 0
})

const expectsCash = computed(() => {
  if (props.account.role === ACCOUNT_ROLE_CODES.seller || props.account.role === ACCOUNT_ROLE_CODES.probationarySeller) {
    return props.account.debt === 0
  } else if (props.account.role === ACCOUNT_ROLE_CODES.cashCollector || props.account.role === ACCOUNT_ROLE_CODES.temporaryCollector) {
    return props.account.credit === 0
  } else {
    return false
  }
})

const owesTech = computed(() => {
  const rolesThatSendTech: string[] = [
    ACCOUNT_ROLE_CODES.seller, ACCOUNT_ROLE_CODES.probationarySeller,
    ACCOUNT_ROLE_CODES.techFarm, ACCOUNT_ROLE_CODES.temporaryFarm
  ]

  return rolesThatSendTech.includes(props.account.role) && props.account.debt > 0
})

const expectsTech = computed(() => {
  const rolesThatExpectTech: string[] = [
    ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.techReceiver, ACCOUNT_ROLE_CODES.temporaryReceiver
  ]

  return rolesThatExpectTech.includes(props.account.role) && props.account.credit > 0
})

const getAidNationLink = (nation: NationSimplified) => {
  return `https://www.cybernations.net/aid_form.asp?Nation_ID=${nation.nationId}&bynation=${props.account.nationId}`
}
</script>
