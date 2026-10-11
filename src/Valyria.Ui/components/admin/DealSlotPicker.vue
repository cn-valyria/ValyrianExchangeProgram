<template>
  <div class="space-y-3">
    <UFormField :label="props.label">
      <USelectMenu
        v-model="selectedAccountId"
        :items="accountOptions"
        value-key="value"
        placeholder="Select an account"
        class="w-full"
      />
    </UFormField>

    <UFormField v-if="selectedAccountId !== undefined" label="Open Slot">
      <div
        v-if="isLoading"
        class="flex items-center gap-2 text-sm text-gray-500"
      >
        <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
        Loading open slots...
      </div>
      <p v-else-if="slots.length === 0" class="text-sm text-gray-500">
        This account has no open slots.
      </p>
      <USelectMenu
        v-else
        v-model="selectedSlotId"
        :items="slotOptions"
        value-key="value"
        placeholder="Select a slot"
        class="w-full"
      />
    </UFormField>
  </div>
</template>

<script setup lang="ts">
import { describeAid } from '~/infrastructure/deals'
import type { SlotDetail } from '~/stores/account'
import type { AdminAccount } from '~/stores/adminAccounts'
import { useAdminDealsStore } from '~/stores/adminDeals'

const props = defineProps<{
  label: string
  accounts: AdminAccount[]
}>()

const slot = defineModel<SlotDetail | null>({ default: null })

const toast = useToast()
const adminDealsStore = useAdminDealsStore()

const selectedAccountId = ref<number | undefined>(undefined)
const selectedSlotId = ref<number | undefined>(undefined)
const slots = ref<SlotDetail[]>([])
const isLoading = ref<boolean>(false)

const accountOptions = computed(() =>
  props.accounts.map((account) => ({
    label: `${account.rulerName} of ${account.nationName}`,
    value: account.id,
  })),
)

const slotOptions = computed(() =>
  slots.value.map((s) => ({
    label: `Slot #${s.slotNumber}: sends ${describeAid(s.techSentCycles, s.cashSentCycles)}, receives ${describeAid(s.techReceivedCycles, s.cashReceivedCycles)}`,
    value: s.id,
  })),
)

watch(selectedAccountId, async (accountId) => {
  selectedSlotId.value = undefined
  slots.value = []
  if (accountId === undefined) return

  isLoading.value = true
  try {
    slots.value = await adminDealsStore.loadAvailableSlots(accountId)
  } catch (e) {
    console.error(e)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load open slots for that account.',
      duration: 5000,
    })
  } finally {
    isLoading.value = false
  }
})

watch(selectedSlotId, (slotId) => {
  const selected = slots.value.find((s) => s.id === slotId) ?? null
  const account = props.accounts.find((a) => a.id === selectedAccountId.value)

  // Available-slot results don't include the owning account, so attach it for display
  slot.value =
    selected && account
      ? {
          ...selected,
          account: {
            accountId: account.id,
            nationId: account.nationId,
            nationName: account.nationName,
            rulerName: account.rulerName,
            allianceName: account.allianceName,
          },
        }
      : null
})
</script>
