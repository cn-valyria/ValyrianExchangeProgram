<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Credit History</h2>
        <div class="flex gap-2">
          <UBadge color="success" size="lg" variant="subtle">
            +{{ accountTransactionHistory.totalCreditTxns }} out
          </UBadge>
          <UBadge color="error" size="lg" variant="subtle">
            -{{ accountTransactionHistory.totalDebtTxns }} in
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

      <UTable :data="props.creditHistory" :columns="columns" :loading="tableIsLoading" />

      <!-- Pagination -->
      <div class="flex justify-between items-center">
        <div class="text-sm text-gray-600 dark:text-gray-400">
          Showing {{ Math.min((currentPage - 1) * limit + 1, currentTransactionsPage.totalCount) }} to {{ Math.min(currentPage * limit, currentTransactionsPage.totalCount) }} of {{ currentTransactionsPage.totalCount }} results
        </div>
        <UPagination
          v-model:page="currentPage"
          :total="currentTransactionsPage.totalCount"
          :items-per-page="limit"
        />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { TableColumn, TableRow } from '@nuxt/ui';
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'
import type { CreditHistoryItem } from '~/stores/account';
import { AidStatus } from '~/stores/shared/enums';
import type { NationSimplified } from '~/stores/shared/nationSimplified';
import type { TransactionsPage } from '~/stores/shared/transactionsPage';

const toast = useToast()

const accountStore = useAccountStore()

const { accountTransactionHistory } = storeToRefs(accountStore)

interface Props {
  accountId: number,
  accountRole: string
  creditHistory: CreditHistoryItem[]
}

const props = withDefaults(defineProps<Props>(), {
  accountId: () => 0,
  accountRole: () => "",
  creditHistory: () => []
})

const tableIsLoading = ref<boolean>(false)
const currentPage = ref<number>(1)
const limit = ref<number>(10)

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
    cell: ({ row }) => {
      const creditHistoryItem = row.original
      const status = creditHistoryItem.otherNation !== null ? creditHistoryItem.status : AidStatus.Approved
      return h(resolveComponent('UBadge'), {
        color: getStatusColor(status),
        variant: 'subtle'
      }, () => toAidStatusDescription(status))
    }
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
    cell: ({ row }) => {
      const amount = row.original.money
      return amount === 0 ? '0' : new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
      }).format(amount);
    }
  },
  {
    accessorKey: 'technology',
    header: 'Tech',
    cell: ({ row }) => row.original.technology || 0
  },
  {
    accessorKey: 'balanceChangeType',
    header: 'Credit Change',
    cell: ({ row }) => {
      const creditHistoryItem = row.original
      return h('span',
        {
          class: `${creditHistoryItem.balanceChangeType == "Credit" ? "font-bold text-green-700" : "text-red-500"} dark:text-white`
        },
        toCreditChange(creditHistoryItem)
      )
    }
  },
  {
    accessorKey: 'sentOn',
    header: 'Sent On',
    cell: ({ row }) => row.getValue('sentOn') !== null ? row.getValue<string>('sentOn').split('T')[0] : "N/A"
  }
]

const currentTransactionsPage = computed<TransactionsPage>(() => accountTransactionHistory.value.currentPage)

// Helper function to get status color for badges
const getStatusColor = (statusId: string | number): 'primary' | 'success' | 'error' | 'neutral' | 'warning' => {
  const status = typeof statusId === 'string' ? parseInt(statusId) : statusId
  switch (status) {
    case 1: return 'warning' // Pending
    case 2: return 'success' // Approved
    case 3: return 'error' // Cancelled
    case 4: return 'neutral' // Expired
    default: return 'neutral'
  }
}

const toAidStatusDescription = (statusId: number): string => {
  switch (statusId) {
    case 1: return "Pending"
    case 2: return "Approved"
    case 3: return "Cancelled"
    case 4: return "Expired"
    default: return "Unknown"
  }
}

const toCreditChange = (row: CreditHistoryItem): string => {
  const txn = {
    money: row.money,
    type: row.type,
    rate: row.rate,
    balanceChangeType: row.balanceChangeType
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

// Change page handler
const changePage = async (newPageNumber: number): Promise<void> => {
  tableIsLoading.value = true
  currentPage.value = newPageNumber

  try {
    await accountStore.loadTransactionHistory(
      props.accountId,
      limit.value,
      limit.value * (currentPage.value - 1)
    )
  } catch (error) {
    console.error('Failed to load transactions:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load transaction history. Please try again.',
      duration: 5000
    })
  } finally {
    tableIsLoading.value = false
  }
}

watch(currentPage, async (newPage) => {
  await changePage(newPage)
})
</script>
