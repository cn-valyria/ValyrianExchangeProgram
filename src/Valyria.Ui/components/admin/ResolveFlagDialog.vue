<template>
  <UModal :close="{ onClick: () => emit('close', false) }">
    <template #header>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        Resolve Flag on Deal #{{ props.flag.dealId }}
      </h3>
    </template>

    <template #body>
      <div class="space-y-4">
        <p class="text-sm text-gray-600 dark:text-gray-400">
          Resolving this flag lets the flagged account be matched with new
          partners again (once it has no other open flags), and clears its
          notification. It doesn't change or restore the deal itself.
        </p>

        <UFormField label="Admin Notes">
          <UTextarea
            v-model="adminNotes"
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
          label="Resolve"
          color="success"
          :loading="isSaving"
          @click="resolve()"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { useAdminDealsStore, type DealFlag } from '~/stores/adminDeals'

const props = defineProps<{
  flag: DealFlag
}>()

const emit = defineEmits<{ close: [boolean] }>()

const toast = useToast()
const adminDealsStore = useAdminDealsStore()

const adminNotes = ref<string>('')
const isSaving = ref<boolean>(false)

const resolve = async () => {
  isSaving.value = true
  try {
    await adminDealsStore.resolveFlag(props.flag.id, adminNotes.value.trim())
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Flag resolved.',
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
