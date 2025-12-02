<template>
  <UModal :close="{ onClick: () => emit('close', false) }">

    <template #header>
      <h3 class="text-lg font-semibold">{{ props.editingAdmin.id ? 'Edit' : 'Add' }} Admin Access</h3>
    </template>

    <template #body>
      <UForm :schema="schema" :state="state" @submit="onSubmit" class="space-y-4">
        <UFormField label="Account ID">
          <UInput v-model="state.accountId" placeholder="Account name" />
        </UFormField>

        <UFormField label="Role">
          <USelect
            v-model="state.role"
            :options="adminRoleOptions"
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
  editingAdmin?: any
}

const props = withDefaults(defineProps<Props>(), {
  editingAdmin: () => ({})
})

const emit = defineEmits<{ close: [boolean] }>()
const submitButton = ref<HTMLButtonElement>()

// Define the form state
const state = reactive<any>(props.editingAdmin)

// Define the Zod schema - only validates when creating
const schema = z.object({
  accountId: z.number().nullable(),
  role: z.string().nullable()
})

// Admin role options
const adminRoleOptions = [
  { label: 'Admin', value: 'Admin' },
  { label: 'Admin - Lists', value: 'Admin - Lists' }
]

const handleClose = () => {
  emit("close", false);
}

const submitForm = () => {
  submitButton.value?.click()
}

const onSubmit = async () => {
  toast.add({ color: 'success', title: 'Success', description: 'Account updated successfully!', duration: 3000 })
  handleClose()
}
</script>