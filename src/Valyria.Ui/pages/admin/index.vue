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
          <AdminDashboardGeneralProgramStatsCard :data="dashboard.generalProgramSummary" />
        </UCard>
        <UCard>
          <AdminDashboardProgramSlotUsageStatCard :data="dashboard.slotUsageSummary" />
        </UCard>
        <UCard>
          <AdminDashboardDealHealthStatCard :data="dashboard.dealHealthSummary" />
        </UCard>
        <UCard>
          <AdminDashboardSlotRatiosStatCard :data="dashboard.aidRatioSummary" />
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
          <AdminDashboardDealAidPerDayTile :data="dashboard.dealAidPerDay" />
        </UCard>
      </div>

      <!-- Dashboard Tiles - Row 2 -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <UCard>
          <AdminDashboardAccountsByAlliance :data="dashboard.accountsPerAlliance" />
        </UCard>
        <UCard>
          <AdminDashboardOpenSlotsByRole :data="dashboard.openSlotsPerRole" />
        </UCard>
      </div>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAdminStore } from '~/stores/admin'

const toast = useToast()

const adminStore = useAdminStore()

const { dashboard } = storeToRefs(adminStore)

const loading = ref(true)

const alliances = computed<string[]>(() => {
  if (!dashboard.value.accountsPerAlliance) {
    return []
  }
  return dashboard.value.accountsPerAlliance.map(row => row.allianceName)
})

// Check admin access and load dashboard on mount
onMounted(async () => {
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
