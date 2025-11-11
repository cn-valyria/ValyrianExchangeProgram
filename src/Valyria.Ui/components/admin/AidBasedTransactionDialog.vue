<template>
  <Dialog :visible="show" modal :style="{ width: '50rem' }" @update:visible="$emit('close')">
    <template #header>
      <h3>Update Transaction</h3>
    </template>

    <div v-if="transactionBeingEdited !== undefined" class="content">
      <h2>Aid-Based Data</h2>
      <fieldset disabled>
        <div class="field">
          <label class="label">Aid ID</label>
          <div class="control">
            <InputText
              v-model="transactionBeingEdited.aidId"
              placeholder="The CN-generated ID for the aid between two nations"
              class="w-full"
            />
          </div>
        </div>
        <h5>Sent By</h5>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Nation Name</label>
              <div class="control">
                <InputText :modelValue="transactionBeingEdited.sentBy?.nationName ?? ''" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Ruler Name</label>
              <div class="control">
                <InputText :modelValue="transactionBeingEdited.sentBy?.rulerName ?? ''" class="w-full" />
              </div>
            </div>
          </div>
        </div>
        <h5>Received By</h5>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Nation Name</label>
              <div class="control">
                <InputText :modelValue="transactionBeingEdited.receivedBy?.nationName ?? ''" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Ruler Name</label>
              <div class="control">
                <InputText :modelValue="transactionBeingEdited.receivedBy?.rulerName ?? ''" class="w-full" />
              </div>
            </div>
          </div>
        </div>
        <div class="field">
          <label class="label">Status</label>
          <div class="control">
            <InputText v-model="transactionBeingEdited.status" class="w-full" />
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Money</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.money" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Technology</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.technology" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Soldiers</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.soldiers" class="w-full" />
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Starts On</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.startsOn" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Ends On</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.endsOn" class="w-full" />
              </div>
            </div>
          </div>
        </div>
        <div class="field">
          <label class="label">Reason</label>
          <div class="control">
            <InputText v-model="transactionBeingEdited.reason" class="w-full" />
          </div>
        </div>
      </fieldset>
      <h2>Transaction Metadata</h2>
      <fieldset disabled>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Classification</label>
              <div class="control">
                <Dropdown
                  v-model="transactionBeingEdited.classification"
                  :options="classifications"
                  optionLabel="description"
                  optionValue="id"
                  class="w-full"
                />
              </div>
            </div>
            <div class="field">
              <label class="label">Rate</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.rate" class="w-full" />
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Cash Moved <br />(Cash)</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.cashMovedCashCredit" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Cash Moved <br />(Tech)</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.cashMovedTechCredit" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Tech Moved <br />(Cash)</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.techMovedCashCredit" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Tech Moved <br />(Tech)</label>
              <div class="control">
                <InputText v-model="transactionBeingEdited.techMovedTechCredit" class="w-full" />
              </div>
            </div>
          </div>
        </div>
      </fieldset>
    </div>

    <template #footer>
      <Button label="Close" severity="secondary" @click="$emit('close')" />
    </template>
  </Dialog>
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

defineEmits<Emits>()

const transactionBeingEdited = ref<Transaction>({} as Transaction)
const classifications = transactionClassifications

watch(() => props.transaction, (val) => {
  transactionBeingEdited.value = { ...val }
})
</script>
