<template>
  <UModal :close="{ onClick: () => emit('close', false) }">

    <template #header>
      <h3 v-if="isInUpdateState" class="text-lg font-semibold text-gray-900 dark:text-white">
        {{ state.rulerName }}'s Account
      </h3>
      <h3 v-else class="text-lg font-semibold text-gray-900 dark:text-white">
        New Account
      </h3>
    </template>

    <template #body>
      <UForm :schema="schema" :state="state" @submit="onSubmit" class="space-y-4">
        <!-- Account Data Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Account Data</h2>

          <fieldset :disabled="isInUpdateState" class="space-y-4 px-2">
            <UFormField label="Nation ID" name="nationId">
              <UInput v-model="nationIdInput" placeholder="The account's NationID" class="w-full" />
            </UFormField>
          </fieldset>

          <fieldset disabled class="space-y-4 mt-4 px-2">
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormField label="Nation Name">
                <UInput v-model="state.nationName" class="w-full" />
              </UFormField>
              <UFormField label="Ruler Name">
                <UInput v-model="state.rulerName" class="w-full" />
              </UFormField>
            </div>
            <UFormField label="Alliance Name">
              <UInput v-model="state.allianceName" class="w-full" />
            </UFormField>
          </fieldset>
        </div>

        <!-- Form Fields Section -->
        <div v-if="isInUpdateState || foundProspectAccount">
          <div class="space-y-4 px-2">
            <UFormField label="Role" name="role">
              <USelectMenu
                v-model="state.role"
                :items="roles"
                value-key="code"
                label-key="description"
                placeholder="Select a role"
                class="w-full"
              />
            </UFormField>

            <UFormField label="Secret Code" name="uniqueCode">
              <div class="flex gap-2">
                <UInput v-model="state.uniqueCode" class="flex-1" />
                <UButton label="Generate" @click="generateSecretCode()" color="neutral" />
              </div>
            </UFormField>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <UFormField label="Discord Username">
                <UInput
                  v-model="state.discord"
                  placeholder="e.g. username or username#1234"
                  class="w-full"
                />
              </UFormField>
              <UFormField label="Discord User ID">
                <UInput
                  v-model="state.discordUniqueId"
                  placeholder="e.g. 318737615257993226"
                  class="w-full"
                />
              </UFormField>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <UFormField label="Has Foreign Ministry?" name="hasForeignMinistry">
                <URadioGroup
                  v-model="hasForeignMinistryInput"
                  :items="radioGroupYesNo"
                  orientation="horizontal"
                />
              </UFormField>

              <UFormField label="Has Federal Aid Commission?" name="hasFederalAidCommission">
                <URadioGroup
                  v-model="hasFederalAidCommissionInput"
                  :items="radioGroupYesNo"
                  orientation="horizontal"
                />
              </UFormField>

              <UFormField label="Has Disaster Relief Agency?" name="hasDisasterReliefAgency">
                <URadioGroup
                  v-model="hasDisasterReliefAgencyInput"
                  :items="radioGroupYesNo"
                  orientation="horizontal"
                />
              </UFormField>
            </div>
          </div>

          <!-- Transactional Data Section -->
          <div v-if="isInUpdateState" class="my-6">
            <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">Transactional Data</h2>
            <fieldset disabled class="space-y-4 px-2">
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                <UFormField label="Slots Free">
                  <UInput :model-value="state.slotsFull - state.slotsUsed" class="w-full" />
                </UFormField>
                <UFormField label="Slots Full">
                  <UInput :model-value="state.slotsFull" class="w-full" />
                </UFormField>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <UFormField label="Credit">
                  <UInput :model-value="state.credit" class="w-full" />
                </UFormField>
                <UFormField label="Debt">
                  <UInput :model-value="state.debt" class="w-full" />
                </UFormField>
                <UFormField label="Balance">
                  <UInput :model-value="null" class="w-full" />
                </UFormField>
              </div>

              <h3 class="text-md text-gray-900 dark:text-white mb-4">Cash Credit Totals</h3>
              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <UFormField label="Cash Sent">
                  <UInput :model-value="state.cashSentCashCredit" class="w-full" />
                </UFormField>
                <UFormField label="Cash Rec'd">
                  <UInput :model-value="state.cashReceivedCashCredit" class="w-full" />
                </UFormField>
                <UFormField label="Tech Sent">
                  <UInput :model-value="state.techSentCashCredit" class="w-full" />
                </UFormField>
                <UFormField label="Tech Rec'd">
                  <UInput :model-value="state.techReceivedCashCredit" class="w-full" />
                </UFormField>
              </div>

              <h3 class="text-md text-gray-900 dark:text-white mb-4">Tech Credit Totals</h3>
              <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <UFormField label="Cash Sent">
                  <UInput :model-value="state.cashSentTechCredit" class="w-full" />
                </UFormField>
                <UFormField label="Cash Rec'd">
                  <UInput :model-value="state.cashReceivedTechCredit" class="w-full" />
                </UFormField>
                <UFormField label="Tech Sent">
                  <UInput :model-value="state.techSentTechCredit" class="w-full" />
                </UFormField>
                <UFormField label="Tech Rec'd">
                  <UInput :model-value="state.techReceivedTechCredit" class="w-full" />
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
        <UButton label="Cancel" color="neutral" @click="handleClose" />
        <UButton
          v-if="isInUpdateState"
          label="Save"
          color="success"
          :loading="buttonIsLoading"
          @click="submitForm()"
        />
        <UButton
          v-else-if="foundProspectAccount"
          label="Create"
          color="success"
          :loading="buttonIsLoading"
          @click="submitForm()"
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
  </UModal>
</template>

<script setup lang="ts">
import { clone, z } from 'zod'
import { useAdminAccountsStore, type AdminAccount } from '~/stores/adminAccounts'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { accountRoles, type AccountRole } from '~/infrastructure/dataLists'

interface Props {
  account?: AdminAccount
}

const props = withDefaults(defineProps<Props>(), {
  account: () => ({} as AdminAccount)
})

const emit = defineEmits<{ close: [boolean] }>()

const config = useRuntimeConfig()
const toast = useToast()
const adminAccountsStore = useAdminAccountsStore()

const buttonIsLoading = ref(false)
const submitButton = ref<HTMLButtonElement>()
const roles = accountRoles
const radioGroupYesNo: Array<{ value: string, label: string }> = [
  { value: 'Y', label: 'Yes' },
  { value: 'N', label: 'No' }
]

// Define the form state
const state = reactive<AdminAccount>(props.account)

// Define the Zod schema - only validates when creating
const schema = z.object({
  nationId: z.number().nullable(),
  role: z.string().optional(),
  uniqueCode: z.string().min(1, 'Every account must have a unique code provided').optional(),
  hasForeignMinistry: z.boolean().nullable(),
  hasFederalAidCommission: z.boolean().nullable(),
  hasDisasterReliefAgency: z.boolean().nullable(),
  discord: z.string().optional(),
  discordUniqueId: z.number().optional(),
  nationName: z.string().optional(),
  rulerName: z.string().optional(),
  allianceName: z.string().optional()
}).superRefine((data, ctx) => {
  // Only validate these fields when creating (foundProspectAccount)
  if (data.rulerName && !data.nationId) {
    if (!data.role) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Every account must have a role that they are enrolled as',
        path: ['role']
      })
    }
    if (data.hasForeignMinistry === null || data.hasForeignMinistry === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Must indicate if the nation has the FM improvement or not',
        path: ['hasForeignMinistry']
      })
    }
    if (data.hasFederalAidCommission === null || data.hasFederalAidCommission === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Must indicate if the nation has the FAC wonder or not',
        path: ['hasFederalAidCommission']
      })
    }
    if (data.hasDisasterReliefAgency === null || data.hasDisasterReliefAgency === undefined) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: 'Must indicate if the nation has the DRA wonder or not',
        path: ['hasDisasterReliefAgency']
      })
    }
  }
})

const nationIdInput = computed({
  get() {
    return state.nationId?.toString() ?? ''
  },
  set(val: string) {
    if (val !== "" && !isNaN(parseInt(val))) {
      state.nationId = parseInt(val)
    } else {
      state.nationId = null
    }
  }
})

const roleSelection = computed({
  get() {
    return accountRoles.find(role => role.code === state.role)
  },
  set(val: AccountRole) {
    state.role = val?.code
  }
})

const hasForeignMinistryInput = computed({
  get() {
    return (state.hasForeignMinistry ?? false) ? 'Y' : 'N'
  },
  set(val: string) {
    if (val === "Y" || val === "N") {
      state.hasForeignMinistry = val === "Y"
    } else {
      state.hasForeignMinistry = null
    }
  }
})

const hasDisasterReliefAgencyInput = computed({
  get() {
    return (state.hasDisasterReliefAgency ?? false) ? 'Y' : 'N'
  },
  set(val: string) {
    if (val === "Y" || val === "N") {
      state.hasDisasterReliefAgency = val === "Y"
    } else {
      state.hasDisasterReliefAgency = null
    }
  }
})

const hasFederalAidCommissionInput = computed({
  get() {
    return (state.hasFederalAidCommission ?? false) ? 'Y' : 'N'
  },
  set(val: string) {
    if (val === "Y" || val === "N") {
      state.hasFederalAidCommission = val === "Y"
    } else {
      state.hasFederalAidCommission = null
    }
  }
})

const isInUpdateState = computed(() => {
  return state !== undefined && state.id !== undefined
})

const foundProspectAccount = computed(() => {
  return state !== undefined && state.rulerName !== undefined
})

const handleClose = () => {
  emit("close", false);
}

const submitForm = () => {
  submitButton.value?.click()
}

const findProspectAccount = async () => {
  buttonIsLoading.value = true
  try {
    const response = await $fetch(`${config.public.apiUrl}/accounts/prospect?nationId=${state.nationId}`)
    const prospectAccount = response as any

    Object.assign(state, {
      ...state,
      nationName: prospectAccount.nationName,
      rulerName: prospectAccount.rulerName,
      allianceName: prospectAccount.allianceName
    })
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

const onSubmit = async () => {
  if (isInUpdateState.value) {
    await update()
  } else {
    await create()
  }
}

const update = async () => {
  buttonIsLoading.value = true
  try {
    await adminAccountsStore.updateAccount(state as any)
    toast.add({ color: 'success', title: 'Success', description: 'Account updated successfully!', duration: 3000 })
    handleClose();
  } catch (e) {
    console.error(e)
    toast.add({ color: 'error', title: 'Error', description: ERROR_MESSAGES.default, duration: 3000 })
  }

  buttonIsLoading.value = false
}

const create = async () => {
  buttonIsLoading.value = true
  try {
    await adminAccountsStore.createAccount(state)
    toast.add({ color: 'success', title: 'Success', description: 'Account created successfully!', duration: 3000 })
    handleClose();
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
  state.uniqueCode = newCode
}
</script>
