<template>
  <Dialog :visible="show" modal :style="{ width: '50rem' }" @update:visible="handleClose()">
    <template #header>
      <h3>{{ transactionBeingEdited.id !== undefined ? "Update" : "New"}} Manual Transaction</h3>
    </template>

    <div class="content">
      <h2>Manual Data</h2>
      <fieldset :disabled="isInUpdateState">
        <div class="field">
          <label class="label">Account</label>
          <AutoComplete
            v-model="accountTextbox"
            :suggestions="filteredAccounts"
            field="rulerName"
            placeholder="No account selected"
            :class="{ 'p-invalid': validator.propertyHasValidationError('nation') }"
            @complete="searchAccounts"
            @item-select="onAccountSelect"
            class="w-full"
          />
          <small v-if="validator.propertyHasValidationError('nation')" class="p-error">
            Cannot create a transaction without an assigned account
          </small>
        </div>
      </fieldset>
      <fieldset disabled>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Nation Name</label>
              <div class="control">
                <InputText v-model="transactionNation.nationName" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Ruler Name</label>
              <div class="control">
                <InputText v-model="transactionNation.rulerName" class="w-full" />
              </div>
            </div>
          </div>
        </div>
        <div class="field">
          <div class="label">Alliance Name</div>
          <div class="control">
            <InputText v-model="transactionNation.allianceName" class="w-full" />
          </div>
        </div>
      </fieldset>
      <fieldset>
        <div class="field">
          <label class="label">Adjustment Type</label>
          <div class="control is-expanded">
            <Dropdown
              v-model="transactionBeingEdited.adjustmentType"
              :options="adjustmentTypes"
              placeholder="Select adjustment type"
              :class="{ 'p-invalid': validator.propertyHasValidationError('adjustmentType') }"
              class="w-full"
            />
            <small v-if="validator.propertyHasValidationError('adjustmentType')" class="p-error">
              Every manual transaction must be either credit or debt
            </small>
          </div>
        </div>
      </fieldset>
      <div class="field">
        <label class="label">Reason</label>
        <div class="control">
          <InputText v-model="transactionBeingEdited.reason" class="w-full" />
        </div>
      </div>
      <h2>Transaction Metadata</h2>
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
                placeholder="Select classification"
                :class="{ 'p-invalid': validator.propertyHasValidationError('classification') }"
                class="w-full"
              />
              <small v-if="validator.propertyHasValidationError('classification')" class="p-error">
                Every transaction must be given a classification
              </small>
            </div>
          </div>
          <div class="field">
            <label class="label">Rate</label>
            <div class="control">
              <InputNumber
                v-model="transactionBeingEdited.rate"
                :class="{ 'p-invalid': validator.propertyHasValidationError('rate') }"
                class="w-full"
              />
            </div>
            <small v-if="validator.propertyHasValidationError('rate')" class="p-error">
              Please define a valid rate for the transaction credits
            </small>
          </div>
        </div>
      </div>
      <fieldset disabled>
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
      <Button label="Save" severity="success" :loading="buttonIsLoading" @click="save()" />
      <Button label="Cancel" severity="secondary" @click="handleClose()" />
    </template>
  </Dialog>
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

const buttonIsLoading = ref(false)
const transactionBeingEdited = ref<Transaction>({} as Transaction)
const accountTextbox = ref("")
const filteredAccounts = ref<Account[]>([])
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

const searchAccounts = (event: any) => {
  const query = event.query.toLowerCase()
  filteredAccounts.value = props.accounts.filter(account =>
    account.rulerName.toLowerCase().includes(query)
  )
}

const onAccountSelect = (event: any) => {
  selectedAccount.value = event.value
}

watch(() => props.transaction, (val) => {
  transactionBeingEdited.value = { ...val }
  validator.value = new Validator(transactionBeingEdited.value)

  // Update the account textbox if the loaded transaction already has an account defined
  if (selectedAccount.value !== undefined) {
    accountTextbox.value = selectedAccount.value.rulerName
  }

  // Recalculate the credits when the transaction changes, since they're always readonly
  recalculateCredits()
})

watch(() => transactionBeingEdited.value.adjustmentType, () => {
  recalculateCredits()
})

watch(() => transactionBeingEdited.value.rate, () => {
  recalculateCredits()
})

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

const handleClose = () => {
  accountTextbox.value = ""
  emit("close")
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
