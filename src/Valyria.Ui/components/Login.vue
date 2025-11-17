<template>
  <UModal v-model="isVisible">
    <UCard>
      <template #header>
        <h3 class="text-2xl font-bold text-gray-900 dark:text-white">VEP Login Portal</h3>
      </template>

      <template #body>
        <div class="space-y-6">
          <!-- Nation ID or Ruler Name Section -->
          <div class="grid grid-cols-1 md:grid-cols-5 gap-4 items-center">
            <div class="md:col-span-2">
              <UFormGroup label="Nation ID" :error="nationIdOrRulerNameIsMissing ? 'Nation ID or Ruler Name is required' : undefined">
                <UInput
                  v-model="nationId"
                  placeholder="Enter Nation ID"
                  :color="nationIdOrRulerNameIsMissing ? 'error' : 'primary'"
                />
              </UFormGroup>
            </div>

            <div class="md:col-span-1 flex items-center justify-center">
              <span class="text-gray-500 dark:text-gray-400 font-medium">OR</span>
            </div>

            <div class="md:col-span-2">
              <UFormGroup label="Ruler Name" :error="nationIdOrRulerNameIsMissing ? 'Nation ID or Ruler Name is required' : undefined">
                <UInput
                  v-model="rulerName"
                  placeholder="Enter Ruler Name"
                  :color="nationIdOrRulerNameIsMissing ? 'error' : 'primary'"
                />
              </UFormGroup>
            </div>
          </div>

          <!-- Unique Code Section -->
          <UFormGroup label="Unique Code" :error="uniqueCodeIsMissing ? 'Unique Code is required' : undefined">
            <UInput
              v-model="uniqueCode"
              placeholder="Enter your unique code"
              :color="uniqueCodeIsMissing ? 'error' : 'primary'"
            />
          </UFormGroup>
        </div>
      </template>

      <template #footer>
        <div class="flex justify-end gap-3">
          <UButton color="neutral" variant="ghost" @click="closeDialog">
            Close
          </UButton>
          <UButton color="primary" @click="login">
            Log In
          </UButton>
        </div>
      </template>
    </UCard>
  </UModal>
</template>

<script setup lang="ts">
const props = defineProps<{
  show: boolean
}>()

const emit = defineEmits<{
  close: []
}>()

const authStore = useAuthStore()
const toast = useToast()

const nationId = ref('')
const rulerName = ref('')
const uniqueCode = ref('')
const nationIdOrRulerNameIsMissing = ref(false)
const uniqueCodeIsMissing = ref(false)

const isVisible = computed({
  get: () => props.show,
  set: (value) => {
    if (!value) {
      closeDialog()
    }
  },
})

function closeDialog() {
  emit('close')
  // Reset form
  nationId.value = ''
  rulerName.value = ''
  uniqueCode.value = ''
  nationIdOrRulerNameIsMissing.value = false
  uniqueCodeIsMissing.value = false
}

function formIsValid() {
  nationIdOrRulerNameIsMissing.value =
    nationId.value.length === 0 && rulerName.value.length === 0
  uniqueCodeIsMissing.value = uniqueCode.value.length === 0

  return !(nationIdOrRulerNameIsMissing.value || uniqueCodeIsMissing.value)
}

async function login() {
  if (!formIsValid()) {
    return
  }

  try {
    await authStore.login({
      nationId: nationId.value ? parseInt(nationId.value) : undefined,
      rulerName: rulerName.value || undefined,
      uniqueCode: uniqueCode.value,
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
