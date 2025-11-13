<template>
  <div>
    <UCard v-if="slotsFree === 0" class="text-center">
      <template #header>
        <h5 class="text-xl font-semibold text-gray-900 dark:text-white">You are not expected to interact with VEP today</h5>
      </template>
      <p class="text-gray-600 dark:text-gray-400">
        Your nation does not have any free foreign aid slots, so you are not expected to send or receive any transactions today. Please check
        again tomorrow for the updated status of your nation.
      </p>
    </UCard>

    <UAccordion v-else :items="accordionItems" :default-open="true">
      <template #default="{ item, open }">
        <UButton color="gray" variant="ghost" class="w-full" :ui="{ rounded: 'rounded-none rounded-t-lg', padding: { sm: 'p-3' } }">
          <template #leading>
            <div class="w-5 h-5 rounded-full" :class="[open ? 'bg-primary-500' : 'bg-gray-400']" />
          </template>

          <span class="truncate text-lg font-semibold">
            <span v-if="owesTech">Tech Targets</span>
            <span v-else-if="owesCash">Cash Targets</span>
            <span v-else-if="expectsCash">Incoming Cash</span>
            <span v-else-if="expectsTech">Incoming Tech</span>
            <span v-else>Potential Transactions</span>
            <span class="font-normal text-gray-600 dark:text-gray-400">({{ aidList.length }} nations)</span>
          </span>
        </UButton>
      </template>

      <template #item="{ item }">
        <div class="p-4 space-y-4">
          <p class="text-gray-700 dark:text-gray-300">
            Please
            <span v-if="owesTech">send tech to </span>
            <span v-else-if="owesCash">send cash to </span>
            <span v-else-if="expectsCash">accept cash from </span>
            <span v-else-if="expectsTech">accept tech from </span>
            <span v-else>Potential Transactions</span>
            any of the nations in the following list.
          </p>
          <p v-if="expectsCash || expectsTech" class="text-red-600 dark:text-red-400 font-medium">
            IMPORTANT: <strong>Do not use this list to send aid. </strong>
            It should only be used to verify that nations sending you aid are doing so legitimately.
          </p>

          <UTable :rows="aidList" :columns="columns">
            <template #nation-data="{ row }">
              <a v-if="owesCash || owesTech" :href="getAidNationLink(row.nation)" target="_blank" class="text-primary-600 hover:text-primary-700 dark:text-primary-400 dark:hover:text-primary-300">
                {{ row.nation.rulerName }} of {{ row.nation.nationName }}
              </a>
              <span v-else class="text-gray-900 dark:text-gray-100">
                {{ row.nation.rulerName }} of {{ row.nation.nationName }}
              </span>
            </template>

            <template #alliance-data="{ row }">
              <div class="flex items-center gap-2">
                <ChipAllianceAcronym :allianceName="row.nation.allianceName" />
                <span class="text-gray-700 dark:text-gray-300">{{ row.nation.allianceName }}</span>
              </div>
            </template>

            <template #slotsFree-data="{ row }">
              <UBadge color="blue" variant="soft">{{ row.slotsFree }}</UBadge>
            </template>

            <template #discord-data="{ row }">
              <span class="text-gray-700 dark:text-gray-300">{{ row.discord }}</span>
            </template>
          </UTable>
        </div>
      </template>
    </UAccordion>
  </div>
</template>

<script setup lang="ts">
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'

interface Nation {
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
}

interface AidListItem {
  nation: Nation
  slotsFree: number
  discord: string
}

interface Account {
  slotsFull: number
  slotsUsed: number
  role: string
  credit: number
  debt: number
  nationId: number
}

interface Props {
  account: Account
  aidList: AidListItem[]
}

const props = defineProps<Props>()

const columns = [
  { key: 'nation', label: 'Nation' },
  { key: 'alliance', label: 'Alliance' },
  { key: 'slotsFree', label: 'Slots Free' },
  { key: 'discord', label: 'Discord Tag' }
]

const accordionItems = computed(() => [
  {
    label: 'Aid List',
    defaultOpen: true
  }
])

const slotsFree = computed(() => {
  return props.account.slotsFull - props.account.slotsUsed
})

const owesCash = computed(() => {
  const rolesThatSendCash = [ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.cashDonor, ACCOUNT_ROLE_CODES.temporaryDonor]
  return rolesThatSendCash.includes(props.account.role) && props.account.credit === 0
})

const expectsCash = computed(() => {
  if (props.account.role === ACCOUNT_ROLE_CODES.seller || props.account.role === ACCOUNT_ROLE_CODES.probationarySeller) {
    return props.account.debt === 0
  } else if ([ACCOUNT_ROLE_CODES.cashCollector, ACCOUNT_ROLE_CODES.temporaryCollector].includes(props.account.role)) {
    return props.account.credit === 0
  } else {
    return false
  }
})

const owesTech = computed(() => {
  const rolesThatSendTech = [
    ACCOUNT_ROLE_CODES.seller, ACCOUNT_ROLE_CODES.probationarySeller,
    ACCOUNT_ROLE_CODES.techFarm, ACCOUNT_ROLE_CODES.temporaryFarm
  ]

  return rolesThatSendTech.includes(props.account.role) && props.account.debt > 0
})

const expectsTech = computed(() => {
  const rolesThatExpectTech = [
    ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.techReceiver, ACCOUNT_ROLE_CODES.temporaryReceiver
  ]

  return rolesThatExpectTech.includes(props.account.role) && props.account.credit > 0
})

const getAidNationLink = (nation: Nation) => {
  return `https://www.cybernations.net/aid_form.asp?Nation_ID=${nation.nationId}&bynation=${props.account.nationId}`
}
</script>
