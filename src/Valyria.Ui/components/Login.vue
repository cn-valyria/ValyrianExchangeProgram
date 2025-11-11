<template>
  <Dialog v-model:visible="isVisible" modal header="VEP Login Portal" :style="{ width: '50rem' }">
    <div class="grid">
      <div class="col-5">
        <div class="field">
          <label for="nationId" class="block mb-2">Nation ID</label>
          <InputText
            id="nationId"
            v-model="nationId"
            type="text"
            class="w-full"
            :invalid="nationIdOrRulerNameIsMissing"
          />
          <small v-if="nationIdOrRulerNameIsMissing" class="text-red-500">
            Nation ID or Ruler Name is required.
          </small>
        </div>
      </div>
      <Divider layout="vertical" class="col-2">OR</Divider>
      <div class="col-5">
        <div class="field">
          <label for="rulerName" class="block mb-2">Ruler Name</label>
          <InputText
            id="rulerName"
            v-model="rulerName"
            type="text"
            class="w-full"
            :invalid="nationIdOrRulerNameIsMissing"
          />
          <small v-if="nationIdOrRulerNameIsMissing" class="text-red-500">
            Nation ID or Ruler Name is required.
          </small>
        </div>
      </div>
    </div>
    <div class="field mt-4">
      <label for="uniqueCode" class="block mb-2">Unique Code</label>
      <InputText
        id="uniqueCode"
        v-model="uniqueCode"
        type="text"
        class="w-full"
        :invalid="uniqueCodeIsMissing"
      />
      <small v-if="uniqueCodeIsMissing" class="text-red-500">Unique Code is required.</small>
    </div>

    <template #footer>
      <Button label="Close" text @click="closeDialog" />
      <Button label="Log In" @click="login" />
    </template>
  </Dialog>
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
      severity: 'success',
      summary: 'Login Successful',
      detail: 'Welcome back!',
      life: 3000,
    })

    closeDialog()
  } catch (error) {
    console.error('Login failed:', error)
    toast.add({
      severity: 'error',
      summary: 'Login Failed',
      detail: 'Invalid credentials. Please try again.',
      life: 5000,
    })
  }
}
</script>
