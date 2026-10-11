<template>
  <UModal :close="{ onClick: () => emit('close', false) }">
    <template #header>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        Cancel Deal #{{ props.deal.id }}
      </h3>
    </template>

    <template #body>
      <div class="space-y-4">
        <p class="text-sm text-gray-600 dark:text-gray-400">
          This ends the deal between
          <span class="font-semibold">{{
            props.deal.slot1.account.rulerName
          }}</span>
          and
          <span class="font-semibold">{{
            props.deal.slot2.account.rulerName
          }}</span
          >. Both slots become open and will be re-matched on the next matching
          run.
        </p>

        <UFormField
          label="Reason"
          required
          help="Shown in both accounts' deal history."
        >
          <UTextarea
            v-model="reason"
            :rows="3"
            placeholder="Why is this deal being cancelled?"
            class="w-full"
          />
        </UFormField>
      </div>
    </template>

    <template #footer>
      <div class="flex gap-3 justify-end w-full">
        <UButton
          label="Keep Deal"
          color="neutral"
          @click="emit('close', false)"
        />
        <UButton
          label="Cancel Deal"
          color="error"
          :disabled="reason.trim().length === 0"
          :loading="isSaving"
          @click="cancelDeal()"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { useAdminDealsStore, type AdminDeal } from '~/stores/adminDeals'

const props = defineProps<{
  deal: AdminDeal
}>()

const emit = defineEmits<{ close: [boolean] }>()

const toast = useToast()
const adminDealsStore = useAdminDealsStore()

const reason = ref<string>('')
const isSaving = ref<boolean>(false)

const cancelDeal = async () => {
  isSaving.value = true
  try {
    await adminDealsStore.cancelDeal(props.deal.id, reason.value.trim())
    toast.add({
      color: 'success',
      title: 'Success',
      description: `Deal #${props.deal.id} cancelled.`,
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
