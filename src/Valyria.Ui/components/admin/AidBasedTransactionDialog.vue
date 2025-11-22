<template>
  <UModal :close="{ onClick: () => emit('close') }">
    <template #header>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        Update Transaction
      </h3>
    </template>

    <template #body>
      <div v-if="state !== undefined" class="space-y-6">
        <!-- Aid-Based Data Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Aid-Based Data</h2>

          <fieldset disabled class="space-y-4">
            <UFormField label="Aid ID">
              <UInput
                v-model="state.aidId"
                placeholder="The CN-generated ID for the aid between two nations"
              />
            </UFormField>

            <div>
              <h5 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Sent By</h5>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <UFormField label="Nation Name">
                  <UInput :model-value="state.sentBy?.nationName ?? ''" />
                </UFormField>
                <UFormField label="Ruler Name">
                  <UInput :model-value="state.sentBy?.rulerName ?? ''" />
                </UFormField>
              </div>
            </div>

            <div>
              <h5 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Received By</h5>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <UFormField label="Nation Name">
                  <UInput :model-value="state.receivedBy?.nationName ?? ''" />
                </UFormField>
                <UFormField label="Ruler Name">
                  <UInput :model-value="state.receivedBy?.rulerName ?? ''" />
                </UFormField>
              </div>
            </div>

            <UFormField label="Status">
              <UInput v-model="state.status" />
            </UFormField>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <UFormField label="Money">
                <UInput v-model="state.money" />
              </UFormField>
              <UFormField label="Technology">
                <UInput v-model="state.technology" />
              </UFormField>
              <UFormField label="Soldiers">
                <UInput v-model="state.soldiers" />
              </UFormField>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormField label="Starts On">
                <UInput v-model="state.startsOn" />
              </UFormField>
              <UFormField label="Ends On">
                <UInput v-model="state.endsOn" />
              </UFormField>
            </div>

            <UFormField label="Reason">
              <UInput v-model="state.reason" />
            </UFormField>
          </fieldset>
        </div>

        <!-- Transaction Metadata Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Transaction Metadata</h2>

          <fieldset disabled class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormField label="Classification">
                <USelectMenu
                  v-model="state.classification"
                  :options="classifications"
                  value-attribute="id"
                  option-attribute="description"
                />
              </UFormField>
              <UFormField label="Rate">
                <UInput v-model="state.rate" />
              </UFormField>
            </div>

            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
              <UFormField label="Cash Moved (Cash)">
                <UInput v-model="state.cashMovedCashCredit" />
              </UFormField>
              <UFormField label="Cash Moved (Tech)">
                <UInput v-model="state.cashMovedTechCredit" />
              </UFormField>
              <UFormField label="Tech Moved (Cash)">
                <UInput v-model="state.techMovedCashCredit" />
              </UFormField>
              <UFormField label="Tech Moved (Tech)">
                <UInput v-model="state.techMovedTechCredit" />
              </UFormField>
            </div>
          </fieldset>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton label="Close" color="neutral" @click="handleClose" />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { transactionClassifications } from '~/infrastructure/dataLists'
import type { Transaction } from '~/stores/shared/transaction'

interface Props {
  transaction?: Transaction
}

interface Emits {
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  transaction: () => ({} as Transaction)
})

const emit = defineEmits<Emits>()

const state = reactive<Transaction>(props.transaction)
const classifications = transactionClassifications

const handleClose = () => {
  emit('close')
}
</script>
