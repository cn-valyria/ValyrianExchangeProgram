<template>
  <UModal v-model="isOpen" @update:model-value="handleClose">
    <UCard :ui="{ ring: '', divide: 'divide-y divide-gray-200 dark:divide-gray-700' }">
      <template #header>
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
          Update Transaction
        </h3>
      </template>

      <div v-if="transactionBeingEdited !== undefined" class="space-y-6">
        <!-- Aid-Based Data Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Aid-Based Data</h2>

          <fieldset disabled class="space-y-4">
            <UFormGroup label="Aid ID">
              <UInput
                v-model="transactionBeingEdited.aidId"
                placeholder="The CN-generated ID for the aid between two nations"
              />
            </UFormGroup>

            <div>
              <h5 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Sent By</h5>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <UFormGroup label="Nation Name">
                  <UInput :model-value="transactionBeingEdited.sentBy?.nationName ?? ''" />
                </UFormGroup>
                <UFormGroup label="Ruler Name">
                  <UInput :model-value="transactionBeingEdited.sentBy?.rulerName ?? ''" />
                </UFormGroup>
              </div>
            </div>

            <div>
              <h5 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">Received By</h5>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <UFormGroup label="Nation Name">
                  <UInput :model-value="transactionBeingEdited.receivedBy?.nationName ?? ''" />
                </UFormGroup>
                <UFormGroup label="Ruler Name">
                  <UInput :model-value="transactionBeingEdited.receivedBy?.rulerName ?? ''" />
                </UFormGroup>
              </div>
            </div>

            <UFormGroup label="Status">
              <UInput v-model="transactionBeingEdited.status" />
            </UFormGroup>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <UFormGroup label="Money">
                <UInput v-model="transactionBeingEdited.money" />
              </UFormGroup>
              <UFormGroup label="Technology">
                <UInput v-model="transactionBeingEdited.technology" />
              </UFormGroup>
              <UFormGroup label="Soldiers">
                <UInput v-model="transactionBeingEdited.soldiers" />
              </UFormGroup>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormGroup label="Starts On">
                <UInput v-model="transactionBeingEdited.startsOn" />
              </UFormGroup>
              <UFormGroup label="Ends On">
                <UInput v-model="transactionBeingEdited.endsOn" />
              </UFormGroup>
            </div>

            <UFormGroup label="Reason">
              <UInput v-model="transactionBeingEdited.reason" />
            </UFormGroup>
          </fieldset>
        </div>

        <!-- Transaction Metadata Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Transaction Metadata</h2>

          <fieldset disabled class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormGroup label="Classification">
                <USelectMenu
                  v-model="transactionBeingEdited.classification"
                  :options="classifications"
                  value-attribute="id"
                  option-attribute="description"
                />
              </UFormGroup>
              <UFormGroup label="Rate">
                <UInput v-model="transactionBeingEdited.rate" />
              </UFormGroup>
            </div>

            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
              <UFormGroup label="Cash Moved (Cash)">
                <UInput v-model="transactionBeingEdited.cashMovedCashCredit" />
              </UFormGroup>
              <UFormGroup label="Cash Moved (Tech)">
                <UInput v-model="transactionBeingEdited.cashMovedTechCredit" />
              </UFormGroup>
              <UFormGroup label="Tech Moved (Cash)">
                <UInput v-model="transactionBeingEdited.techMovedCashCredit" />
              </UFormGroup>
              <UFormGroup label="Tech Moved (Tech)">
                <UInput v-model="transactionBeingEdited.techMovedTechCredit" />
              </UFormGroup>
            </div>
          </fieldset>
        </div>
      </div>

      <template #footer>
        <div class="flex justify-end">
          <UButton label="Close" color="gray" @click="handleClose" />
        </div>
      </template>
    </UCard>
  </UModal>
</template>

<script setup lang="ts">
import { transactionClassifications } from '~/infrastructure/dataLists'

interface Nation {
  nationName: string
  rulerName: string
}

interface Transaction {
  aidId?: string
  sentBy?: Nation
  receivedBy?: Nation
  status?: string
  money?: string
  technology?: string
  soldiers?: string
  startsOn?: string
  endsOn?: string
  reason?: string
  classification?: number
  rate?: string
  cashMovedCashCredit?: string
  cashMovedTechCredit?: string
  techMovedCashCredit?: string
  techMovedTechCredit?: string
}

interface Props {
  show: boolean
  transaction?: Transaction
}

interface Emits {
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  transaction: () => ({} as Transaction)
})

const emit = defineEmits<Emits>()

const isOpen = ref(false)
const transactionBeingEdited = ref<Transaction>({} as Transaction)
const classifications = transactionClassifications

watch(() => props.show, (val) => {
  isOpen.value = val
})

watch(() => props.transaction, (val) => {
  transactionBeingEdited.value = { ...val }
})

const handleClose = () => {
  emit('close')
}
</script>
