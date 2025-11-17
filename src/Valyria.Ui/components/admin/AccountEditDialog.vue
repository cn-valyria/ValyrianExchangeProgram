<template>
  <USlideover v-model="isOpen" @update:model-value="handleClose">
    <UCard class="flex flex-col flex-1">
      <template #header>
        <h3 v-if="isInUpdateState" class="text-lg font-semibold text-gray-900 dark:text-white">
          {{ accountBeingEdited.rulerName }}'s Account
        </h3>
        <h3 v-else class="text-lg font-semibold text-gray-900 dark:text-white">
          New Account
        </h3>
      </template>

      <div class="space-y-6">
        <!-- Account Data Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Account Data</h2>

          <fieldset :disabled="isInUpdateState" class="space-y-4">
            <UFormGroup label="Nation ID" :error="propertyHasValidationError('nationId') ? 'Nation ID is required' : undefined">
              <UInput v-model="nationIdInput" placeholder="The account's NationID" />
            </UFormGroup>
          </fieldset>

          <fieldset disabled class="space-y-4 mt-4">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormGroup label="Nation Name">
                <UInput v-model="accountBeingEdited.nationName" />
              </UFormGroup>
              <UFormGroup label="Ruler Name">
                <UInput v-model="accountBeingEdited.rulerName" />
              </UFormGroup>
            </div>
            <UFormGroup label="Alliance Name">
              <UInput v-model="accountBeingEdited.allianceName" />
            </UFormGroup>
          </fieldset>
        </div>

        <!-- Form Fields Section -->
        <div v-if="isInUpdateState || foundProspectAccount" class="space-y-4">
          <UFormGroup
            label="Role"
            :error="propertyHasValidationError('role') ? 'Every account must have a role that they are enrolled as' : undefined"
          >
            <USelectMenu
              v-model="accountBeingEdited.role"
              :options="roles"
              value-attribute="code"
              option-attribute="description"
              placeholder="Select a role"
            />
          </UFormGroup>

          <UFormGroup
            label="Secret Code"
            :error="propertyHasValidationError('uniqueCode') ? 'Every account must have a unique code provided' : undefined"
          >
            <div class="flex gap-2">
              <UInput v-model="accountBeingEdited.uniqueCode" class="flex-1" />
              <UButton label="Generate" @click="generateSecretCode()" color="neutral" />
            </div>
          </UFormGroup>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <UFormGroup label="Discord Username">
              <UInput
                v-model="accountBeingEdited.discord"
                placeholder="e.g. username or username#1234"
              />
            </UFormGroup>
            <UFormGroup label="Discord User ID">
              <UInput
                v-model="accountBeingEdited.discordUniqueId"
                placeholder="e.g. 318737615257993226"
              />
            </UFormGroup>
          </div>

          <UFormGroup
            label="Has Foreign Ministry?"
            :error="propertyHasValidationError('hasForeignMinistry') ? 'Must indicate if the nation has the FM improvement or not' : undefined"
          >
            <URadioGroup
              v-model="hasForeignMinistryInput"
              :items="[
                { value: 'Y', label: 'Yes' },
                { value: 'N', label: 'No' }
              ]"
            />
          </UFormGroup>

          <UFormGroup
            label="Has Federal Aid Commission?"
            :error="propertyHasValidationError('hasFederalAidCommission') ? 'Must indicate if the nation has the FAC wonder or not' : undefined"
          >
            <URadioGroup
              v-model="hasFederalAidCommissionInput"
              :items="[
                { value: 'Y', label: 'Yes' },
                { value: 'N', label: 'No' }
              ]"
            />
          </UFormGroup>

          <UFormGroup
            label="Has Disaster Relief Agency?"
            :error="propertyHasValidationError('hasDisasterReliefAgency') ? 'Must indicate if the nation has the DRA wonder or not' : undefined"
          >
            <URadioGroup
              v-model="hasDisasterReliefAgencyInput"
              :items="[
                { value: 'Y', label: 'Yes' },
                { value: 'N', label: 'No' }
              ]"
            />
          </UFormGroup>

          <!-- Transactional Data Section -->
          <div v-if="isInUpdateState">
            <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Transactional Data</h2>
            <fieldset disabled class="space-y-4">
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <UFormGroup label="Slots Free">
                  <UInput :model-value="accountBeingEdited.slotsFull - accountBeingEdited.slotsUsed" />
                </UFormGroup>
                <UFormGroup label="Slots Full">
                  <UInput :model-value="accountBeingEdited.slotsFull" />
                </UFormGroup>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <UFormGroup label="Credit">
                  <UInput :model-value="accountBeingEdited.credit" />
                </UFormGroup>
                <UFormGroup label="Debt">
                  <UInput :model-value="accountBeingEdited.debt" />
                </UFormGroup>
                <UFormGroup label="Balance">
                  <UInput :model-value="null" />
                </UFormGroup>
              </div>

              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <UFormGroup label="Cash Sent (Cash)">
                  <UInput :model-value="accountBeingEdited.cashSentCashCredit" />
                </UFormGroup>
                <UFormGroup label="Cash Rec'd (Cash)">
                  <UInput :model-value="accountBeingEdited.cashReceivedCashCredit" />
                </UFormGroup>
                <UFormGroup label="Tech Sent (Cash)">
                  <UInput :model-value="accountBeingEdited.techSentCashCredit" />
                </UFormGroup>
                <UFormGroup label="Tech Rec'd (Cash)">
                  <UInput :model-value="accountBeingEdited.techReceivedCashCredit" />
                </UFormGroup>
              </div>

              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <UFormGroup label="Cash Sent (Tech)">
                  <UInput :model-value="accountBeingEdited.cashSentTechCredit" />
                </UFormGroup>
                <UFormGroup label="Cash Rec'd (Tech)">
                  <UInput :model-value="accountBeingEdited.cashReceivedTechCredit" />
                </UFormGroup>
                <UFormGroup label="Tech Sent (Tech)">
                  <UInput :model-value="accountBeingEdited.techSentTechCredit" />
                </UFormGroup>
                <UFormGroup label="Tech Rec'd (Tech)">
                  <UInput :model-value="accountBeingEdited.techReceivedTechCredit" />
                </UFormGroup>
              </div>
            </fieldset>
          </div>
        </div>
      </div>

      <template #footer>
        <div class="flex gap-3 justify-end">
          <UButton label="Cancel" color="neutral" @click="handleClose" />
          <UButton
            v-if="isInUpdateState"
            label="Save"
            color="success"
            :loading="buttonIsLoading"
            @click="update()"
          />
          <UButton
            v-else-if="foundProspectAccount"
            label="Create"
            color="success"
            :loading="buttonIsLoading"
            @click="create()"
          />
          <UButton
            v-else
            label="Search"
            color="primary"
            :loading="buttonIsLoading"
            @click="findProspectAccount()"
          />
        </div>
      </template>
    </UCard>
  </USlideover>
</template>

<script setup lang="ts">
import { useAdminAccountsStore, type AdminAccount } from '~/stores/adminAccounts'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { accountRoles } from '~/infrastructure/dataLists'

interface Props {
  show: boolean
  account?: AdminAccount
}

interface Emits {
  (e: 'close'): void
}

const props = withDefaults(defineProps<Props>(), {
  account: () => ({} as AdminAccount)
})

const emit = defineEmits<Emits>()

const config = useRuntimeConfig()
const toast = useToast()
const adminAccountsStore = useAdminAccountsStore()

const isOpen = ref(false)
const buttonIsLoading = ref(false)
const accountBeingEdited = ref<AdminAccount>({} as AdminAccount)
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
      accountBeingEdited.value.nationId = null;
    }
  }
})

const hasForeignMinistryInput = computed({
  get() {
    return (accountBeingEdited.value.hasForeignMinistry ?? false) ? 'Y' : 'N';
  },
  set(val: string) {
    if (val === "Y" || val === "N") {
      accountBeingEdited.value.hasForeignMinistry = val === "Y";
    } else {
      accountBeingEdited.value.hasForeignMinistry = null;
    }
  }
})

const hasDisasterReliefAgencyInput = computed({
  get() {
    return (accountBeingEdited.value.hasDisasterReliefAgency ?? false) ? 'Y' : 'N';
  },
  set(val: string) {
    if (val === "Y" || val === "N") {
      accountBeingEdited.value.hasDisasterReliefAgency = val === "Y";
    } else {
      accountBeingEdited.value.hasDisasterReliefAgency = null;
    }
  }
})

const hasFederalAidCommissionInput = computed({
  get() {
    return (accountBeingEdited.value.hasFederalAidCommission ?? false) ? 'Y' : 'N';
  },
  set(val: string) {
    if (val === "Y" || val === "N") {
      accountBeingEdited.value.hasFederalAidCommission = val === "Y";
    } else {
      accountBeingEdited.value.hasFederalAidCommission = null;
    }
  }
})

const isInUpdateState = computed(() => {
  return accountBeingEdited.value !== undefined && accountBeingEdited.value.id !== undefined
})

const foundProspectAccount = computed(() => {
  return accountBeingEdited.value !== undefined && accountBeingEdited.value.rulerName !== undefined
})

watch(() => props.show, (val) => {
  isOpen.value = val
})

watch(() => props.account, (val) => {
  accountBeingEdited.value = { ...val }
})

const handleClose = () => {
  emit('close')
}

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
      toast.add({ color: 'error', title: 'Error', description: e.response.data, duration: 3000 })
    } else {
      toast.add({ color: 'error', title: 'Error', description: "VEP encountered an error while searching for the above nation ID. Please try again, or contact @lilweirdward if the error persists.", duration: 3000 })
    }
  }

  buttonIsLoading.value = false
}

const update = async () => {
  buttonIsLoading.value = true
  try {
    await adminAccountsStore.updateAccount(accountBeingEdited.value as any)
    toast.add({ color: 'success', title: 'Success', description: 'Account updated successfully!', duration: 3000 })
    emit('close')
  } catch (e) {
    console.error(e)
    toast.add({ color: 'error', title: 'Error', description: ERROR_MESSAGES.default, duration: 3000 })
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
    toast.add({ color: 'success', title: 'Success', description: 'Account created successfully!', duration: 3000 })
    emit('close')
  } catch (e) {
    console.error(e)
    toast.add({ color: 'error', title: 'Error', description: ERROR_MESSAGES.default, duration: 3000 })
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
