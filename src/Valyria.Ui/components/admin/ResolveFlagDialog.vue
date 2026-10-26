<template>
  <UModal :close="{ onClick: () => emit('close', false) }">
    <template #header>
      <div>
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
          Resolve Flag on Deal #{{ props.flag.dealId }}
        </h3>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          {{ typeInfo.label }} against {{ flaggedName }} ·
          {{ dealStatus.label }}
        </p>
      </div>
    </template>

    <template #body>
      <div class="space-y-4">
        <URadioGroup
          v-model="resolution"
          :items="radioItems"
          variant="card"
          class="w-full"
        />

        <UFormField
          v-if="resolution === 'cancelDeal'"
          label="Reason"
          required
          help="Shown in both accounts' deal history."
        >
          <UTextarea
            v-model="notes"
            :rows="3"
            placeholder="Why is this deal being cancelled?"
            class="w-full"
          />
        </UFormField>
        <UFormField v-else label="Admin Notes">
          <UTextarea
            v-model="notes"
            :rows="3"
            placeholder="Optional notes on how this was resolved"
            class="w-full"
          />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <div class="flex gap-3 justify-end w-full">
        <UButton label="Cancel" color="neutral" @click="emit('close', false)" />
        <UButton
          :label="confirmButton.label"
          :color="confirmButton.color"
          :disabled="!canConfirm"
          :loading="isSaving"
          @click="resolve()"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import type { ApiError } from '~/composables/useApiClient'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import {
  getAccountName,
  getDealStatusInfo,
  getFlagTypeInfo,
  getResolutionOptions,
  type FlagResolution,
} from '~/infrastructure/flags'
import { useAdminDealsStore, type DealFlag } from '~/stores/adminDeals'

const props = defineProps<{
  flag: DealFlag
  // The other account's open flag for the same failed offer, if any
  partnerFlag?: DealFlag
}>()

const emit = defineEmits<{ close: [boolean] }>()

const toast = useToast()
const adminDealsStore = useAdminDealsStore()

const options = computed(() =>
  getResolutionOptions(props.flag, props.partnerFlag),
)
const typeInfo = computed(() => getFlagTypeInfo(props.flag))
const dealStatus = computed(() => getDealStatusInfo(props.flag))
const flaggedName = computed(() => getAccountName(props.flag.flaggedAccount))

// Clearing is always on offer, and changes the least
const resolution = ref<FlagResolution>('clear')
const notes = ref<string>('')
const isSaving = ref<boolean>(false)

const radioItems = computed(() =>
  options.value.map((option) => ({
    value: option.value,
    label: option.label,
    description:
      option.disabledReason !== null
        ? `Not available: ${option.disabledReason}`
        : option.description,
    disabled: option.disabledReason !== null,
  })),
)

const confirmButton = computed(
  (): { label: string; color: 'success' | 'error' | 'primary' } => {
    switch (resolution.value) {
      case 'restore':
        return { label: 'Restore Deal', color: 'success' }
      case 'cancelDeal':
        return { label: 'Cancel Deal', color: 'error' }
      case 'clearBoth':
        return { label: 'Clear Both Flags', color: 'primary' }
      default:
        return { label: 'Clear Flag', color: 'primary' }
    }
  },
)

const canConfirm = computed(
  () => resolution.value !== 'cancelDeal' || notes.value.trim().length > 0,
)

const successMessages: Record<FlagResolution, string> = {
  clear: 'Flag cleared.',
  clearBoth: 'Both flags cleared.',
  restore: `Deal #${props.flag.dealId} restored.`,
  cancelDeal: `Deal #${props.flag.dealId} cancelled and the warning cleared.`,
}

const resolve = async () => {
  const trimmedNotes = notes.value.trim()
  isSaving.value = true
  try {
    switch (resolution.value) {
      case 'restore':
        await adminDealsStore.restoreFlaggedDeal(props.flag.id, trimmedNotes)
        break
      case 'cancelDeal':
        await adminDealsStore.cancelDeal(props.flag.dealId, trimmedNotes)
        await adminDealsStore.resolveFlag(props.flag.id, trimmedNotes)
        break
      case 'clearBoth':
        await adminDealsStore.resolveFlag(props.flag.id, trimmedNotes)
        if (props.partnerFlag) {
          await adminDealsStore.resolveFlag(props.partnerFlag.id, trimmedNotes)
        }
        break
      default:
        await adminDealsStore.resolveFlag(props.flag.id, trimmedNotes)
    }
    toast.add({
      color: 'success',
      title: 'Success',
      description: successMessages[resolution.value],
      duration: 3000,
    })
    emit('close', true)
  } catch (e) {
    console.error(e)
    toast.add({
      color: 'error',
      title: 'Error',
      description:
        (e as ApiError)?.statusCode === 409
          ? "This deal can't be restored any more. Reload the page to see why."
          : ERROR_MESSAGES.default,
      duration: 5000,
    })
  } finally {
    isSaving.value = false
  }
}
</script>
