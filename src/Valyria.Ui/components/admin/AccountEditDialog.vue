<template>
  <UModal :close="{ onClick: () => emit('close', false) }">
    <template #header>
      <h3
        v-if="isInUpdateState"
        class="text-lg font-semibold text-gray-900 dark:text-white"
      >
        {{ state.rulerName }}'s Account
      </h3>
      <h3 v-else class="text-lg font-semibold text-gray-900 dark:text-white">
        New Account
      </h3>
    </template>

    <template #body>
      <UForm
        ref="form"
        :schema="schema"
        :state="state"
        class="space-y-4"
        @submit="onSubmit"
        @error="onError"
      >
        <!-- Account Data Section -->
        <div>
          <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">
            Account Data
          </h2>

          <fieldset :disabled="isInUpdateState" class="space-y-4 px-2">
            <UFormField label="Nation ID" name="nationId">
              <UInput
                v-model="nationIdInput"
                placeholder="The account's NationID"
                class="w-full"
              />
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
                <UButton
                  label="Generate"
                  color="neutral"
                  @click="generateSecretCode()"
                />
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
                  v-model="discordUniqueIdInput"
                  placeholder="e.g. 318737615257993226"
                  class="w-full"
                />
              </UFormField>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <UFormField
                label="Has Foreign Ministry?"
                name="hasForeignMinistry"
              >
                <URadioGroup
                  v-model="hasForeignMinistryInput"
                  :items="radioGroupYesNo"
                  orientation="horizontal"
                />
              </UFormField>

              <UFormField
                label="Has Federal Aid Commission?"
                name="hasFederalAidCommission"
              >
                <URadioGroup
                  v-model="hasFederalAidCommissionInput"
                  :items="radioGroupYesNo"
                  orientation="horizontal"
                />
              </UFormField>

              <UFormField
                label="Has Disaster Relief Agency?"
                name="hasDisasterReliefAgency"
              >
                <URadioGroup
                  v-model="hasDisasterReliefAgencyInput"
                  :items="radioGroupYesNo"
                  orientation="horizontal"
                />
              </UFormField>
            </div>
          </div>

          <!-- Slots Section -->
          <div v-if="isInUpdateState" class="my-6">
            <h2 class="text-xl font-bold text-gray-900 dark:text-white mb-4">
              Slots
              <span
                class="text-base font-normal text-gray-500 dark:text-gray-400"
                >({{ state.slotsUsed }} / {{ state.slotsTotal }} in deals)</span
              >
            </h2>
            <AdminAccountSlotsEditor :account-id="state.id" />
          </div>
        </div>
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
          @click="form.submit()"
        />
        <UButton
          v-else-if="foundProspectAccount"
          label="Create"
          color="success"
          :loading="buttonIsLoading"
          @click="form.submit()"
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
import { z } from 'zod'
import type { FormErrorEvent } from '#ui/types'
import {
  useAdminAccountsStore,
  type AdminAccount,
} from '~/stores/adminAccounts'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { accountRoles } from '~/infrastructure/dataLists'

interface Props {
  account?: AdminAccount
}

const props = withDefaults(defineProps<Props>(), {
  account: () => ({}) as AdminAccount,
})

const emit = defineEmits<{ close: [boolean] }>()

const toast = useToast()
const adminAccountsStore = useAdminAccountsStore()
const { apiFetch } = useApiClient()

const buttonIsLoading = ref(false)
const form = ref()
const roles = accountRoles
const radioGroupYesNo: Array<{ value: string; label: string }> = [
  { value: 'Y', label: 'Yes' },
  { value: 'N', label: 'No' },
]

// Edit a copy, so cancelling leaves the account in the accounts list untouched. New accounts start
// with the improvements set to No, matching what the radio buttons show
const state = reactive<AdminAccount>({
  ...props.account,
  hasForeignMinistry: props.account.hasForeignMinistry ?? false,
  hasFederalAidCommission: props.account.hasFederalAidCommission ?? false,
  hasDisasterReliefAgency: props.account.hasDisasterReliefAgency ?? false,
})

// Define the Zod schema - only validates when creating
const schema = z
  .object({
    nationId: z.number().nullable(),
    role: z.string().nullable().optional(),
    uniqueCode: z
      .string()
      .min(1, 'Every account must have a unique code provided')
      .nullable()
      .optional(),
    hasForeignMinistry: z.boolean().nullable(),
    hasFederalAidCommission: z.boolean().nullable(),
    hasDisasterReliefAgency: z.boolean().nullable(),
    discord: z.string().nullable().optional(),
    discordUniqueId: z.bigint().nullable().optional(),
    nationName: z.string().nullable().optional(),
    rulerName: z.string().nullable().optional(),
    allianceName: z.string().nullable().optional(),
  })
  .superRefine((data, ctx) => {
    // Only validate these fields when creating (foundProspectAccount)
    if (data.rulerName && !data.nationId) {
      if (!data.role) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Every account must have a role that they are enrolled as',
          path: ['role'],
        })
      }
      if (
        data.hasForeignMinistry === null ||
        data.hasForeignMinistry === undefined
      ) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Must indicate if the nation has the FM improvement or not',
          path: ['hasForeignMinistry'],
        })
      }
      if (
        data.hasFederalAidCommission === null ||
        data.hasFederalAidCommission === undefined
      ) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Must indicate if the nation has the FAC wonder or not',
          path: ['hasFederalAidCommission'],
        })
      }
      if (
        data.hasDisasterReliefAgency === null ||
        data.hasDisasterReliefAgency === undefined
      ) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Must indicate if the nation has the DRA wonder or not',
          path: ['hasDisasterReliefAgency'],
        })
      }
    }
  })

const nationIdInput = computed({
  get() {
    return state.nationId?.toString() ?? ''
  },
  set(val: string) {
    if (val !== '' && !isNaN(parseInt(val))) {
      state.nationId = parseInt(val)
    } else {
      state.nationId = null
    }
  },
})

const hasForeignMinistryInput = computed({
  get() {
    return (state.hasForeignMinistry ?? false) ? 'Y' : 'N'
  },
  set(val: string) {
    if (val === 'Y' || val === 'N') {
      state.hasForeignMinistry = val === 'Y'
    } else {
      state.hasForeignMinistry = null
    }
  },
})

const hasDisasterReliefAgencyInput = computed({
  get() {
    return (state.hasDisasterReliefAgency ?? false) ? 'Y' : 'N'
  },
  set(val: string) {
    if (val === 'Y' || val === 'N') {
      state.hasDisasterReliefAgency = val === 'Y'
    } else {
      state.hasDisasterReliefAgency = null
    }
  },
})

const hasFederalAidCommissionInput = computed({
  get() {
    return (state.hasFederalAidCommission ?? false) ? 'Y' : 'N'
  },
  set(val: string) {
    if (val === 'Y' || val === 'N') {
      state.hasFederalAidCommission = val === 'Y'
    } else {
      state.hasFederalAidCommission = null
    }
  },
})

const discordUniqueIdInput = computed({
  get() {
    return state.discordUniqueId?.toString() ?? ''
  },
  set(val: string) {
    if (val !== '' && !isNaN(parseInt(val))) {
      state.discordUniqueId = BigInt(val)
    } else {
      state.discordUniqueId = null
    }
  },
})

const isInUpdateState = computed(() => {
  return state !== undefined && state.id !== undefined
})

const foundProspectAccount = computed(() => {
  return state !== undefined && state.rulerName !== undefined
})

const handleClose = () => {
  emit('close', false)
}

interface ProspectAccount {
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
}

const prospectSearchErrors: Record<number, string> = {
  404: 'No nation was found with that nation ID.',
  409: 'That nation already has a VEP account.',
}

const findProspectAccount = async () => {
  buttonIsLoading.value = true
  const { data, error } = await apiFetch<ProspectAccount>(
    `/api/accounts/prospect?nationId=${state.nationId}`,
  )
  buttonIsLoading.value = false

  if (error.value || !data.value) {
    console.error(error.value)
    toast.add({
      color: 'error',
      title: 'Error',
      description:
        prospectSearchErrors[error.value?.statusCode] ??
        'VEP encountered an error while searching for the above nation ID. Please try again, or contact @lilweirdward if the error persists.',
      duration: 3000,
    })
    return
  }

  Object.assign(state, {
    nationName: data.value.nationName,
    rulerName: data.value.rulerName,
    allianceName: data.value.allianceName,
  })
}

const onSubmit = async () => {
  if (isInUpdateState.value) {
    await update()
  } else {
    await create()
  }
}

const onError = async (event: FormErrorEvent) => {
  if (event?.errors?.length > 0) {
    toast.add({
      description: `Form validation failed: ${event.errors.map((error) => error.message).join(', ')}`,
      title: 'Validation Error',
      color: 'error',
      duration: 3000,
    })
  }
}

const update = async () => {
  buttonIsLoading.value = true
  try {
    // Prepare the payload, ensuring null values are sent for empty fields
    const payload = {
      ...state,
      discordUniqueId: state.discordUniqueId ?? null,
      discord: state.discord || null,
    }
    await adminAccountsStore.updateAccount(payload)
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Account updated successfully!',
      duration: 3000,
    })
    handleClose()
  } catch (e) {
    console.error(e)
    toast.add({
      color: 'error',
      title: 'Error',
      description: ERROR_MESSAGES.default,
      duration: 3000,
    })
  }

  buttonIsLoading.value = false
}

const create = async () => {
  buttonIsLoading.value = true
  try {
    // Prepare the payload, ensuring null values are sent for empty fields
    const payload = {
      ...state,
      discordUniqueId: state.discordUniqueId ?? null,
      discord: state.discord || null,
    }
    await adminAccountsStore.createAccount(payload)
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Account created successfully!',
      duration: 3000,
    })
    handleClose()
  } catch (e) {
    console.error(e)
    toast.add({
      color: 'error',
      title: 'Error',
      description: ERROR_MESSAGES.default,
      duration: 3000,
    })
  }

  buttonIsLoading.value = false
}

const randomIntFromInterval = (min: number, max: number) => {
  return Math.floor(Math.random() * (max - min + 1) + min)
}

const generateSecretCode = () => {
  const newCode = String(randomIntFromInterval(1, 99999)).padStart(5, '0')
  state.uniqueCode = newCode
}
</script>
