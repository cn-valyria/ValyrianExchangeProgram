<template>
  <UContainer>
    <!-- Loading Overlay -->
    <div
      v-if="loading"
      class="fixed inset-0 bg-white/80 dark:bg-gray-900/80 backdrop-blur-sm z-50 flex items-center justify-center"
    >
      <UIcon
        name="i-heroicons-arrow-path"
        class="w-12 h-12 text-primary-500 animate-spin"
      />
    </div>

    <div class="py-8 space-y-6">
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">
          VEP Dashboard
        </h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          As of the last data load. On Hold accounts are left out.
        </p>
      </div>

      <AdminDashboardSummaryCards
        :summary="dashboard.summary"
        :markets="markets.markets"
        :other-deals="markets.otherDeals"
        :breakdown="breakdown"
      />

      <UCard>
        <AdminDashboardSlotMarkets :markets="markets.markets" />
      </UCard>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <UCard>
          <AdminDashboardOpenSlotBreakdown :breakdown="breakdown" />
        </UCard>
        <UCard>
          <AdminDashboardDealHealthByPairing
            :markets="markets.markets"
            :other-deals="markets.otherDeals"
          />
        </UCard>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <UCard>
          <AdminDashboardDealActivity
            :data="dashboard.activity"
            :days="ACTIVITY_DAYS"
          />
        </UCard>
        <UCard>
          <AdminDashboardCancellationReasons
            :data="dashboard.cancellationReasons"
            :days="CANCELLATION_DAYS"
          />
        </UCard>
      </div>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { ACTIVITY_DAYS, CANCELLATION_DAYS, useAdminStore } from '~/stores/admin'
import {
  buildSlotMarkets,
  getOpenSlotBreakdown,
} from '~/infrastructure/dashboard'

const toast = useToast()

const adminStore = useAdminStore()

const { dashboard } = storeToRefs(adminStore)

const loading = ref(true)

const markets = computed(() =>
  buildSlotMarkets(dashboard.value.slotConfigs, dashboard.value.dealHealth),
)

const breakdown = computed(() =>
  getOpenSlotBreakdown(dashboard.value.slotConfigs, markets.value.markets),
)

onMounted(async () => {
  try {
    await adminStore.loadDashboard()
  } catch (error) {
    console.error('Failed to load dashboard:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load dashboard data. Please try again.',
      duration: 5000,
    })
  } finally {
    loading.value = false
  }
})
</script>
