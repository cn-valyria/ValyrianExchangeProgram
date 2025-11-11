<template>
  <div>
    <div class="container">
      <div class="section">
        <h1 class="title">VEP Dashboard</h1>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <nav class="level">
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Total Accounts</p>
              <p class="title">{{ dashboard.totalAccounts }}</p>
            </div>
          </div>
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Buyer to Seller Ratio</p>
              <p class="title">{{ buyerSellerRatio }}</p>
            </div>
          </div>
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Total Aid Transactions</p>
              <p class="title">{{ dashboard.totalTransactions }}</p>
            </div>
          </div>
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Total Unused Credit</p>
              <p class="title">{{ dashboard.unusedCredit }}</p>
            </div>
          </div>
        </nav>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <div class="tile is-ancestor">
          <div class="tile is-parent">
            <article class="tile is-child box">
              <AdminDashboardTotalTransactionsTile :data="dashboard.transactionsPerDay" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <AdminDashboardPendingCreditTile :data="dashboard.pendingCreditByAlliance" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <AdminDashboardAccountsByRole :data="dashboard.accountsPerRole" />
            </article>
          </div>
        </div>
        <div class="tile is-ancestor">
          <div class="tile is-parent">
            <article class="tile is-child box">
              <AdminDashboardTotalSlotsByRole :data="dashboard.totalSlotsPerRole" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <AdminDashboardFreeSlotsByList :data="dashboard.freeSlotsPerList" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <AdminDashboardAccountsByAlliance :data="dashboard.accountsPerAlliance" />
            </article>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAdminStore } from '~/stores/admin'
import { useAuthStore } from '~/stores/auth'

definePageMeta({
  middleware: 'auth'
})

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
      severity: 'error',
      summary: 'Access Denied',
      detail: 'You do not have permission to access this page.',
      life: 5000
    })
    await router.push('/')
    return
  }

  try {
    await adminStore.loadDashboard()
  } catch (error) {
    console.error('Failed to load dashboard:', error)
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'Failed to load dashboard data. Please try again.',
      life: 5000
    })
  }
})
</script>
