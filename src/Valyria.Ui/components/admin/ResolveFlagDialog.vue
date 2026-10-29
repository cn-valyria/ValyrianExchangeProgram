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
        <UCheckbox
          v-if="canHoldAccount"
          v-model="holdAccount"
          :label="`Also put ${flaggedName} on hold`"
          description="Cancels their other active deals and stops them being matched until an admin changes their role back."
        />
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
import { ACCOUNT_ROLE_CODES, ERROR_MESSAGES } from '~/infrastructure/constants'
import {
  addDays,
  getAccountName,
  getCnToday,
  getDealStatusInfo,
  getFlagTypeInfo,
  getResolutionOptions,
  type FlagResolution,
} from '~/infrastructure/flags'
import { DealExtensionKind } from '~/stores/account'
import { useAdminAccountsStore } from '~/stores/adminAccounts'
import { useAdminDealsStore, type DealFlag } from '~/stores/adminDeals'

const props = defineProps<{
  flag: DealFlag
  // The other account's open flag for the same failed offer, if any
  partnerFlag?: DealFlag
}>()

const emit = defineEmits<{ close: [boolean] }>()

const toast = useToast()
const adminDealsStore = useAdminDealsStore()
const adminAccountsStore = useAdminAccountsStore()

const options = computed(() =>
  getResolutionOptions(props.flag, props.partnerFlag),
)
const typeInfo = computed(() => getFlagTypeInfo(props.flag))
const dealStatus = computed(() => getDealStatusInfo(props.flag))
const flaggedName = computed(() => getAccountName(props.flag.flaggedAccount))

// Nothing is picked up front: the options do very different things, so each needs reading
const resolution = ref<FlagResolution | null>(null)
const notes = ref<string>('')
const holdAccount = ref<boolean>(false)
const isSaving = ref<boolean>(false)

const canHoldAccount = computed(
  () =>
    resolution.value === 'cancelDeal' &&
    props.flag.flaggedAccountRole !== ACCOUNT_ROLE_CODES.onHold,
)

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
      case null:
        return { label: 'Resolve', color: 'primary' }
      case 'extendFiveDays':
        return { label: 'Give 5 More Days', color: 'success' }
      case 'extendUntilNextLeg':
        return { label: 'Hold Off Until Next Aid', color: 'success' }
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
  () =>
    resolution.value !== null &&
    (resolution.value !== 'cancelDeal' || notes.value.trim().length > 0),
)

// Built on success: only warnings on running deals have a deadline to extend
const getSuccessMessage = (chosen: FlagResolution, held: boolean): string => {
  const deal = `deal #${props.flag.dealId}`
  switch (chosen) {
    case 'extendFiveDays':
      return `${flaggedName.value} now has until ${addDays(props.flag.cancelsOn ?? getCnToday(), 5)} on ${deal}.`
    case 'extendUntilNextLeg':
      return `Deal #${props.flag.dealId} won't be cancelled before its next aid is recorded.`
    case 'clearBoth':
      return 'Both flags cleared.'
    case 'restore':
      return `Deal #${props.flag.dealId} restored.`
    case 'cancelDeal':
      return held
        ? `Deal #${props.flag.dealId} cancelled, ${flaggedName.value} put on hold and the warning cleared.`
        : `Deal #${props.flag.dealId} cancelled and the warning cleared.`
    default:
      return 'Flag cleared.'
  }
}

const resolve = async () => {
  if (resolution.value === null) return
  const chosen = resolution.value
  const trimmedNotes = notes.value.trim()
  const held = canHoldAccount.value && holdAccount.value
  isSaving.value = true
  try {
    switch (chosen) {
      case 'extendFiveDays':
      case 'extendUntilNextLeg':
        await adminDealsStore.extendFlaggedDeal(
          props.flag.id,
          chosen === 'extendFiveDays'
            ? DealExtensionKind.FiveDays
            : DealExtensionKind.UntilNextLeg,
          trimmedNotes,
        )
        break
      case 'restore':
        await adminDealsStore.restoreFlaggedDeal(props.flag.id, trimmedNotes)
        break
      case 'cancelDeal':
        // Cancelled before the hold so this deal keeps the admin's reason. The flag is
        // resolved last so it stays open to retry from if the hold fails
        await adminDealsStore.cancelDeal(props.flag.dealId, trimmedNotes)
        if (held) {
          await adminAccountsStore.holdAccount(
            props.flag.flaggedAccount.accountId,
          )
        }
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
      description: getSuccessMessage(chosen, held),
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
          ? "This deal has changed since the page loaded, so that can't be done any more. Reload the page to see why."
          : ERROR_MESSAGES.default,
      duration: 5000,
    })
  } finally {
    isSaving.value = false
  }
}
</script>
