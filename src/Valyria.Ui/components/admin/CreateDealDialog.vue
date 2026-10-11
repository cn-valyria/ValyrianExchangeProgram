<template>
  <UModal :close="{ onClick: () => emit('close', false) }">
    <template #header>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        Create Deal
      </h3>
    </template>

    <template #body>
      <div class="space-y-6">
        <p class="text-sm text-gray-600 dark:text-gray-400">
          Pair an open slot from one account with an open slot from another.
          Matching normally happens automatically, so only do this to fix up a
          specific pairing.
        </p>

        <AdminDealSlotPicker
          v-model="slotA"
          label="First Account"
          :accounts="props.accounts"
        />
        <AdminDealSlotPicker
          v-model="slotB"
          label="Second Account"
          :accounts="props.accounts"
        />

        <UAlert
          v-if="isSameAccount"
          color="error"
          icon="i-heroicons-x-circle"
          title="Both slots belong to the same account"
          description="A deal must be between two different accounts."
        />
        <UAlert
          v-else-if="slotA && slotB && !slotsAreCompatible"
          color="warning"
          icon="i-heroicons-exclamation-triangle"
          title="These slots aren't an exact match"
          description="What one slot sends doesn't line up with what the other expects to receive. The deal can still be created, but both nations will be told to send different amounts than their partner expects."
        />
        <UAlert
          v-else-if="slotA && slotB"
          color="success"
          icon="i-heroicons-check-circle"
          :title="`${slotA.account.rulerName} sends ${describeAid(slotA.techSentCycles, slotA.cashSentCycles)}; ${slotB.account.rulerName} sends ${describeAid(slotB.techSentCycles, slotB.cashSentCycles)}`"
          description="Per 10-day cycle."
        />
      </div>
    </template>

    <template #footer>
      <div class="flex gap-3 justify-end w-full">
        <UButton label="Cancel" color="neutral" @click="emit('close', false)" />
        <UButton
          label="Create Deal"
          color="success"
          :disabled="!slotA || !slotB || isSameAccount"
          :loading="isSaving"
          @click="createDeal()"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { describeAid } from '~/infrastructure/deals'
import type { SlotDetail } from '~/stores/account'
import type { AdminAccount } from '~/stores/adminAccounts'
import { useAdminDealsStore } from '~/stores/adminDeals'

const props = defineProps<{
  accounts: AdminAccount[]
}>()

const emit = defineEmits<{ close: [boolean] }>()

const toast = useToast()
const adminDealsStore = useAdminDealsStore()

const slotA = ref<SlotDetail | null>(null)
const slotB = ref<SlotDetail | null>(null)
const isSaving = ref<boolean>(false)

const isSameAccount = computed(
  () =>
    slotA.value !== null &&
    slotB.value !== null &&
    slotA.value.account.accountId === slotB.value.account.accountId,
)

// Same rule the matcher uses: each slot's cycles are the exact inverse of the other's
const slotsAreCompatible = computed(() => {
  const a = slotA.value
  const b = slotB.value
  if (!a || !b) return false
  return (
    a.techSentCycles === b.techReceivedCycles &&
    a.techReceivedCycles === b.techSentCycles &&
    a.cashSentCycles === b.cashReceivedCycles &&
    a.cashReceivedCycles === b.cashSentCycles
  )
})

const createDeal = async () => {
  if (!slotA.value || !slotB.value) return

  isSaving.value = true
  try {
    await adminDealsStore.createDeal(slotA.value.id, slotB.value.id)
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Deal created.',
      duration: 3000,
    })
    emit('close', true)
  } catch (e) {
    console.error(e)
    toast.add({
      color: 'error',
      title: 'Error',
      description: ERROR_MESSAGES.default,
      duration: 5000,
    })
  } finally {
    isSaving.value = false
  }
}
</script>
