<template>
  <UModal :close="{ onClick: () => emit('close', false) }">
    <template #header>
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
        VEP Settings
      </h3>
    </template>

    <template #body>
      <USwitch
        v-model="settings.discordNotificationsEnabled"
        label="Discord notifications"
        description="Get pinged on Discord when you have a new deal, or when a deal needs something from you. You'll still get the daily CN messages either way."
      />
    </template>

    <template #footer>
      <div class="flex gap-3 justify-end w-full">
        <UButton label="Cancel" color="neutral" @click="emit('close', false)" />
        <UButton
          label="Save"
          :disabled="!hasChanges"
          :loading="isSaving"
          @click="save()"
        />
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import {
  useAccountStore,
  type Account,
  type AccountSettings,
} from '~/stores/account'

const props = defineProps<{
  account: Account
}>()

const emit = defineEmits<{ close: [boolean] }>()

const toast = useToast()
const accountStore = useAccountStore()

const settings = reactive<AccountSettings>({
  discordNotificationsEnabled: props.account.discordNotificationsEnabled,
})
const isSaving = ref<boolean>(false)

const hasChanges = computed<boolean>(
  () =>
    settings.discordNotificationsEnabled !==
    props.account.discordNotificationsEnabled,
)

const save = async () => {
  isSaving.value = true
  try {
    await accountStore.updateSettings(props.account.id, { ...settings })
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Your settings were saved.',
      duration: 3000,
    })
    emit('close', true)
  } catch (e) {
    console.error(e)
    toast.add({
      color: 'error',
      title: 'Error',
      description: ERROR_MESSAGES.default,
      duration: 5000,
    })
  } finally {
    isSaving.value = false
  }
}
</script>
