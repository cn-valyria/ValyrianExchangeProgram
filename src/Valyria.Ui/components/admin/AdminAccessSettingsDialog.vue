<template>
  <UModal :close="{ onClick: () => emit('close', false) }">

    <template #header>
      <h3 class="text-lg font-semibold">{{ props.editingAdmin?.accountId ? 'Edit' : 'Add' }} Admin Access</h3>
    </template>

    <template #body>
      <UForm :schema="schema" :state="state" @submit="onSubmit" class="space-y-4">
        <UFormField label="Account ID">
          <UInputMenu v-model="state.accountId" :items="accounts" value-key="id" label-key="rulerName" placeholder="Account ID" />
        </UFormField>

        <UFormField label="Role">
          <USelectMenu
            v-model="state.adminRoleId"
            :items="adminRoleOptions"
            value-key="value"
            placeholder="Select admin role"
          />
        </UFormField>

        <!-- Hidden submit button for form submission -->
        <button type="submit" class="hidden" ref="submitButton"></button>
      </UForm>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton label="Cancel" color="neutral" @click="handleClose" />
        <UButton color="primary" :loading="buttonIsLoading" @click="submitForm()">
          Save
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import z from 'zod';
import { ERROR_MESSAGES } from '~/infrastructure/constants';

const toast = useToast()
const adminAccessStore = useAdminAccessStore()
const adminAccountsStore = useAdminAccountsStore()

const { accounts } = storeToRefs(adminAccountsStore)

interface Props {
  editingAdmin?: AdminAccessSetting
}

const props = withDefaults(defineProps<Props>(), {
  editingAdmin: () => ({} as AdminAccessSetting)
})

const emit = defineEmits<{ close: [boolean] }>()
const submitButton = ref<HTMLButtonElement>()

const buttonIsLoading = ref(false)

// Admin role options
const adminRoleOptions = [
  { label: 'Super Admin', value: 1 },
  { label: 'Admin (Read-only)', value: 2 },
  { label: 'Admin - Lists', value: 3 }
]

// Define the form state
const state = reactive<AdminAccessSetting>(props.editingAdmin)

// Define the Zod schema - only validates when creating
const schema = z.object({
  accountId: z.number().nullable(),
  adminRoleId: z.number().nullable()
})

const handleClose = () => {
  emit("close", false);
}

const submitForm = () => {
  console.log(schema.safeParse(state))
  submitButton.value?.click()
}

const onSubmit = async () => {
  console.log(state)
  buttonIsLoading.value = true
  // The store reports failure by returning false rather than throwing
  const created = await adminAccessStore.createAdminAccess(state)
  buttonIsLoading.value = false

  if (!created) {
    toast.add({ color: 'error', title: 'Error', description: ERROR_MESSAGES.default, duration: 3000 })
    return
  }

  toast.add({ color: 'success', title: 'Success', description: 'Access added successfully!', duration: 3000 })
  handleClose()
}

onMounted(async () => {
  if (accounts.value.length === 0) {
    try {
      await adminAccountsStore.loadAllAccounts(false)
      console.log('Accounts loaded:', accounts.value)
    } catch (error) {
      console.error('Failed to load accounts:', error)
      toast.add({
        color: 'error',
        title: 'Error',
        description: 'Failed to load accounts. Please try again.',
        duration: 5000
      })
    }
  } else {
    console.log('Accounts already loaded into state')
  }
})
</script>