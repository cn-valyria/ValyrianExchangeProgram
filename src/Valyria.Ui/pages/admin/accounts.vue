<template>
  <UContainer>
    <div class="py-8 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">VEP Accounts</h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">Manage accounts enrolled in VEP, or enroll new accounts</p>
      </div>

      <!-- Actions and Table Card -->
      <UCard>
        <div class="space-y-4">
          <!-- Search and Actions -->
          <div class="flex justify-between items-center gap-4">
            <UInput
              v-model="searchQuery"
              placeholder="Search by nation, ruler, alliance, or role..."
              icon="i-heroicons-magnifying-glass"
              class="w-full max-w-md"
            />
            <div class="flex items-center gap-4">
              <USwitch
                v-model="showOnHold"
                @update:model-value="handleToggleChange"
              >
                <template #label>
                  <span class="text-sm text-gray-600 dark:text-gray-400">Show On Hold</span>
                </template>
              </USwitch>
              <UButton
                color="success"
                icon="i-heroicons-plus"
                @click="createAccount()"
              >
                Create Account
              </UButton>
            </div>
          </div>

          <!-- Table -->
          <UTable
            :data="filteredAccounts"
            :columns="columns"
            :loading="loading"
            :sort="{ column: 'nationId', direction: 'asc' }"
          />
        </div>
      </UCard>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { storeToRefs } from 'pinia'
import { useAdminAccountsStore, type AdminAccount } from '~/stores/adminAccounts'
import { useAuthStore } from '~/stores/auth'
import { accountRoles } from '~/infrastructure/dataLists'
import AccountEditDialog from '~/components/admin/AccountEditDialog.vue'

const router = useRouter()
const toast = useToast()
const overlay = useOverlay()

const modal = overlay.create(AccountEditDialog)

const adminAccountsStore = useAdminAccountsStore()
const authStore = useAuthStore()

const { accounts } = storeToRefs(adminAccountsStore)
const { loggedInUser } = storeToRefs(authStore)

const editDialogIsVisible = ref<boolean>(false)
const accountIdBeingDeleted = ref<number>(0)
const loading = ref<boolean>(false)
const searchQuery = ref<string>('')
const showOnHold = ref<boolean>(false)
const roles = accountRoles

// Table columns definition
const columns: TableColumn<AdminAccount>[] = [
  {
    accessorKey: 'nationName',
    header: 'Nation',
    cell: ({ row }) => {
      const account = row.original
      return h('div', [
        h('div', { class: 'font-medium text-gray-900 dark:text-white' }, account.rulerName),
        h('div', { class: 'text-sm text-gray-500 dark:text-gray-400' }, account.nationName)
      ])
    }
  },
  {
    accessorKey: 'allianceName',
    header: 'Alliance',
    cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' }, row.original.allianceName)
  },
  {
    accessorKey: 'role',
    header: 'Role',
    cell: ({ row }) => {
      const roleCode = row.original.role
      return h(resolveComponent('UBadge'), {
        color: getRoleBadgeColor(roleCode),
        variant: 'subtle'
      }, () => getRoleDescription(roleCode))
    }
  },
  {
    accessorKey: 'credit',
    header: 'Credit',
    cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.credit)
  },
  {
    accessorKey: 'debt',
    header: 'Debt',
    cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.debt)
  },
  {
    accessorKey: 'avgDaysToClearDebt',
    header: 'Debt Activity',
    cell: ({ row }) => {
      const account = row.original
      if (account.totalTimesInDebt === 0) {
        return h('span', { class: 'text-gray-400 dark:text-gray-500 italic' }, 'N/A')
      }
      const avgDays = account.avgDaysToClearDebt.toFixed(2)
      const times = account.totalTimesInDebt
      return h('div', { class: 'text-gray-900 dark:text-white' }, [
        h('span', { class: 'font-mono' }, avgDays),
        h('span', { class: 'text-xs text-gray-500 dark:text-gray-400 ml-1' }, `(${times})`)
      ])
    }
  },
  {
    accessorKey: 'avgDaysToFillCredit',
    header: 'Credit Activity',
    cell: ({ row }) => {
      const account = row.original
      if (account.totalTimesWithCredit === 0) {
        return h('span', { class: 'text-gray-400 dark:text-gray-500 italic' }, 'N/A')
      }
      const avgDays = account.avgDaysToFillCredit.toFixed(2)
      const times = account.totalTimesWithCredit
      return h('div', { class: 'text-gray-900 dark:text-white' }, [
        h('span', { class: 'font-mono' }, avgDays),
        h('span', { class: 'text-xs text-gray-500 dark:text-gray-400 ml-1' }, `(${times})`)
      ])
    }
  },
  {
    accessorKey: 'slotsUsed',
    header: 'Slots Free',
    cell: ({ row }) => {
      const slotsFree = row.original.slotsFull - row.original.slotsUsed
      return h('span', { class: 'font-mono text-gray-900 dark:text-white' }, slotsFree)
    }
  },
  {
    accessorKey: 'slotsFull',
    header: 'Slots Full',
    cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.slotsFull)
  },
  {
    accessorKey: 'id',
    header: 'Actions',
    cell: ({ row }) => {
      const account = row.original
      return h('div', { class: 'flex gap-2' }, [
        h(resolveComponent('UButton'), {
          label: 'Edit',
          color: 'primary',
          variant: 'soft',
          size: 'xs',
          onClick: () => editAccount(account)
        }),
        h(resolveComponent('UButton'), {
          label: 'Delete',
          color: 'error',
          variant: 'soft',
          size: 'xs',
          loading: accountIdBeingDeleted.value === account.id,
          onClick: () => deleteAccountById(account.id)
        })
      ])
    }
  }
]

// Helper function to get role description
const getRoleDescription = (roleCode: string): string => {
  const role = roles.find(r => r.code === roleCode)
  return role ? role.description : roleCode
}

// Helper function to get role badge color
const getRoleBadgeColor = (roleCode: string): 'primary' | 'secondary' | 'success' | 'info' | 'warning' | 'error' | 'neutral' => {
  switch (roleCode) {
    case 'BUYER':
      return 'primary'
    case 'SELLER':
      return 'success'
    case 'BOTH':
      return 'info'
    default:
      return 'neutral'
  }
}

// Filtered accounts based on search query
const filteredAccounts = computed(() => {
  if (!searchQuery.value) {
    return accounts.value
  }

  const query = searchQuery.value.toLowerCase()
  return accounts.value.filter((account: AdminAccount) => {
    return (
      account.rulerName?.toLowerCase().includes(query) ||
      account.nationName?.toLowerCase().includes(query) ||
      account.allianceName?.toLowerCase().includes(query) ||
      getRoleDescription(account.role)?.toLowerCase().includes(query)
    )
  })
})

// Create new account
const createAccount = (): void => {
  modal.open({
    account: {} as AdminAccount
  })
}

// Edit existing account
const editAccount = (account: AdminAccount): void => {
  modal.open({
    account: account
  })
}

// Delete account by ID
const deleteAccountById = async (accountId: number): Promise<void> => {
  accountIdBeingDeleted.value = accountId

  try {
    await adminAccountsStore.deleteAccount(accountId)
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Account deleted successfully.',
      duration: 3000
    })
  } catch (error) {
    console.error('Failed to delete account:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'VEP encountered an error while deleting the account. Please try again, or contact @lilweirdward if the error persists.',
      duration: 5000
    })
  } finally {
    accountIdBeingDeleted.value = 0
  }
}

// Handle toggle change to reload accounts with/without On Hold
const handleToggleChange = async (): Promise<void> => {
  loading.value = true
  try {
    await adminAccountsStore.loadAllAccounts(showOnHold.value)
  } catch (error) {
    console.error('Failed to reload accounts:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to reload accounts. Please try again.',
      duration: 5000
    })
  } finally {
    loading.value = false
  }
}

// Check admin access and load accounts on mount
onMounted(async () => {
  loading.value = true
  try {
    await adminAccountsStore.loadAllAccounts(showOnHold.value)
    console.log('Accounts loaded:', accounts.value)
  } catch (error) {
    console.error('Failed to load accounts:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load accounts. Please try again.',
      duration: 5000
    })
  } finally {
    loading.value = false
  }
})
</script>
