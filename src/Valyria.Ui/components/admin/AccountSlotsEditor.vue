<template>
  <div class="space-y-4">
    <UAlert
      color="info"
      icon="i-heroicons-information-circle"
      description="Values are aid packages per 10-day cycle. Slots are added or removed automatically when FM/DRA change, and reset to role defaults when the role changes. Editing a slot that's in a deal changes what this account is told to send right away, but not the partner's slot."
    />

    <div v-if="isLoading" class="flex items-center gap-2 text-sm text-gray-500 px-2">
      <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" /> Loading slots...
    </div>
    <p v-else-if="slots.length === 0" class="text-sm text-gray-500 px-2">This account has no slots.</p>

    <div
      v-for="slot in slots"
      v-else
      :key="slot.id"
      class="p-3 rounded-lg bg-gray-50 dark:bg-gray-800 space-y-3"
    >
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-gray-900 dark:text-white">Slot #{{ slot.slotNumber }}</span>
          <UBadge :color="slot.hasActiveDeal ? 'success' : 'info'" variant="subtle" size="sm">
            {{ slot.hasActiveDeal ? 'In a deal' : 'Open' }}
          </UBadge>
        </div>
        <UButton
          label="Save"
          color="success"
          size="xs"
          :disabled="!isDirty(slot)"
          :loading="savingSlotId === slot.id"
          @click="saveSlot(slot)"
        />
      </div>

      <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
        <UFormField label="Tech Sent">
          <UInputNumber v-model="slot.techSentCycles" :min="0" class="w-full" />
        </UFormField>
        <UFormField label="Tech Rec'd">
          <UInputNumber v-model="slot.techReceivedCycles" :min="0" class="w-full" />
        </UFormField>
        <UFormField label="Cash Sent">
          <UInputNumber v-model="slot.cashSentCycles" :min="0" class="w-full" />
        </UFormField>
        <UFormField label="Cash Rec'd">
          <UInputNumber v-model="slot.cashReceivedCycles" :min="0" class="w-full" />
        </UFormField>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import type { SlotDetail } from '~/stores/account'
import { useAdminAccountsStore } from '~/stores/adminAccounts'

const props = defineProps<{
  accountId: number
}>()

const toast = useToast()
const adminAccountsStore = useAdminAccountsStore()

const slots = ref<SlotDetail[]>([])
const savedSlots = ref<Record<number, SlotDetail>>({})
const isLoading = ref<boolean>(false)
const savingSlotId = ref<number | null>(null)

const isDirty = (slot: SlotDetail): boolean => {
  const saved = savedSlots.value[slot.id]
  return saved !== undefined && (
    saved.techSentCycles !== slot.techSentCycles
    || saved.techReceivedCycles !== slot.techReceivedCycles
    || saved.cashSentCycles !== slot.cashSentCycles
    || saved.cashReceivedCycles !== slot.cashReceivedCycles
  )
}

const saveSlot = async (slot: SlotDetail) => {
  savingSlotId.value = slot.id
  try {
    await adminAccountsStore.updateSlot(slot)
    savedSlots.value[slot.id] = { ...slot }
    toast.add({ color: 'success', title: 'Success', description: `Slot #${slot.slotNumber} updated.`, duration: 3000 })
  } catch (e) {
    console.error(e)
    toast.add({ color: 'error', title: 'Error', description: ERROR_MESSAGES.default, duration: 5000 })
  } finally {
    savingSlotId.value = null
  }
}

onMounted(async () => {
  isLoading.value = true
  try {
    slots.value = await adminAccountsStore.loadAccountSlots(props.accountId)
    savedSlots.value = Object.fromEntries(slots.value.map((slot) => [slot.id, { ...slot }]))
  } catch (e) {
    console.error(e)
    toast.add({ color: 'error', title: 'Error', description: "Failed to load this account's slots.", duration: 5000 })
  } finally {
    isLoading.value = false
  }
})
</script>
