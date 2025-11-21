<template>
  <UContainer>
    <div class="py-8 space-y-8">
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">VEP Dashboard</h1>
      </div>

      <!-- Stats Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <UCard>
          <div class="text-center space-y-2">
            <p class="text-sm text-gray-500 dark:text-gray-400">Total Accounts</p>
            <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ dashboard.totalAccounts }}</p>
          </div>
        </UCard>
        <UCard>
          <div class="text-center space-y-2">
            <p class="text-sm text-gray-500 dark:text-gray-400">Buyer to Seller Ratio</p>
            <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ buyerSellerRatio }}</p>
          </div>
        </UCard>
        <UCard>
          <div class="text-center space-y-2">
            <p class="text-sm text-gray-500 dark:text-gray-400">Total Aid Transactions</p>
            <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ dashboard.totalTransactions }}</p>
          </div>
        </UCard>
        <UCard>
          <div class="text-center space-y-2">
            <p class="text-sm text-gray-500 dark:text-gray-400">Total Unused Credit</p>
            <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ dashboard.unusedCredit }}</p>
          </div>
        </UCard>
      </div>

      <!-- Dashboard Tiles - Row 1 -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <UCard>
          <AdminDashboardTotalTransactionsTile :data="dashboard.transactionsPerDay" />
        </UCard>
        <UCard>
          <AdminDashboardPendingCreditTile :data="dashboard.pendingCreditByAlliance" />
        </UCard>
        <UCard>
          <AdminDashboardAccountsByRole :data="dashboard.accountsPerRole" />
        </UCard>
      </div>

      <!-- Dashboard Tiles - Row 2 -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <UCard>
          <AdminDashboardTotalSlotsByRole :data="dashboard.totalSlotsPerRole" />
        </UCard>
        <UCard>
          <AdminDashboardFreeSlotsByList :data="dashboard.freeSlotsPerList" />
        </UCard>
        <UCard>
          <AdminDashboardAccountsByAlliance :data="dashboard.accountsPerAlliance" />
        </UCard>
      </div>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAdminStore } from '~/stores/admin'
import { useAuthStore } from '~/stores/auth'

const router = useRouter()
const toast = useToast()

const adminStore = useAdminStore()
const authStore = useAuthStore()

const { dashboard } = storeToRefs(adminStore)
const { loggedInUser } = storeToRefs(authStore)

// Computed property for buyer to seller ratio
const buyerSellerRatio = computed<string>(() => {
  if (!dashboard.value.totalBuyers || !dashboard.value.totalSellers) {
    return '0.00'
  }
  return (dashboard.value.totalBuyers / dashboard.value.totalSellers).toFixed(2)
})

// Check admin access and load dashboard on mount
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
    await adminStore.loadDashboard()
  } catch (error) {
    console.error('Failed to load dashboard:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load dashboard data. Please try again.',
      duration: 5000
    })
  }
})
</script>
