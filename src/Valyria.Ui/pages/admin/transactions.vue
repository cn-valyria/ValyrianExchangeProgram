<template>
  <UContainer>
    <div class="py-8 space-y-8">
      <!-- Header -->
      <div class="flex justify-between items-start">
        <div>
          <h1 class="text-3xl font-bold text-gray-900 dark:text-white">VEP Transactions</h1>
          <p class="text-gray-600 dark:text-gray-400 mt-2">Manage every transaction that VEP tracks, as well as manual balance adjustments</p>
        </div>
        <UButton
          icon="i-heroicons-funnel"
          label="Filters"
          color="primary"
          @click="openFilterSlideover()"
        />
      </div>

      <!-- Tabs and Tables -->
    <UTabs v-model="activeTab" :items="tabs" class="w-full">
      <!-- Aid-Based Tab -->
      <template #aid-based>
        <UCard>
          <div class="space-y-4">
            <UTable
              :data="aidBasedTransactions"
              :columns="displayAidBasedColumns"
              :loading="tableIsLoading"
            />

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

      <!-- Manual Tab -->
      <template #manual>
        <UCard>
          <div class="space-y-4">
            <div v-if="!isReadOnly" class="flex justify-end">
              <UButton label="Create Transaction" color="success" @click="createTransaction()" />
            </div>

            <UTable
              :data="manualTransactions"
              :columns="displayManualColumns"
              :loading="tableIsLoading"
            />

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
    </UTabs>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { storeToRefs } from 'pinia'
import { useAdminTransactionsStore, type TransactionFilter, type ManualTransaction } from '~/stores/adminTransactions'
import { useAdminAccountsStore } from '~/stores/adminAccounts'
import { useAuthStore } from '~/stores/auth'
import { TRANSACTION_TYPES } from '~/infrastructure/constants'
import { transactionClassifications, accountRoles } from '~/infrastructure/dataLists'
import type { Transaction } from '~/stores/shared/transaction'
import ManualTransactionDialog from '~/components/admin/ManualTransactionDialog.vue'
import AidBasedTransactionDialog from '~/components/admin/AidBasedTransactionDialog.vue'
import TransactionsFilter from '~/components/admin/TransactionsFilter.vue'

const router = useRouter()
const toast = useToast()
const overlay = useOverlay()

const aidBasedModal = overlay.create(AidBasedTransactionDialog)
const manualModal = overlay.create(ManualTransactionDialog)
const filterSlideover = overlay.create(TransactionsFilter)

const adminTransactionsStore = useAdminTransactionsStore()
const adminAccountsStore = useAdminAccountsStore()
const authStore = useAuthStore()

const { currentTransactionsPage, aidBasedTransactions, manualTransactions } = storeToRefs(adminTransactionsStore)
const { accounts } = storeToRefs(adminAccountsStore)
const { loggedInUser } = storeToRefs(authStore)

const activeTab = ref<string>(TRANSACTION_TYPES.aidBased)
const currentPage = ref<number>(1)
const limit = ref<number>(25)
const tableIsLoading = ref<boolean>(false)
const aidBasedDialogIsVisible = ref<boolean>(false)
const manualDialogIsVisible = ref<boolean>(false)
const transactionBeingEdited = ref<Transaction | undefined>(undefined)
const transactionIdBeingDeleted = ref<number>(0)
const classifications = transactionClassifications

// Check if user has read-only access
const isReadOnly = computed(() => {
  return loggedInUser.value?.roles?.some((role: any) => role.name === "Admin (Read-only)") ?? false
})

// New filter structure
const { filters } = storeToRefs(adminTransactionsStore)

// Tab configuration for UTabs component
const tabs = [
  {
    value: TRANSACTION_TYPES.aidBased,
    label: 'Aid-Based',
    slot: 'aid-based'
  },
  {
    value: TRANSACTION_TYPES.manual,
    label: 'Manual',
    slot: 'manual'
  }
]

// Column definitions for aid-based transactions table
const aidBasedColumns: TableColumn<Transaction>[] = [
  {
    accessorKey: 'id',
    header: 'Actions',
    cell: ({ row }) => {
      const transaction = row.original
      return h('div', { class: 'flex gap-2' }, [
        h(resolveComponent('UButton'), {
          label: 'View',
          color: 'primary',
          size: 'xs',
          onClick: () => editTransaction(transaction, TRANSACTION_TYPES.aidBased)
        }),
        h(resolveComponent('UButton'), {
          label: 'Delete',
          color: 'error',
          size: 'xs',
          loading: transactionIdBeingDeleted.value === transaction.id,
          onClick: () => deleteTransactionById(transaction.id)
        })
      ])
    }
  },
  {
    accessorKey: 'sentBy',
    header: 'Sent By',
    cell: ({ row }) => {
      const transaction = row.original
      return h('span', { class: 'text-gray-900 dark:text-white' },
        `${transaction.sentBy?.rulerName || ''} of ${transaction.sentBy?.nationName || ''}`)
    }
  },
  {
    accessorKey: 'receivedBy',
    header: 'Received By',
    cell: ({ row }) => {
      const transaction = row.original
      return h('span', { class: 'text-gray-900 dark:text-white' },
        `${transaction.receivedBy?.rulerName || ''} of ${transaction.receivedBy?.nationName || ''}`)
    }
  },
  {
    accessorKey: 'code',
    header: 'Role Pairing',
    cell: ({ row }) => {
      const transaction = row.original
      return h('span', { class: 'text-gray-900 dark:text-white' },
        `${transaction.code?.sendingRole || ''}${transaction.code?.receivingRole || ''}`)
    }
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => {
      const transaction = row.original
      return h(resolveComponent('UBadge'), {
        color: getStatusColor(transaction.status),
        variant: 'subtle'
      }, () => toAidStatusDescription(transaction.status))
    }
  },
  {
    accessorKey: 'money',
    header: 'Money',
    cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.money)
  },
  {
    accessorKey: 'technology',
    header: 'Tech',
    cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.technology)
  },
  {
    accessorKey: 'reason',
    header: 'Reason',
    cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' }, row.original.reason || '')
  },
  {
    accessorKey: 'startsOn',
    header: 'Sent On',
    cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' },
      formatDate(row.original.startsOn || ''))
  }
]

// Column definitions for manual transactions table
const manualColumns: TableColumn<ManualTransaction>[] = [
  {
    accessorKey: 'id',
    header: 'Actions',
    cell: ({ row }) => {
      const transaction = row.original
      return h('div', { class: 'flex gap-2' }, [
        h(resolveComponent('UButton'), {
          label: 'Edit',
          color: 'primary',
          size: 'xs',
          onClick: () => editTransaction(transaction, TRANSACTION_TYPES.manual)
        }),
        h(resolveComponent('UButton'), {
          label: 'Delete',
          color: 'error',
          size: 'xs',
          loading: transactionIdBeingDeleted.value === transaction.id,
          onClick: () => deleteTransactionById(transaction.id)
        })
      ])
    }
  },
  {
    accessorKey: 'nation',
    header: 'Account',
    cell: ({ row }) => {
      const transaction = row.original
      return h('span', { class: 'text-gray-900 dark:text-white' },
        `${transaction.nation?.rulerName || ''} of ${transaction.nation?.nationName || ''}`)
    }
  },
  {
    accessorKey: 'adjustmentType',
    header: 'Adjustment Type',
    cell: ({ row }) => {
      const transaction = row.original
      return h(resolveComponent('UBadge'), {
        color: transaction.adjustmentType === 'Credit' ? 'success' : 'error',
        variant: 'subtle'
      }, () => transaction.adjustmentType)
    }
  },
  {
    accessorKey: 'reason',
    header: 'Reason',
    cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' }, row.original.reason || '')
  },
  {
    accessorKey: 'accountCode',
    header: 'Role',
    cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' }, row.original.accountCode || '')
  },
  {
    accessorKey: 'classification',
    header: 'Classification',
    cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' },
      getClassificationDescription(row.original.classification))
  },
  {
    accessorKey: 'rate',
    header: 'Rate',
    cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.rate)
  }
]

// Display columns based on read-only status
const displayAidBasedColumns = computed(() => {
  if (isReadOnly.value) {
    // Change "Actions" column to "View" only and filter out Delete button
    return aidBasedColumns.map(col => {
      if (col.header === 'Actions') {
        return {
          ...col,
          cell: ({ row }: any) => {
            const transaction = row.original
            return h(resolveComponent('UButton'), {
              label: 'View',
              color: 'primary',
              size: 'xs',
              onClick: () => editTransaction(transaction, TRANSACTION_TYPES.aidBased)
            })
          }
        }
      }
      return col
    })
  }
  return aidBasedColumns
})

const displayManualColumns = computed(() => {
  if (isReadOnly.value) {
    // Filter out Actions column entirely for manual transactions
    return manualColumns.filter(col => col.header !== 'Actions')
  }
  return manualColumns
})

// Helper function to get classification description
const getClassificationDescription = (classificationId: number | null): string => {
  if (classificationId === null) return 'N/A'
  const classification = classifications.find(c => c.id === classificationId)
  return classification ? classification.description : 'Unknown'
}

// Helper function to format date
const formatDate = (dateString: string): string => {
  return dateString.split('T')[0]
}

// Helper function to convert aid status ID to description
const toAidStatusDescription = (statusId: string | number): string => {
  const status = typeof statusId === 'string' ? parseInt(statusId) : statusId
  switch (status) {
    case 1: return 'Pending'
    case 2: return 'Approved'
    case 3: return 'Cancelled'
    case 4: return 'Expired'
    default: return 'Unknown'
  }
}

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

// Handle filter slideover functionality
const openFilterSlideover = async (): Promise<void> => {
  const instance = filterSlideover.open({ filters: filters.value })
  await instance.result
  await changePage(1)
}

// Change page handler
const changePage = async (newPageNumber: number): Promise<void> => {
  tableIsLoading.value = true
  currentPage.value = newPageNumber

  try {
    await adminTransactionsStore.reloadCurrentTransactionsPage({
      type: activeTab.value,
      filter: filters.value,
      limit: limit.value,
      offset: limit.value * (currentPage.value - 1)
    })
  } catch (error) {
    console.error('Failed to load transactions:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'The current page failed to load',
      duration: 5000
    })
  } finally {
    tableIsLoading.value = false
  }
}

// Watch for tab changes
watch(activeTab, async (_) => {
  await changePage(1)
})

watch(currentPage, async (newPage) => {
  await changePage(newPage)
})

// Create new transaction
const createTransaction = (): void => {
  manualModal.open({
    transaction: {} as ManualTransaction,
    accounts: accounts.value
  })
}

// Edit transaction
const editTransaction = (transaction: Transaction | ManualTransaction, transactionType: string): void => {
  if (transactionType === TRANSACTION_TYPES.aidBased) {
    aidBasedModal.open({ transaction: transaction as Transaction })
  } else if (transactionType === TRANSACTION_TYPES.manual) {
    manualModal.open({ transaction: transaction as ManualTransaction, accounts: accounts.value })
  }
}

// Delete transaction by ID
const deleteTransactionById = async (id: number): Promise<void> => {
  transactionIdBeingDeleted.value = id

  try {
    await adminTransactionsStore.deleteTransaction(id)
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Transaction deleted successfully.',
      duration: 3000
    })
    await changePage(currentPage.value)
  } catch (error) {
    console.error('Failed to delete transaction:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'VEP encountered an error while deleting the transaction. Please try again, or contact @lilweirdward if the error persists.',
      duration: 5000
    })
  } finally {
    transactionIdBeingDeleted.value = 0
  }
}

// Check admin access and load data on mount
onMounted(async () => {
  try {
    await changePage(1)
    console.log('Transactions loaded:', currentTransactionsPage.value)

    await adminAccountsStore.loadAllAccounts()
    console.log('Accounts loaded:', accounts.value)
  } catch (error) {
    console.error('Failed to load page data:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load page data. Please try again.',
      duration: 5000
    })
  }
})
</script>
