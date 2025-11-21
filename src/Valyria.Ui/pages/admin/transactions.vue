<template>
  <UContainer>
    <div class="py-8 space-y-8">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">VEP Transactions</h1>
        <p class="text-gray-600 dark:text-gray-400 mt-2">Manage every transaction that VEP tracks, as well as manual balance adjustments</p>
      </div>

      <!-- Filters Card -->
      <UCard>
        <div class="space-y-4">
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Filters</h2>
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <UFormField label="Sent By">
              <UInput v-model="filters.sentBy" placeholder="Nation ID" />
            </UFormField>
            <UFormField label="Received By">
              <UInput v-model="filters.receivedBy" placeholder="Nation ID" />
            </UFormField>
            <UFormField label="Since">
              <UInput v-model="dateFilters.sentSince" type="date" />
            </UFormField>
            <UFormField label="Until">
              <UInput v-model="dateFilters.sentUntil" type="date" />
            </UFormField>
          </div>
          <div class="flex justify-end">
            <UButton label="Apply Filters" color="primary" @click="changePage(1)" />
          </div>
        </div>
      </UCard>

      <!-- Tabs and Tables -->
      <UTabs v-model="activeTabIndex" :items="tabs" class="w-full">
        <!-- Aid-Based Tab -->
        <template #aid-based>
          <UCard>
            <div class="space-y-4">
              <UTable
                :data="aidBasedTransactions"
                :columns="aidBasedColumns"
                :loading="tableIsLoading"
              />

              <!-- Pagination -->
              <div class="flex justify-between items-center">
                <div class="text-sm text-gray-600 dark:text-gray-400">
                  Showing {{ Math.min((currentPage - 1) * limit + 1, currentTransactionsPage.totalCount) }} to {{ Math.min(currentPage * limit, currentTransactionsPage.totalCount) }} of {{ currentTransactionsPage.totalCount }} results
                </div>
                <UPagination
                  v-model="currentPage"
                  :total="currentTransactionsPage.totalCount"
                  :page-count="limit"
                  @update:model-value="changePage"
                />
              </div>
            </div>
          </UCard>
        </template>

        <!-- Manual Tab -->
        <template #manual>
          <UCard>
            <div class="space-y-4">
              <div class="flex justify-end">
                <UButton label="Create Transaction" color="success" @click="createTransaction()" />
              </div>

              <UTable
                :data="manualTransactions"
                :columns="manualColumns"
                :loading="tableIsLoading"
              />

              <!-- Pagination -->
              <div class="flex justify-between items-center">
                <div class="text-sm text-gray-600 dark:text-gray-400">
                  Showing {{ Math.min((currentPage - 1) * limit + 1, currentTransactionsPage.totalCount) }} to {{ Math.min(currentPage * limit, currentTransactionsPage.totalCount) }} of {{ currentTransactionsPage.totalCount }} results
                </div>
                <UPagination
                  v-model="currentPage"
                  :total="currentTransactionsPage.totalCount"
                  :page-count="limit"
                  @update:model-value="changePage"
                />
              </div>
            </div>
          </UCard>
        </template>
      </UTabs>
    </div>

    <AdminAidBasedTransactionDialog
      :show="aidBasedDialogIsVisible"
      :transaction="transactionBeingEdited"
      @close="closeDialog(TRANSACTION_TYPES.aidBased)"
    />
    <AdminManualTransactionDialog
      :show="manualDialogIsVisible"
      :transaction="transactionBeingEdited"
      :accounts="accounts"
      @close="closeDialog(TRANSACTION_TYPES.manual)"
    />
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { storeToRefs } from 'pinia'
import { useAdminTransactionsStore, type TransactionFilter, type ManualTransaction } from '~/stores/adminTransactions'
import { useAdminAccountsStore } from '~/stores/adminAccounts'
import { useAuthStore } from '~/stores/auth'
import { TRANSACTION_TYPES } from '~/infrastructure/constants'
import { transactionClassifications } from '~/infrastructure/dataLists'
import type { Transaction } from '~/stores/shared/transaction'

const router = useRouter()
const toast = useToast()

const adminTransactionsStore = useAdminTransactionsStore()
const adminAccountsStore = useAdminAccountsStore()
const authStore = useAuthStore()

const { currentTransactionsPage, aidBasedTransactions, manualTransactions } = storeToRefs(adminTransactionsStore)
const { accounts } = storeToRefs(adminAccountsStore)
const { loggedInUser } = storeToRefs(authStore)

const activeTabIndex = ref<number>(0)
const currentPage = ref<number>(1)
const limit = ref<number>(25)
const tableIsLoading = ref<boolean>(false)
const aidBasedDialogIsVisible = ref<boolean>(false)
const manualDialogIsVisible = ref<boolean>(false)
const transactionBeingEdited = ref<Transaction | undefined>(undefined)
const transactionIdBeingDeleted = ref<number>(0)
const classifications = transactionClassifications

// Date filter strings (for UInput compatibility)
const dateFilters = ref({
  sentSince: '',
  sentUntil: ''
})

const filters = ref<TransactionFilter>({
  sentBy: null,
  receivedBy: null,
  sentSince: null,
  sentUntil: null
})

// Tab configuration for UTabs component
const tabs = [
  {
    key: 'aid-based',
    label: 'Aid-Based',
    slot: 'aid-based'
  },
  {
    key: 'manual',
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
          onClick: () => editTransaction(transaction as any, TRANSACTION_TYPES.manual)
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

// Computed property for current transaction type based on active tab
const currentTab = computed<string>(() => {
  return activeTabIndex.value === 0 ? TRANSACTION_TYPES.aidBased : TRANSACTION_TYPES.manual
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

// Change page handler
const changePage = async (newPageNumber: number): Promise<void> => {
  tableIsLoading.value = true
  currentPage.value = newPageNumber

  try {
    await adminTransactionsStore.reloadCurrentTransactionsPage({
      type: currentTab.value,
      filter: {
        sentBy: filters.value.sentBy ? Number(filters.value.sentBy) : undefined,
        receivedBy: filters.value.receivedBy ? Number(filters.value.receivedBy) : undefined,
        sentSince: filters.value.sentSince || undefined,
        sentUntil: filters.value.sentUntil || undefined
      },
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

// Watch for date filter changes and convert to Date objects
watch(() => dateFilters.value.sentSince, (newValue) => {
  filters.value.sentSince = newValue ? new Date(newValue) : null
})

watch(() => dateFilters.value.sentUntil, (newValue) => {
  filters.value.sentUntil = newValue ? new Date(newValue) : null
})

// Watch for tab changes
watch(activeTabIndex, async (newIndex) => {
  await changePage(1)
})

// Create new transaction
const createTransaction = (): void => {
  transactionBeingEdited.value = {} as Transaction
  manualDialogIsVisible.value = true
}

// Edit transaction
const editTransaction = (transaction: Transaction, transactionType: string): void => {
  transactionBeingEdited.value = transaction

  if (transactionType === TRANSACTION_TYPES.aidBased) {
    aidBasedDialogIsVisible.value = true
  } else if (transactionType === TRANSACTION_TYPES.manual) {
    manualDialogIsVisible.value = true
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

// Close dialog
const closeDialog = async (transactionType: string): Promise<void> => {
  if (transactionType === TRANSACTION_TYPES.aidBased) {
    aidBasedDialogIsVisible.value = false
  } else if (transactionType === TRANSACTION_TYPES.manual) {
    manualDialogIsVisible.value = false
  }

  transactionBeingEdited.value = undefined
  await changePage(currentPage.value)
}

// Check admin access and load data on mount
onMounted(async () => {
  // Check if user has admin role
  if (!loggedInUser.value?.roles?.some((role: any) => role.name === 'Admin')) {
    console.warn('Tried to access an admin page without the admin role.')
    toast.add({
      color: 'error',
      title: 'Access Denied',
      description: 'You do not have permission to access this page.',
      duration: 5000
    })
    await router.push('/')
    return
  }

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
