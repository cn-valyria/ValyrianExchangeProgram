<template>
  <UModal v-model="isOpen" @update:model-value="handleClose">
    <UCard :ui="{ ring: '', divide: 'divide-y divide-gray-200 dark:divide-gray-700' }">
      <template #header>
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
          {{ transactionBeingEdited.id !== undefined ? "Update" : "New"}} Manual Transaction
        </h3>
      </template>

      <div class="space-y-6">
        <!-- Manual Data Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Manual Data</h2>

          <fieldset :disabled="isInUpdateState" class="space-y-4">
            <UFormGroup
              label="Account"
              :error="validator.propertyHasValidationError('nation') ? 'Cannot create a transaction without an assigned account' : undefined"
            >
              <USelectMenu
                v-model="selectedAccount"
                :options="props.accounts"
                searchable
                searchable-placeholder="Search accounts..."
                placeholder="No account selected"
                value-attribute="nationId"
                option-attribute="rulerName"
                by="nationId"
              >
                <template #label>
                  <span v-if="selectedAccount">{{ selectedAccount.rulerName }}</span>
                  <span v-else class="text-gray-400">No account selected</span>
                </template>
              </USelectMenu>
            </UFormGroup>
          </fieldset>

          <fieldset disabled class="space-y-4 mt-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormGroup label="Nation Name">
                <UInput v-model="transactionNation.nationName" />
              </UFormGroup>
              <UFormGroup label="Ruler Name">
                <UInput v-model="transactionNation.rulerName" />
              </UFormGroup>
            </div>
            <UFormGroup label="Alliance Name">
              <UInput v-model="transactionNation.allianceName" />
            </UFormGroup>
          </fieldset>
        </div>

        <!-- Transaction Fields Section -->
        <div class="space-y-4">
          <UFormGroup
            label="Adjustment Type"
            :error="validator.propertyHasValidationError('adjustmentType') ? 'Every manual transaction must be either credit or debt' : undefined"
          >
            <USelectMenu
              v-model="transactionBeingEdited.adjustmentType"
              :options="adjustmentTypes"
              placeholder="Select adjustment type"
            />
          </UFormGroup>

          <UFormGroup label="Reason">
            <UInput v-model="transactionBeingEdited.reason" />
          </UFormGroup>
        </div>

        <!-- Transaction Metadata Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Transaction Metadata</h2>

          <div class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormGroup
                label="Classification"
                :error="validator.propertyHasValidationError('classification') ? 'Every transaction must be given a classification' : undefined"
              >
                <USelectMenu
                  v-model="transactionBeingEdited.classification"
                  :options="classifications"
                  value-attribute="id"
                  option-attribute="description"
                  placeholder="Select classification"
                />
              </UFormGroup>

              <UFormGroup
                label="Rate"
                :error="validator.propertyHasValidationError('rate') ? 'Please define a valid rate for the transaction credits' : undefined"
              >
                <UInput
                  v-model="transactionBeingEdited.rate"
                  type="number"
                />
              </UFormGroup>
            </div>

            <fieldset disabled>
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
      </div>

      <template #footer>
        <div class="flex gap-3 justify-end">
          <UButton label="Cancel" color="gray" @click="handleClose()" />
          <UButton label="Save" color="green" :loading="buttonIsLoading" @click="save()" />
        </div>
      </template>
    </UCard>
  </UModal>
</template>

<script setup lang="ts">
import { useAdminTransactionsStore } from '~/stores/adminTransactions'
import { ADJUSTMENT_TYPES, ERROR_MESSAGES } from '~/infrastructure/constants'
import { transactionClassifications } from '~/infrastructure/dataLists'
import Validator from '~/infrastructure/validator'

interface Nation {
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
}

interface Account {
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
  role: string
}

interface Transaction {
  id?: number
  nation?: Nation | null
  adjustmentType?: string
  reason?: string
  classification?: number | null
  rate?: number | string
  cashMovedCashCredit?: number
  cashMovedTechCredit?: number
  techMovedCashCredit?: number
  techMovedTechCredit?: number
}

interface Props {
  show: boolean
  transaction?: Transaction
  accounts: Account[]
}

interface Emits {
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  transaction: () => ({} as Transaction),
  accounts: () => []
})

const emit = defineEmits<Emits>()

const toast = useToast()
const adminTransactionsStore = useAdminTransactionsStore()

const isOpen = ref(false)
const buttonIsLoading = ref(false)
const transactionBeingEdited = ref<Transaction>({} as Transaction)
const adjustmentTypes = [ADJUSTMENT_TYPES.credit, ADJUSTMENT_TYPES.debt]
const classifications = transactionClassifications

const validator = ref(new Validator())

const selectedAccount = computed({
  get() {
    const txn = transactionBeingEdited.value
    if (txn.nation === undefined || txn.nation === null) {
      return undefined
    }

    return props.accounts.find(account => account.nationId === txn.nation!.nationId)
  },
  set(val: Account | undefined) {
    if (val === undefined || val === null) {
      transactionBeingEdited.value.nation = null
    } else {
      transactionBeingEdited.value.nation = {
        nationId: val.nationId,
        nationName: val.nationName,
        rulerName: val.rulerName,
        allianceName: val.allianceName
      }
    }
    recalculateCredits()
  }
})

const transactionNation = computed(() => {
  if (transactionBeingEdited.value.nation) {
    return transactionBeingEdited.value.nation
  } else {
    return {} as Nation
  }
})

const isInUpdateState = computed(() => {
  return transactionBeingEdited.value !== undefined && transactionBeingEdited.value.id !== undefined
})

watch(() => props.show, (val) => {
  isOpen.value = val
})

watch(() => props.transaction, (val) => {
  transactionBeingEdited.value = { ...val }
  validator.value = new Validator(transactionBeingEdited.value)

  // Recalculate the credits when the transaction changes, since they're always readonly
  recalculateCredits()
})

watch(() => transactionBeingEdited.value.adjustmentType, () => {
  recalculateCredits()
})

watch(() => transactionBeingEdited.value.rate, () => {
  recalculateCredits()
})

const handleClose = () => {
  emit("close")
}

const transactionIsValid = () => {
  validator.value.validateProperty("nation", (val: any) => val === undefined || val === null)
  validator.value.validateProperty("adjustmentType")
  validator.value.validateProperty("classification")
  validator.value.validateProperty("rate", (val: any) => val === undefined || val === "")

  return validator.value.isValid
}

const save = async () => {
  if (!transactionIsValid()) {
    console.error("Transaction form is invalid and will not be submitted to the API")
    return
  }

  buttonIsLoading.value = true
  const txn = transactionBeingEdited.value

  try {
    const payload = {
      aidId: null,
      sendingNationId: txn.adjustmentType === ADJUSTMENT_TYPES.credit ? txn.nation!.nationId : null,
      receivingNationId: txn.adjustmentType === ADJUSTMENT_TYPES.debt ? txn.nation!.nationId : null,
      reasonOverride: txn.reason,
      lu: selectedAccount.value!.role,
      classification: parseInt(txn.classification as any),
      rate: parseInt(txn.rate as any),
      cashMovedCashCredit: txn.cashMovedCashCredit,
      cashMovedTechCredit: txn.cashMovedTechCredit,
      techMovedCashCredit: txn.techMovedCashCredit,
      techMovedTechCredit: txn.techMovedTechCredit
    }

    if (txn.id !== undefined) {
      await adminTransactionsStore.updateTransaction({ ...payload, id: txn.id })
    } else {
      await adminTransactionsStore.createTransaction(payload)
    }

    toast.add({ severity: 'success', summary: 'Success', detail: 'Transaction saved successfully!', life: 3000 })
    emit('close')
  } catch (e) {
    console.error(e)
    toast.add({ severity: 'error', summary: 'Error', detail: ERROR_MESSAGES.default, life: 3000 })
  }

  buttonIsLoading.value = false
}

const recalculateCredits = () => {
  const txn = transactionBeingEdited.value
  if (txn.nation === undefined || txn.nation === null) {
    zeroOutCredits(txn)
    return
  }

  if (selectedAccount.value === undefined) {
    zeroOutCredits(txn)
    return
  }

  const rate = parseInt(txn.rate as any)
  if (isNaN(rate) || rate === 0) {
    zeroOutCredits(txn)
  } else if (txn.adjustmentType === ADJUSTMENT_TYPES.credit) {
    switch (selectedAccount.value.role) {
      case "B":
      case "W":
        updateCredits(txn, rate, rate / 9 * 100, 0, 0)
        break
      case "S":
      case "N":
      case "Q":
        updateCredits(txn, 0, 0, rate, rate / 9 * 100)
        break
      default:
        zeroOutCredits(txn)
        break
    }
  } else if (txn.adjustmentType === ADJUSTMENT_TYPES.debt) {
    switch (selectedAccount.value.role) {
      case "B":
      case "W":
        updateCredits(txn, 0, 0, rate, rate / 9 * 100)
        break
      case "S":
      case "N":
      case "Q":
        updateCredits(txn, rate, rate / 9 * 100, 0, 0)
        break
      default:
        zeroOutCredits(txn)
        break
    }
  } else {
    zeroOutCredits(txn)
  }
}

const updateCredits = (txn: Transaction, cc: number, ct: number, tc: number, tt: number) => {
  txn.cashMovedCashCredit = cc
  txn.cashMovedTechCredit = ct
  txn.techMovedCashCredit = tc
  txn.techMovedTechCredit = tt
}

const zeroOutCredits = (txn: Transaction) => {
  updateCredits(txn, 0, 0, 0, 0)
}
</script>
