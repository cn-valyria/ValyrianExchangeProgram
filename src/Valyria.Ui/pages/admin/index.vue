<template>
  <UContainer>
    <!-- Loading Overlay -->
    <div v-if="loading" class="fixed inset-0 bg-white/80 dark:bg-gray-900/80 backdrop-blur-sm z-50 flex items-center justify-center">
      <UIcon name="i-heroicons-arrow-path" class="w-12 h-12 text-primary-500 animate-spin" />
    </div>

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
            <p class="text-sm text-gray-500 dark:text-gray-400">Incoming/Outgoing Cash Ratio</p>
            <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ incomingOutgoingCashRatio }}</p>
          </div>
        </UCard>
        <UCard>
          <div class="text-center space-y-2">
            <p class="text-sm text-gray-500 dark:text-gray-400">Incoming/Outgoing Tech Ratio</p>
            <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ incomingOutgoingTechRatio }}</p>
          </div>
        </UCard>
        <UCard>
          <div class="text-center space-y-2">
            <p class="text-sm text-gray-500 dark:text-gray-400">Total Aid Transactions</p>
            <p class="text-3xl font-bold text-gray-900 dark:text-white">{{ dashboard.totalTransactions }}</p>
          </div>
        </UCard>
      </div>

      <!-- Dashboard Tiles - Row 1 -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <UCard>
          <AdminDashboardAccountsByRole :data="dashboard.accountsPerRole" />
        </UCard>
        <UCard>
          <AdminDashboardTotalSlotsByAlliance :data="dashboard.totalSlotsPerAlliance" :alliances="alliances" />
        </UCard>
        <UCard>
          <AdminDashboardTotalTransactionsTile :data="dashboard.transactionsPerDay" />
        </UCard>
      </div>

      <!-- Dashboard Tiles - Row 2 -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <UCard>
          <AdminDashboardAccountsByAlliance :data="dashboard.accountsPerAlliance" />
        </UCard>
        <UCard>
          <AdminDashboardFreeSlotsByList :data="dashboard.freeSlotsPerList" />
        </UCard>
        <UCard>
          <AdminDashboardPendingCreditTile :data="dashboard.pendingCreditByAlliance" />
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

const loading = ref(true)

// Computed property for slot ratios
const incomingOutgoingCashRatio = computed<string>(() => {
  if (!dashboard.value.totalIncomingCashSlots || !dashboard.value.totalOutgoingCashSlots) {
    return '0.00'
  }
  return (dashboard.value.totalIncomingCashSlots / dashboard.value.totalOutgoingCashSlots).toFixed(2)
})
const incomingOutgoingTechRatio = computed<string>(() => {
  if (!dashboard.value.totalIncomingTechSlots || !dashboard.value.totalOutgoingTechSlots) {
    return '0.00'
  }
  return (dashboard.value.totalIncomingTechSlots / dashboard.value.totalOutgoingTechSlots).toFixed(2)
})

const alliances = computed<string[]>(() => {
  if (!dashboard.value.accountsPerAlliance) {
    return []
  }
  return dashboard.value.accountsPerAlliance.map(row => row.allianceName)
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
  } finally {
    loading.value = false
  }
})
</script>
