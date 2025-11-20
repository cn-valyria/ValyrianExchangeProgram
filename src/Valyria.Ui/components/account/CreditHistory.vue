<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Credit History</h2>
        <div class="flex gap-2">
          <UBadge color="success" size="lg" variant="subtle">
            +{{ creditCount }} out
          </UBadge>
          <UBadge color="error" size="lg" variant="subtle">
            -{{ debtCount }} in
          </UBadge>
        </div>
      </div>
    </template>

    <div class="space-y-4">
      <UAlert
        color="info"
        icon="i-heroicons-information-circle"
        title="About Your Credit History"
        description="Your entire history of credit changes, as recorded in the VEP. This history includes both aid-based transactions, as well as manually-configured credits or debts that may have been added to your account by an admin. Please contact VEP Management if you have any concerns about your credit history."
      />

      <UTable :data="props.creditHistory" :columns="columns" />
    </div>
  </UCard>
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
    accessorKey: 'rate',
    meta: {
      class: {
        th: "hidden",
        td: "hidden"
      }
    }
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

const creditCount = computed(() =>
  props.creditHistory.filter((txn: CreditHistoryItem) => txn.balanceChangeType === "Credit").length
)

const debtCount = computed(() =>
  props.creditHistory.filter((txn: CreditHistoryItem) => txn.balanceChangeType === "Debt").length
)

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
