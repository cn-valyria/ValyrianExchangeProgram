<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900">
    <!-- Loading Overlay -->
    <div v-if="loading" class="fixed inset-0 bg-white/80 dark:bg-gray-900/80 backdrop-blur-sm z-50 flex items-center justify-center">
      <UIcon name="i-heroicons-arrow-path" class="w-12 h-12 text-primary-500 animate-spin" />
    </div>

    <!-- Header Section -->
    <div class="bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
      <UContainer class="py-6">
        <div class="flex flex-col gap-6">
          <!-- Top Section: Account Info (Centered) -->
          <div class="flex flex-col items-center text-center space-y-3">
            <h1 class="text-3xl font-bold text-gray-900 dark:text-white">
              <span class="font-extrabold">{{ account.rulerName }}</span> of {{ account.nationName }}
            </h1>
            <div class="flex flex-wrap gap-2 justify-center">
              <ChipAccountRole :code="account.role" />
              <ChipAllianceAcronym :allianceName="account.allianceName" />
            </div>
          </div>

          <!-- Bottom Section: Slot Usage (Left) and Deal Health (Right) -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Left: Slot Usage -->
            <div class="flex justify-center lg:justify-end lg:pr-4">
              <AccountSlotUsage
                :slotsUsed="dealsSummary.slotsUsed"
                :slotsTotal="dealsSummary.slotsTotal"
                :role="account.role"
              />
            </div>

            <!-- Right: Deal Health -->
            <div class="flex justify-center lg:justify-start lg:pl-4 lg:border-l border-gray-200 dark:border-gray-700">
              <AccountDealHealth :deals="dealsSummary.activeDeals" :notifications="dealsSummary.notifications" />
            </div>
          </div>
        </div>
      </UContainer>
    </div>

    <!-- Main Content -->
    <UContainer class="py-8">
      <div class="space-y-6">
        <!-- Action Needed Card: only shows when a deal needs something from this account today -->
        <AccountActionNeeded :deals="dealsSummary.activeDeals" :myNationId="account.nationId" />

        <!-- Active Deals Card -->
        <AccountActiveDeals
          :account="account"
          :deals="dealsSummary.activeDeals"
          :notifications="dealsSummary.notifications"
        />

        <!-- Deal History Card -->
        <AccountDealHistory :accountId="accountId" />
      </div>
    </UContainer>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { useAuthStore } from '~/stores/auth'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const accountStore = useAccountStore()
const authStore = useAuthStore()

const { account, dealsSummary } = storeToRefs(accountStore)
const { loggedInUser } = storeToRefs(authStore)

const loading = ref<boolean>(true)

// Parse account ID from route params
const accountId = computed<number>(() => {
  const parsedId = parseInt(route.params.id as string)
  return isNaN(parsedId) ? 0 : parsedId
})

// Load account data on mount
onMounted(async () => {
  loading.value = true

  // Check if user is accessing their own account
  if (loggedInUser.value && loggedInUser.value.accountId !== accountId.value) {
    toast.add({
      color: 'error',
      title: 'Access Denied',
      description: "You're not logged into the account that you tried to access.",
      duration: 5000
    })
    await router.push('/')
    return
  }

  try {
    await Promise.all([
      accountStore.loadAccount(accountId.value),
      accountStore.loadDealsSummary(accountId.value),
      accountStore.loadDealHistory(accountId.value)
    ])
  } catch (error) {
    console.error('Failed to load account data:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load account data. Please try again.',
      duration: 5000
    })
  } finally {
    loading.value = false
  }
})
</script>
