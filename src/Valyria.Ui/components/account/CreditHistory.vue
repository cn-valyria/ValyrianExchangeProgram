<template>
  <UAccordion :items="accordionItems">
    <template #credit-history>
      <div class="p-4 space-y-4">
        <p class="text-gray-700 dark:text-gray-300">
          Your entire history of credit changes, as recorded in the VEP. This history includes both aid-based transactions, as well as manually-
          configured credits or debts that may have been added to your account by an admin. Please contact VEP Management if you have any
          concerns about your credit history.
        </p>

        <UTable :data="props.creditHistory" :columns="columns">
          <!-- <template #otherNation-data="{ row }">
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
          </template> -->
        </UTable>
      </div>
    </template>
  </UAccordion>
</template>

<script setup lang="ts">
import type { TableColumn, TableRow } from '@nuxt/ui';
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'
import type { CreditHistoryItem } from '~/stores/account';
import type { NationSimplified } from '~/stores/shared/nationSimplified';

interface Props {
  accountRole: string
  creditHistory: CreditHistoryItem[]
}

const props = defineProps<Props>()

const columns: TableColumn<CreditHistoryItem>[] = [
  {
    accessorKey: 'otherNation',
    header: 'Other Nation',
    cell: ({ row }) => {
      const otherNation = row.getValue('otherNation') as NationSimplified;
      return otherNation !== null ? `${otherNation.rulerName} of ${otherNation.nationName}` : "VEP Management";
    }
  },
  {
    accessorKey: 'type',
    header: 'Type'
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => row.getValue('otherNation') !== null ? toAidStatusDescription(row.getValue<number>('status')) : "Active"
  },
  {
    accessorKey: 'money',
    header: 'Money',
    cell: ({ row }) => row.getValue('money') !== null ? row.getValue('money') : "N/A"
  },
  {
    accessorKey: 'technology',
    header: 'Tech',
    cell: ({ row }) => row.getValue('technology') !== null ? row.getValue('technology') : "N/A"
  },
  {
    accessorKey: 'balanceChangeType',
    header: 'Credit Change',
    cell: ({ row }) => toCreditChange(row)
  },
  {
    accessorKey: 'sentOn',
    header: 'Sent On',
    cell: ({ row }) => row.getValue('sentOn') !== null ? row.getValue<string>('sentOn').split('T')[0] : "N/A"
  }
]

const accordionItems = computed(() => [
  {
    label: `Credit History (+${props.creditHistory.filter((txn: CreditHistoryItem) => txn.balanceChangeType === "Credit").length} out, -${props.creditHistory.filter((txn: CreditHistoryItem) => txn.balanceChangeType === "Debt").length} in)`,
    defaultOpen: false,
    slot: 'credit-history'
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

const toCreditChange = (row: TableRow<CreditHistoryItem>): string => {
  const txn = {
    money: row.getValue<number>("money"),
    type: row.getValue<string>("type"),
    rate: row.getValue<number>("rate"),
    balanceChangeType: row.getValue("balanceChangeType")
  };

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
