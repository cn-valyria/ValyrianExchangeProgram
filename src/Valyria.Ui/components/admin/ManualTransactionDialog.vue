<template>
  <UModal :close="{ onClick: () => emit('close') }">
    <template #header>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        {{ state.id !== undefined ? "Update" : "New"}} Manual Transaction
      </h3>
    </template>

    <template #body>
      <UForm :schema="schema" :state="state" @submit="onSubmit" class="space-y-6">
        <!-- Manual Data Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Manual Data</h2>

          <fieldset :disabled="isInUpdateState" class="space-y-4">
            <UFormField label="Account" name="nation">
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
                <template #default>
                  <span v-if="selectedAccount">{{ selectedAccount.rulerName }}</span>
                  <span v-else class="text-gray-400">No account selected</span>
                </template>
              </USelectMenu>
            </UFormField>
          </fieldset>

          <fieldset disabled class="space-y-4 mt-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormField label="Nation Name">
                <UInput v-model="transactionNation.nationName" />
              </UFormField>
              <UFormField label="Ruler Name">
                <UInput v-model="transactionNation.rulerName" />
              </UFormField>
            </div>
            <UFormField label="Alliance Name">
              <UInput v-model="transactionNation.allianceName" />
            </UFormField>
          </fieldset>
        </div>

        <!-- Transaction Fields Section -->
        <div class="space-y-4">
          <UFormField label="Adjustment Type" name="adjustmentType">
            <USelectMenu
              v-model="state.adjustmentType"
              :options="adjustmentTypes"
              placeholder="Select adjustment type"
            />
          </UFormField>

          <UFormField label="Reason">
            <UInput v-model="state.reason" />
          </UFormField>
        </div>

        <!-- Transaction Metadata Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Transaction Metadata</h2>

          <div class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormField label="Classification" name="classification">
                <USelectMenu
                  v-model="state.classification"
                  :options="classifications"
                  value-attribute="id"
                  option-attribute="description"
                  placeholder="Select classification"
                />
              </UFormField>

              <UFormField label="Rate" name="rate">
                <UInput
                  v-model="state.rate"
                  type="number"
                />
              </UFormField>
            </div>

            <fieldset disabled>
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

        <!-- Hidden submit button for form submission -->
        <button type="submit" class="hidden" ref="submitButton"></button>
      </UForm>
    </template>

    <template #footer>
      <div class="flex gap-3 justify-end">
        <UButton label="Cancel" color="neutral" @click="handleClose()" />
        <UButton label="Save" color="success" :loading="buttonIsLoading" @click="submitForm()" />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { z } from 'zod'
import { useAdminTransactionsStore, type ManualTransaction } from '~/stores/adminTransactions'
import { type AdminAccount } from '~/stores/adminAccounts'
import { ADJUSTMENT_TYPES, ERROR_MESSAGES } from '~/infrastructure/constants'
import { transactionClassifications } from '~/infrastructure/dataLists'
import type { NationSimplified } from '~/stores/shared/nationSimplified'

interface Props {
  transaction?: ManualTransaction
  accounts: AdminAccount[]
}

interface Emits {
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  transaction: () => ({} as ManualTransaction),
  accounts: () => []
})

const emit = defineEmits<Emits>()

const toast = useToast()
const adminTransactionsStore = useAdminTransactionsStore()

const buttonIsLoading = ref(false)
const submitButton = ref<HTMLButtonElement>()
const adjustmentTypes = [ADJUSTMENT_TYPES.credit, ADJUSTMENT_TYPES.debt]
const classifications = transactionClassifications

// Define the form state
const state = reactive<ManualTransaction>(props.transaction)

// Define the Zod schema
const schema = z.object({
  nation: z.object({
    nationId: z.number(),
    nationName: z.string(),
    rulerName: z.string(),
    allianceName: z.string()
  }).nullable().refine((val) => val !== null && val !== undefined, {
    message: 'Cannot create a transaction without an assigned account'
  }),
  adjustmentType: z.string().min(1, 'Every manual transaction must be either credit or debt'),
  reason: z.string().optional(),
  classification: z.number().nullable().refine((val) => val !== null && val !== undefined, {
    message: 'Every transaction must be given a classification'
  }),
  rate: z.union([z.number(), z.string()]).refine((val) => {
    if (typeof val === 'string') {
      return val !== '' && !isNaN(Number(val))
    }
    return true
  }, {
    message: 'Please define a valid rate for the transaction credits'
  }),
  cashMovedCashCredit: z.number().optional(),
  cashMovedTechCredit: z.number().optional(),
  techMovedCashCredit: z.number().optional(),
  techMovedTechCredit: z.number().optional()
})

const selectedAccount = computed({
  get() {
    if (state.nation === undefined || state.nation === null) {
      return undefined
    }
    return props.accounts.find(account => account.nationId === state.nation!.nationId)
  },
  set(val: Account | undefined) {
    if (val === undefined || val === null) {
      state.nation = null
    } else {
      state.nation = {
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
  if (state.nation) {
    return state.nation
  } else {
    return {} as NationSimplified
  }
})

const isInUpdateState = computed(() => {
  return state !== undefined && state.id !== undefined
})

watch(() => state.adjustmentType, () => {
  recalculateCredits()
})

watch(() => state.rate, () => {
  recalculateCredits()
})

const handleClose = () => {
  emit("close")
}

const submitForm = () => {
  submitButton.value?.click()
}

const onSubmit = async () => {
  buttonIsLoading.value = true

  try {
    const payload = {
      aidId: null,
      sendingNationId: state.adjustmentType === ADJUSTMENT_TYPES.credit ? state.nation!.nationId : null,
      receivingNationId: state.adjustmentType === ADJUSTMENT_TYPES.debt ? state.nation!.nationId : null,
      reasonOverride: state.reason,
      lu: selectedAccount.value!.role,
      classification: parseInt(state.classification as any),
      rate: parseInt(state.rate as any),
      cashMovedCashCredit: state.cashMovedCashCredit,
      cashMovedTechCredit: state.cashMovedTechCredit,
      techMovedCashCredit: state.techMovedCashCredit,
      techMovedTechCredit: state.techMovedTechCredit
    }

    if (state.id !== undefined) {
      await adminTransactionsStore.updateTransaction({ ...payload, id: state.id })
    } else {
      await adminTransactionsStore.createTransaction(payload)
    }

    toast.add({ color: 'success', title: 'Success', description: 'Transaction saved successfully!', duration: 3000 })
    emit('close')
  } catch (e) {
    console.error(e)
    toast.add({ color: 'error', title: 'Error', description: ERROR_MESSAGES.default, duration: 3000 })
  }

  buttonIsLoading.value = false
}

const recalculateCredits = () => {
  if (state.nation === undefined || state.nation === null) {
    zeroOutCredits()
    return
  }

  if (selectedAccount.value === undefined) {
    zeroOutCredits()
    return
  }

  const rate = parseInt(state.rate as any)
  if (isNaN(rate) || rate === 0) {
    zeroOutCredits()
  } else if (state.adjustmentType === ADJUSTMENT_TYPES.credit) {
    switch (selectedAccount.value.role) {
      case "B":
      case "W":
        updateCredits(rate, rate / 9 * 100, 0, 0)
        break
      case "S":
      case "N":
      case "Q":
        updateCredits(0, 0, rate, rate / 9 * 100)
        break
      default:
        zeroOutCredits()
        break
    }
  } else if (state.adjustmentType === ADJUSTMENT_TYPES.debt) {
    switch (selectedAccount.value.role) {
      case "B":
      case "W":
        updateCredits(0, 0, rate, rate / 9 * 100)
        break
      case "S":
      case "N":
      case "Q":
        updateCredits(rate, rate / 9 * 100, 0, 0)
        break
      default:
        zeroOutCredits()
        break
    }
  } else {
    zeroOutCredits()
  }
}

const updateCredits = (cc: number, ct: number, tc: number, tt: number) => {
  state.cashMovedCashCredit = cc
  state.cashMovedTechCredit = ct
  state.techMovedCashCredit = tc
  state.techMovedTechCredit = tt
}

const zeroOutCredits = () => {
  updateCredits(0, 0, 0, 0)
}
</script>
