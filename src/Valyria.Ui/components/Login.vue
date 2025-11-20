<template>
  <UModal v-model:open="open">
    <UButton color="primary">Log In</UButton>

    <template #header>
      <h3 class="text-2xl font-bold text-gray-900 dark:text-white">VEP Login Portal</h3>
    </template>

    <template #body>
      <UForm :schema="schema" :state="state" @submit="onSubmit" class="space-y-6">
        <!-- Nation ID or Ruler Name Section -->
        <div class="grid grid-cols-1 md:grid-cols-5 gap-4 items-center">
          <div class="md:col-span-2">
            <UFormField label="Nation ID" name="nationId">
              <UInput
                v-model="state.nationId"
                placeholder="Enter Nation ID"
              />
            </UFormField>
          </div>

          <div class="md:col-span-1 flex items-center justify-center">
            <span class="text-gray-500 dark:text-gray-400 font-medium">OR</span>
          </div>

          <div class="md:col-span-2">
            <UFormField label="Ruler Name" name="rulerName">
              <UInput
                v-model="state.rulerName"
                placeholder="Enter Ruler Name"
              />
            </UFormField>
          </div>
        </div>

        <!-- Unique Code Section -->
        <UFormField label="Unique Code" name="uniqueCode">
          <UInput
            v-model="state.uniqueCode"
            placeholder="Enter your unique code"
          />
        </UFormField>

        <!-- Hidden submit button for form submission -->
        <button type="submit" class="hidden" ref="submitButton"></button>
      </UForm>
    </template>

    <template #footer>
      <div class="flex justify-end gap-3">
        <UButton color="neutral" variant="ghost" @click="closeDialog">
          Close
        </UButton>
        <UButton color="primary" @click="submitForm">
          Log In
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { z } from 'zod'

const authStore = useAuthStore()
const toast = useToast()

const open = ref(false)
const submitButton = ref<HTMLButtonElement>()

// Define the form state
const state = reactive({
  nationId: '',
  rulerName: '',
  uniqueCode: ''
})

// Define the Zod schema with custom validation
const schema = z.object({
  nationId: z.string().optional(),
  rulerName: z.string().optional(),
  uniqueCode: z.string().min(1, 'Unique Code is required')
}).refine(
  (data) => data.nationId || data.rulerName,
  {
    message: 'Either Nation ID or Ruler Name is required',
    path: ['nationId'] // This will show the error on nationId field
  }
)

function closeDialog() {
  open.value = false
  // Reset form
  state.nationId = ''
  state.rulerName = ''
  state.uniqueCode = ''
}

function submitForm() {
  // Trigger the hidden submit button to invoke form validation
  submitButton.value?.click()
}

async function onSubmit() {
  try {
    await authStore.login({
      nationId: state.nationId || undefined,
      rulerName: state.rulerName || undefined,
      uniqueCode: state.uniqueCode,
    })

    toast.add({
      title: 'Login Successful',
      description: 'Welcome back!',
      color: 'success'
    })

    closeDialog()
  } catch (error) {
    console.error('Login failed:', error)
    toast.add({
      title: 'Login Failed',
      description: 'Invalid credentials. Please try again.',
      color: 'error'
    })
  }
}
</script>
