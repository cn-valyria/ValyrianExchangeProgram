<template>
  <UModal :close="{ onClick: () => emit('close', false) }">

    <template #header>
      <h3 class="text-lg font-semibold">{{ props.editingAdmin?.accountId ? 'Edit' : 'Add' }} Admin Access</h3>
    </template>

    <template #body>
      <UForm :schema="schema" :state="state" @submit="onSubmit" class="space-y-4">
        <UFormField label="Account ID">
          <UInput v-model="state.accountId" placeholder="Account ID" />
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
        <UButton color="primary" @click="submitForm()">
          Save
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import z from 'zod';

const toast = useToast()

interface Props {
  editingAdmin?: AdminAccessSetting
}

const props = withDefaults(defineProps<Props>(), {
  editingAdmin: () => ({} as AdminAccessSetting)
})

const emit = defineEmits<{ close: [boolean] }>()
const submitButton = ref<HTMLButtonElement>()

// Define the form state
const state = reactive<AdminAccessSetting>(props.editingAdmin)

// Define the Zod schema - only validates when creating
const schema = z.object({
  accountId: z.number().nullable(),
  adminRoleId: z.number().nullable()
})

// Admin role options
const adminRoleOptions = [
  { label: 'Super Admin', value: 1 },
  { label: 'Admin - Lists', value: 3 }
]

const handleClose = () => {
  emit("close", false);
}

const submitForm = () => {
  submitButton.value?.click()
}

const onSubmit = async () => {
  toast.add({ color: 'error', title: 'Error', description: 'LWW still implementing this.', duration: 3000 })
  handleClose()
}
</script>