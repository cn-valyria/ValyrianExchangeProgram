<template>
  <div>
    <div class="container">
      <div class="section">
        <h1 class="title">VEP Transactions</h1>
        <h2 class="subtitle">Manage every transaction that VEP tracks, as well as manual balance adjustments</h2>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <div class="p-fluid grid">
          <div class="col-12 md:col-3">
            <label for="sentBy" class="block mb-2">Sent By</label>
            <InputText id="sentBy" v-model="filters.sentBy" />
          </div>
          <div class="col-12 md:col-3">
            <label for="receivedBy" class="block mb-2">Received By</label>
            <InputText id="receivedBy" v-model="filters.receivedBy" />
          </div>
          <div class="col-12 md:col-3">
            <label for="sentSince" class="block mb-2">Since</label>
            <Calendar id="sentSince" v-model="filters.sentSince" showIcon />
          </div>
          <div class="col-12 md:col-3">
            <label for="sentUntil" class="block mb-2">Until</label>
            <Calendar id="sentUntil" v-model="filters.sentUntil" showIcon />
          </div>
        </div>
        <div class="mt-3">
          <Button label="Filter" severity="primary" @click="changePage(1)" />
        </div>

        <TabView v-model:activeIndex="activeTabIndex" class="mt-4">
          <TabPanel header="Aid-Based">
            <DataTable
              :value="aidBasedTransactions"
              :loading="tableIsLoading"
              :totalRecords="currentTransactionsPage.totalCount"
              lazy
              paginator
              :rows="limit"
              @page="onPageChange"
            >
              <Column field="id" header="Actions" style="width: 140px">
                <template #body="slotProps">
                  <div class="flex gap-2">
                    <Button
                      label="View"
                      severity="info"
                      size="small"
                      @click="editTransaction(slotProps.data, TRANSACTION_TYPES.aidBased)"
                    />
                    <Button
                      label="Delete"
                      severity="danger"
                      size="small"
                      :loading="transactionIdBeingDeleted === slotProps.data.id"
                      @click="deleteTransactionById(slotProps.data.id)"
                    />
                  </div>
                </template>
              </Column>
              <Column field="sentBy" header="Sent By">
                <template #body="slotProps">
                  {{ slotProps.data.sentBy.rulerName }} of {{ slotProps.data.sentBy.nationName }}
                </template>
              </Column>
              <Column field="receivedBy" header="Received By">
                <template #body="slotProps">
                  {{ slotProps.data.receivedBy.rulerName }} of {{ slotProps.data.receivedBy.nationName }}
                </template>
              </Column>
              <Column field="code" header="Role Pairing">
                <template #body="slotProps">
                  {{ slotProps.data.code.sendingRole }}{{ slotProps.data.code.receivingRole }}
                </template>
              </Column>
              <Column field="status" header="Status">
                <template #body="slotProps">
                  {{ toAidStatusDescription(slotProps.data.status) }}
                </template>
              </Column>
              <Column field="money" header="Money">
                <template #body="slotProps">
                  {{ slotProps.data.money }}
                </template>
              </Column>
              <Column field="technology" header="Tech">
                <template #body="slotProps">
                  {{ slotProps.data.technology }}
                </template>
              </Column>
              <Column field="reason" header="Reason">
                <template #body="slotProps">
                  {{ slotProps.data.reason }}
                </template>
              </Column>
              <Column field="startsOn" header="Sent On">
                <template #body="slotProps">
                  {{ formatDate(slotProps.data.startsOn) }}
                </template>
              </Column>
              <template #empty>
                <div class="text-center">No transactions currently loaded</div>
              </template>
            </DataTable>
          </TabPanel>

          <TabPanel header="Manual">
            <Button label="Create" severity="success" @click="createTransaction()" class="mb-3" />
            <DataTable
              :value="manualTransactions"
              :loading="tableIsLoading"
              :totalRecords="currentTransactionsPage.totalCount"
              lazy
              paginator
              :rows="limit"
              @page="onPageChange"
            >
              <Column field="id" header="Actions" style="width: 140px">
                <template #body="slotProps">
                  <div class="flex gap-2">
                    <Button
                      label="Edit"
                      severity="info"
                      size="small"
                      @click="editTransaction(slotProps.data, TRANSACTION_TYPES.manual)"
                    />
                    <Button
                      label="Delete"
                      severity="danger"
                      size="small"
                      :loading="transactionIdBeingDeleted === slotProps.data.id"
                      @click="deleteTransactionById(slotProps.data.id)"
                    />
                  </div>
                </template>
              </Column>
              <Column field="nation" header="Account">
                <template #body="slotProps">
                  {{ slotProps.data.nation.rulerName }} of {{ slotProps.data.nation.nationName }}
                </template>
              </Column>
              <Column field="adjustmentType" header="Adjustment Type">
                <template #body="slotProps">
                  {{ slotProps.data.adjustmentType }}
                </template>
              </Column>
              <Column field="reason" header="Reason">
                <template #body="slotProps">
                  {{ slotProps.data.reason }}
                </template>
              </Column>
              <Column field="accountCode" header="Role">
                <template #body="slotProps">
                  {{ slotProps.data.accountCode }}
                </template>
              </Column>
              <Column field="classification" header="Classification">
                <template #body="slotProps">
                  {{ getClassificationDescription(slotProps.data.classification) }}
                </template>
              </Column>
              <Column field="rate" header="Rate">
                <template #body="slotProps">
                  {{ slotProps.data.rate }}
                </template>
              </Column>
              <template #empty>
                <div class="text-center">No transactions currently loaded</div>
              </template>
            </DataTable>
          </TabPanel>
        </TabView>
      </div>
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
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAdminTransactionsStore, type TransactionFilter } from '~/stores/adminTransactions'
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

const filters = ref<TransactionFilter>({
  sentBy: null,
  receivedBy: null,
  sentSince: null,
  sentUntil: null
})

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

// Page change event handler for DataTable
const onPageChange = (event: any): void => {
  const newPage = event.page + 1 // PrimeVue pages are 0-indexed
  changePage(newPage)
}

// Change tab handler
const changeTab = async (tabIndex: number): Promise<void> => {
  if (activeTabIndex.value === tabIndex) {
    console.warn('The same tab that is already active was clicked, so nothing will happen.')
    return
  }

  activeTabIndex.value = tabIndex
  await changePage(1)
}

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
