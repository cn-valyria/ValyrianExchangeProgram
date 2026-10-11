<template>
  <UModal :close="{ onClick: () => emit('close', false) }">

    <template #header>
      <h3 class="text-lg font-semibold">{{ props.editingTeamEvent.id ? 'Edit' : 'Add' }} Team Event</h3>
    </template>

    <template #body>
      <UForm :schema="schema" :state="state" class="space-y-4" @submit="onSubmit">
        <UFormField label="Team">
          <USelect
            v-model="state.team"
            :items="teamOptions"
            placeholder="Select team"
          />
        </UFormField>

        <UFormField label="Event Type">
          <USelect
            v-model="state.eventType"
            :items="eventTypeOptions"
            placeholder="Select event type"
          />
        </UFormField>

        <UFormField label="Event Name (optional)">
          <UTextarea v-model="state.eventName" placeholder="Event name or description" />
        </UFormField>

        <!-- Hidden submit button for form submission -->
        <button ref="submitButton" type="submit" class="hidden"></button>
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
import type { TeamEvent } from '~/stores/shared/teamEvent'

const toast = useToast()

interface Props {
  editingTeamEvent?: TeamEvent
}

const props = withDefaults(defineProps<Props>(), {
  editingTeamEvent: () => ({})
})

const emit = defineEmits<{ close: [boolean] }>()
const submitButton = ref<HTMLButtonElement>()

// Define the form state
const state = reactive<TeamEvent>(props.editingTeamEvent)

// Define the Zod schema
const schema = z.object({
  team: z.string().nullable(),
  eventType: z.string().nullable(),
  eventName: z.string().nullable().optional()
})

// Team options
const teamOptions = [
  { label: 'Aqua', value: 'Aqua' },
  { label: 'White', value: 'White' },
  { label: 'Red', value: 'Red' },
  { label: 'Green', value: 'Green' },
  { label: 'Brown', value: 'Brown' },
  { label: 'Blue', value: 'Blue' },
  { label: 'Purple', value: 'Purple' },
  { label: 'Orange', value: 'Orange' },
  { label: 'Maroon', value: 'Maroon' },
  { label: 'Black', value: 'Black' },
  { label: 'Yellow', value: 'Yellow' },
  { label: 'Pink', value: 'Pink' },
  { label: 'None (Grey)', value: 'None (Grey)' }
]

// Event type options
const eventTypeOptions = [
  { label: '+1 Aid Slot', value: '+1 Aid Slot' },
  { label: '-1 Aid Slot', value: '-1 Aid Slot' }
]

const handleClose = () => {
  emit("close", false);
}

const submitForm = () => {
  submitButton.value?.click()
}

const onSubmit = async () => {
  toast.add({ color: 'success', title: 'Success', description: 'Team event saved successfully!', duration: 3000 })
  handleClose()
}
</script>
