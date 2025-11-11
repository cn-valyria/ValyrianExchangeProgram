<template>
  <div style="position: relative">
    <ProgressSpinner v-if="loading" style="width: 50px; height: 50px; position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" />
    <section class="hero is-light is-medium">
      <div class="hero-body">
        <p class="title">
          <span class="has-text-weight-bold">{{ account.rulerName }}</span> of {{ account.nationName }}
        </p>
        <p class="subtitle">
          <div class="flex gap-2">
            <ChipAccountRole :code="account.role"></ChipAccountRole>
            <ChipAllianceAcronym :allianceName="account.allianceName"></ChipAllianceAcronym>
          </div>
        </p>
      </div>
    </section>
    <div class="container">
      <div class="section">
        <AccountBalance :credit="account.credit" :debt="account.debt" :role="account.role"></AccountBalance>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <AccountAidList :account="account" :aidList="aidList"></AccountAidList>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <AccountCreditHistory :accountRole="account.role" :creditHistory="creditHistory"></AccountCreditHistory>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { useAuthStore } from '~/stores/auth'
import { accountRoles } from '~/infrastructure/dataLists'

definePageMeta({
  middleware: 'auth'
})

const route = useRoute()
const router = useRouter()
const toast = useToast()

const accountStore = useAccountStore()
const authStore = useAuthStore()

const { account, aidList } = storeToRefs(accountStore)
const { creditHistory } = accountStore
const { loggedInUser } = storeToRefs(authStore)

const loading = ref<boolean>(true)
const roles = accountRoles

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
      severity: 'error',
      summary: 'Access Denied',
      detail: "You're not logged into the account that you tried to access.",
      life: 5000
    })
    await router.push('/')
    return
  }

  try {
    await accountStore.loadAccount(accountId.value)
    await accountStore.loadAidList(accountId.value)
    await accountStore.loadTransactionHistory(accountId.value)
  } catch (error) {
    console.error('Failed to load account data:', error)
    toast.add({
      severity: 'error',
      summary: 'Error',
      detail: 'Failed to load account data. Please try again.',
      life: 5000
    })
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
/* Override loading overlay background if needed */
:deep(.p-progress-spinner) {
  background: hsla(0, 0%, 100%, 0.8);
  border-radius: 50%;
  padding: 1rem;
}
</style>
