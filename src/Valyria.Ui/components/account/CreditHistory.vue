<template>
  <UAccordion :items="accordionItems">
    <template #default="{ item, open }">
      <UButton color="gray" variant="ghost" class="w-full" :ui="{ rounded: 'rounded-none rounded-t-lg', padding: { sm: 'p-3' } }">
        <template #leading>
          <div class="w-5 h-5 rounded-full" :class="[open ? 'bg-primary-500' : 'bg-gray-400']" />
        </template>

        <span class="truncate text-lg font-semibold">
          Credit History
          <span class="font-normal text-gray-600 dark:text-gray-400">
            +{{ creditHistory.filter(txn => txn.balanceChangeType === "Credit").length }} out,
            -{{ creditHistory.filter(txn => txn.balanceChangeType === "Debt").length }} in
          </span>
        </span>
      </UButton>
    </template>

    <template #item="{ item }">
      <div class="p-4 space-y-4">
        <p class="text-gray-700 dark:text-gray-300">
          Your entire history of credit changes, as recorded in the VEP. This history includes both aid-based transactions, as well as manually-
          configured credits or debts that may have been added to your account by an admin. Please contact VEP Management if you have any
          concerns about your credit history.
        </p>

        <UTable :rows="creditHistory" :columns="columns">
          <template #otherNation-data="{ row }">
            <span class="text-gray-900 dark:text-gray-100">
              {{ row.otherNation !== null ? `${row.otherNation.rulerName} of ${row.otherNation.nationName}` : "VEP Management" }}
            </span>
          </template>

          <template #type-data="{ row }">
            <span class="text-gray-700 dark:text-gray-300">{{ row.type }}</span>
          </template>

          <template #status-data="{ row }">
            <UBadge :color="getStatusColor(row.status)" variant="soft">
              {{ row.otherNation !== null ? toAidStatusDescription(row.status) : "Active" }}
            </UBadge>
          </template>

          <template #money-data="{ row }">
            <span class="text-gray-700 dark:text-gray-300">{{ row.money !== null ? row.money : "N/A" }}</span>
          </template>

          <template #technology-data="{ row }">
            <span class="text-gray-700 dark:text-gray-300">{{ row.technology !== null ? row.technology : "N/A" }}</span>
          </template>

          <template #balanceChangeType-data="{ row }">
            <span :class="row.balanceChangeType === 'Credit' ? 'text-green-600 dark:text-green-400 font-semibold' : 'text-red-600 dark:text-red-400 font-semibold'">
              {{ toCreditChange(row) }}
            </span>
          </template>

          <template #sentOn-data="{ row }">
            <span class="text-gray-700 dark:text-gray-300">{{ row.sentOn !== null ? row.sentOn.split('T')[0] : "N/A" }}</span>
          </template>
        </UTable>
      </div>
    </template>
  </UAccordion>
</template>

<script setup lang="ts">
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'

interface Nation {
  nationName: string
  rulerName: string
}

interface CreditHistoryItem {
  otherNation: Nation | null
  type: string
  status: number
  money: number | null
  technology: number | null
  balanceChangeType: string
  sentOn: string | null
  rate: number
}

interface Props {
  accountRole: string
  creditHistory: CreditHistoryItem[]
}

const props = defineProps<Props>()

const columns = [
  { key: 'otherNation', label: 'Other Nation' },
  { key: 'type', label: 'Type' },
  { key: 'status', label: 'Status' },
  { key: 'money', label: 'Money' },
  { key: 'technology', label: 'Tech' },
  { key: 'balanceChangeType', label: 'Credit Change' },
  { key: 'sentOn', label: 'Sent On' }
]

const accordionItems = computed(() => [
  {
    label: 'Credit History',
    defaultOpen: false
  }
])

const toAidStatusDescription = (statusId: number): string => {
  switch (statusId) {
    case 1: return "Pending"
    case 2: return "Approved"
    case 3: return "Cancelled"
    case 4: return "Expired"
    default: return "Unknown"
  }
}

const getStatusColor = (statusId: number): string => {
  switch (statusId) {
    case 1: return "yellow"
    case 2: return "green"
    case 3: return "gray"
    case 4: return "red"
    default: return "gray"
  }
}

const toCreditChange = (txn: CreditHistoryItem): string => {
  const positivityIndicator = txn.balanceChangeType === "Credit" ? "+" : "-"
  let cashOrTech: string
  let receivedOrSent: string

  if (txn.money !== null) {
    cashOrTech = txn.money > 0 ? "$9m" : "100 tech"
    receivedOrSent = txn.type === "Incoming" ? "received" : "sent"
  } else {
    const cash = `$${txn.rate}m`
    const tech = `${txn.rate / 9 * 100} tech`
    switch (props.accountRole) {
      case ACCOUNT_ROLE_CODES.buyer:
        cashOrTech = txn.balanceChangeType === "Credit" ? tech : cash
        break
      case ACCOUNT_ROLE_CODES.seller:
      case ACCOUNT_ROLE_CODES.probationarySeller:
        cashOrTech = txn.balanceChangeType === "Credit" ? cash : tech
        break
      default:
        // If we can't figure out why they were given a manual txn, then just call it units
        return `${txn.rate} units`
    }

    receivedOrSent = txn.balanceChangeType === "Credit" ? "added" : "deducted"
  }

  return `${positivityIndicator}${cashOrTech} ${receivedOrSent}`
}
</script>
