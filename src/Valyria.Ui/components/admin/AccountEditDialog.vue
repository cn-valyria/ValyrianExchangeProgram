<template>
  <Dialog :visible="show" modal :style="{ width: '50rem' }" @update:visible="$emit('close')">
    <template #header>
      <h3 v-if="isInUpdateState">{{ accountBeingEdited.rulerName }}'s Account</h3>
      <h3 v-else>New Account</h3>
    </template>

    <div class="content">
      <h2>Account Data</h2>
      <fieldset :disabled="isInUpdateState">
        <div class="field">
          <label class="label">Nation ID</label>
          <div class="control">
            <InputText v-model="nationIdInput" placeholder="The account's NationID" class="w-full" />
          </div>
        </div>
      </fieldset>
      <fieldset disabled>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Nation Name</label>
              <div class="control">
                <InputText v-model="accountBeingEdited.nationName" class="w-full" />
              </div>
            </div>
            <div class="field">
              <label class="label">Ruler Name</label>
              <div class="control">
                <InputText v-model="accountBeingEdited.rulerName" class="w-full" />
              </div>
            </div>
          </div>
        </div>
        <div class="field">
          <div class="label">Alliance Name</div>
          <div class="control">
            <InputText v-model="accountBeingEdited.allianceName" class="w-full" />
          </div>
        </div>
      </fieldset>
      <fieldset v-if="isInUpdateState || foundProspectAccount">
        <div class="field">
          <label class="label">Role</label>
          <div class="control is-expanded">
            <Dropdown
              v-model="accountBeingEdited.role"
              :options="roles"
              optionLabel="description"
              optionValue="code"
              placeholder="Select a role"
              :class="{ 'p-invalid': propertyHasValidationError('role') }"
              class="w-full"
            />
          </div>
          <small v-if="propertyHasValidationError('role')" class="p-error">
            Every account must have a role that they are enrolled as
          </small>
        </div>
        <div class="field">
          <label class="label">Secret Code</label>
          <div class="p-inputgroup">
            <InputText
              v-model="accountBeingEdited.uniqueCode"
              :class="{ 'p-invalid': propertyHasValidationError('uniqueCode') }"
            />
            <Button label="Generate" @click="generateSecretCode()" />
          </div>
          <small v-if="propertyHasValidationError('uniqueCode')" class="p-error">
            Every account must have a unique code provided
          </small>
        </div>
        <div class="field is-horizontal">
          <div class="field-body">
            <div class="field">
              <label class="label">Discord Username</label>
              <div class="control">
                <InputText
                  v-model="accountBeingEdited.discord"
                  placeholder="e.g. username or username#1234"
                  class="w-full"
                />
              </div>
            </div>
            <div class="field">
              <label class="label">Discord User ID</label>
              <div class="control">
                <InputText
                  v-model="accountBeingEdited.discordUniqueId"
                  placeholder="e.g. 318737615257993226"
                  class="w-full"
                />
              </div>
            </div>
          </div>
        </div>
        <div class="field">
          <label class="label">Has Foreign Ministry?</label>
          <div class="flex gap-3">
            <div class="flex align-items-center">
              <RadioButton v-model="accountBeingEdited.hasForeignMinistry" inputId="fm-yes" :value="true" />
              <label for="fm-yes" class="ml-2">Yes</label>
            </div>
            <div class="flex align-items-center">
              <RadioButton v-model="accountBeingEdited.hasForeignMinistry" inputId="fm-no" :value="false" />
              <label for="fm-no" class="ml-2">No</label>
            </div>
          </div>
          <small v-if="propertyHasValidationError('hasForeignMinistry')" class="p-error">
            Must indicate if the nation has the FM improvement or not
          </small>
        </div>
        <div class="field">
          <label class="label">Has Federal Aid Commission?</label>
          <div class="flex gap-3">
            <div class="flex align-items-center">
              <RadioButton v-model="accountBeingEdited.hasFederalAidCommission" inputId="fac-yes" :value="true" />
              <label for="fac-yes" class="ml-2">Yes</label>
            </div>
            <div class="flex align-items-center">
              <RadioButton v-model="accountBeingEdited.hasFederalAidCommission" inputId="fac-no" :value="false" />
              <label for="fac-no" class="ml-2">No</label>
            </div>
          </div>
          <small v-if="propertyHasValidationError('hasFederalAidCommission')" class="p-error">
            Must indicate if the nation has the FAC wonder or not
          </small>
        </div>
        <div class="field">
          <label class="label">Has Disaster Relief Agency?</label>
          <div class="flex gap-3">
            <div class="flex align-items-center">
              <RadioButton v-model="accountBeingEdited.hasDisasterReliefAgency" inputId="dra-yes" :value="true" />
              <label for="dra-yes" class="ml-2">Yes</label>
            </div>
            <div class="flex align-items-center">
              <RadioButton v-model="accountBeingEdited.hasDisasterReliefAgency" inputId="dra-no" :value="false" />
              <label for="dra-no" class="ml-2">No</label>
            </div>
          </div>
          <small v-if="propertyHasValidationError('hasDisasterReliefAgency')" class="p-error">
            Must indicate if the nation has the DRA wonder or not
          </small>
        </div>
        <div v-if="isInUpdateState">
          <h2>Transactional Data</h2>
          <fieldset disabled>
            <div class="field is-horizontal">
              <div class="field-body">
                <div class="field">
                  <label class="label">Slots Free</label>
                  <div class="control">
                    <InputText
                      :modelValue="accountBeingEdited.slotsFull - accountBeingEdited.slotsUsed"
                      class="w-full"
                    />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Slots Full</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.slotsFull" class="w-full" />
                  </div>
                </div>
              </div>
            </div>
            <div class="field is-horizontal">
              <div class="field-body">
                <div class="field">
                  <label class="label">Credit</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.credit" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Debt</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.debt" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Balance</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.balance" class="w-full" />
                  </div>
                </div>
              </div>
            </div>
            <div class="field is-horizontal">
              <div class="field-body">
                <div class="field">
                  <label class="label">Cash Sent (Cash)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.cashSentCashCredit" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Cash Rec'd (Cash)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.cashReceivedCashCredit" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Tech Sent (Cash)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.techSentCashCredit" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Tech Rec'd (Cash)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.techReceivedCashCredit" class="w-full" />
                  </div>
                </div>
              </div>
            </div>
            <div class="field is-horizontal">
              <div class="field-body">
                <div class="field">
                  <label class="label">Cash Sent (Tech)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.cashSentTechCredit" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Cash Rec'd (Tech)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.cashReceivedTechCredit" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Tech Sent (Tech)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.techSentTechCredit" class="w-full" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Tech Rec'd (Tech)</label>
                  <div class="control">
                    <InputText :modelValue="accountBeingEdited.techReceivedTechCredit" class="w-full" />
                  </div>
                </div>
              </div>
            </div>
          </fieldset>
        </div>
      </fieldset>
    </div>

    <template #footer>
      <Button
        v-if="isInUpdateState"
        label="Save"
        severity="success"
        :loading="buttonIsLoading"
        @click="update()"
      />
      <Button
        v-else-if="foundProspectAccount"
        label="Create"
        severity="success"
        :loading="buttonIsLoading"
        @click="create()"
      />
      <Button
        v-else
        label="Search"
        severity="success"
        :loading="buttonIsLoading"
        @click="findProspectAccount()"
      />
      <Button label="Cancel" severity="secondary" @click="$emit('close')" />
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { useAdminAccountsStore } from '~/stores/adminAccounts'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { accountRoles } from '~/infrastructure/dataLists'

interface Account {
  id?: number
  nationId: number | string
  nationName?: string
  rulerName?: string
  allianceName?: string
  role?: string
  uniqueCode?: string
  discord?: string
  discordUniqueId?: string
  hasForeignMinistry?: boolean
  hasFederalAidCommission?: boolean
  hasDisasterReliefAgency?: boolean
  slotsFull?: number
  slotsUsed?: number
  credit?: number
  debt?: number
  balance?: number
  cashSentCashCredit?: number
  cashReceivedCashCredit?: number
  techSentCashCredit?: number
  techReceivedCashCredit?: number
  cashSentTechCredit?: number
  cashReceivedTechCredit?: number
  techSentTechCredit?: number
  techReceivedTechCredit?: number
}

interface Props {
  show: boolean
  account?: Account
}

interface Emits {
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  account: () => ({} as Account)
})

const emit = defineEmits<Emits>()

const config = useRuntimeConfig()
const toast = useToast()
const adminAccountsStore = useAdminAccountsStore()

const buttonIsLoading = ref(false)
const accountBeingEdited = ref<Account>({} as Account)
const roles = accountRoles
const validationErrors = ref<Array<{ prop: string }>>([])

const nationIdInput = computed({
  get() {
    return accountBeingEdited.value.nationId?.toString() ?? ''
  },
  set(val: string) {
    if (val !== "" && !isNaN(parseInt(val))) {
      accountBeingEdited.value.nationId = parseInt(val)
    } else {
      accountBeingEdited.value.nationId = ""
    }
  }
})

const isInUpdateState = computed(() => {
  return accountBeingEdited.value !== undefined && accountBeingEdited.value.id !== undefined
})

const foundProspectAccount = computed(() => {
  return accountBeingEdited.value !== undefined && accountBeingEdited.value.rulerName !== undefined
})

watch(() => props.account, (val) => {
  accountBeingEdited.value = { ...val }
})

const findProspectAccount = async () => {
  buttonIsLoading.value = true
  try {
    const response = await $fetch(`${config.public.apiUrl}/accounts/prospect?nationId=${accountBeingEdited.value.nationId}`)
    const prospectAccount = response as any

    accountBeingEdited.value = {
      ...accountBeingEdited.value,
      nationName: prospectAccount.nationName,
      rulerName: prospectAccount.rulerName,
      allianceName: prospectAccount.allianceName
    }
  } catch (e: any) {
    console.error(e)
    if (e.response) {
      toast.add({ severity: 'error', summary: 'Error', detail: e.response.data, life: 3000 })
    } else {
      toast.add({ severity: 'error', summary: 'Error', detail: "VEP encountered an error while searching for the above nation ID. Please try again, or contact @lilweirdward if the error persists.", life: 3000 })
    }
  }

  buttonIsLoading.value = false
}

const update = async () => {
  buttonIsLoading.value = true
  try {
    await adminAccountsStore.updateAccount(accountBeingEdited.value as any)
    toast.add({ severity: 'success', summary: 'Success', detail: 'Account updated successfully!', life: 3000 })
    emit('close')
  } catch (e) {
    console.error(e)
    toast.add({ severity: 'error', summary: 'Error', detail: ERROR_MESSAGES.default, life: 3000 })
  }

  buttonIsLoading.value = false
}

const create = async () => {
  if (!accountIsValid()) {
    console.error("Account form is invalid and will not be submitted to the API")
    return
  }

  buttonIsLoading.value = true
  try {
    await adminAccountsStore.createAccount(accountBeingEdited.value)
    toast.add({ severity: 'success', summary: 'Success', detail: 'Account created successfully!', life: 3000 })
    emit('close')
  } catch (e) {
    console.error(e)
    toast.add({ severity: 'error', summary: 'Error', detail: ERROR_MESSAGES.default, life: 3000 })
  }

  buttonIsLoading.value = false
}

const randomIntFromInterval = (min: number, max: number) => {
  return Math.floor(Math.random() * (max - min + 1) + min)
}

const generateSecretCode = () => {
  const newCode = String(randomIntFromInterval(1, 99999)).padStart(5, "0")
  accountBeingEdited.value.uniqueCode = newCode
}

const accountIsValid = () => {
  validateProperty("role")
  validateProperty("uniqueCode", (val: any) => val === undefined || val === "")
  validateProperty("hasForeignMinistry")
  validateProperty("hasFederalAidCommission")
  validateProperty("hasDisasterReliefAgency")

  return validationErrors.value.length === 0
}

const validateProperty = (prop: string, propIsInvalidFunc: (val: any) => boolean = (val: any) => val === undefined) => {
  const validationIndex = validationErrors.value.findIndex(e => e.prop === prop)
  const propIsInvalid = propIsInvalidFunc((accountBeingEdited.value as any)[prop])
  const propAlreadyHasError = validationIndex > -1
  if (propIsInvalid && !propAlreadyHasError) {
    validationErrors.value.push({ prop })
  } else if (!propIsInvalid && propAlreadyHasError) {
    validationErrors.value.splice(validationIndex, 1)
  }
}

const propertyHasValidationError = (prop: string) => {
  return validationErrors.value.some(e => e.prop === prop)
}
</script>
