<template>
  <UModal :close="{ onClick: () => emit('close', false) }">
    <template #header>
      <h3 class="text-lg font-semibold">Add +1 Aid Slot Team Event</h3>
    </template>

    <template #body>
      <div class="space-y-4">
        <UAlert
          color="warning"
          icon="i-heroicons-exclamation-triangle"
          title="Only add the event once it has passed"
          description="Wait until a majority of the team's senators have approved the proposal in-game. VEP gives every nation on the team an extra aid slot from the approval date, and takes it away 30 days later, so it should match exactly when the event is in effect in CN."
        />

        <UFormField label="Team" required>
          <USelect
            v-model="team"
            :items="teamOptions"
            placeholder="Select team"
            class="w-full"
          />
        </UFormField>

        <UFormField
          label="Approval Date"
          help='The date CN shows on the team&apos;s page, e.g. "Proposal passed on 10/6/2026"'
          required
        >
          <UPopover>
            <UButton color="neutral" variant="subtle" icon="i-lucide-calendar">
              {{ approvedOn ? formatCnDate(approvedOn) : 'Pick a date' }}
            </UButton>

            <template #content>
              <UCalendar
                v-model="approvedOnDate"
                :max-value="maxDate"
                class="p-2"
              />
            </template>
          </UPopover>
        </UFormField>

        <div
          v-if="approvedOn"
          class="text-sm text-gray-600 dark:text-gray-400 space-y-1"
        >
          <p>
            The bonus slot is in effect through
            <span class="font-semibold">{{ formatCnDate(endsOn) }}</span
            >, and deals on it are cancelled after that.
          </p>
          <p>
            To leave time for every aid offer, VEP stops matching new deals into
            the bonus slot early: after
            {{ formatCnDate(addDays(approvedOn, 10)) }} for 1:2 slots and
            {{ formatCnDate(addDays(approvedOn, 20)) }} for 1:1 slots. One-way
            slots are matched until the event ends.
          </p>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton label="Cancel" color="neutral" @click="handleClose" />
        <UButton
          color="primary"
          :loading="saving"
          :disabled="!team || !approvedOn"
          @click="save()"
        >
          Add Event
        </UButton>
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { CalendarDate } from '@internationalized/date'
import type { ApiError } from '~/composables/useApiClient'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import {
  addDays,
  cnToday,
  EVENT_TEAMS,
  formatCnDate,
  TEAM_EVENT_DAYS,
  type Team,
} from '~/infrastructure/teamEvents'

const toast = useToast()
const teamEventsStore = useAdminTeamEventsStore()

const emit = defineEmits<{ close: [boolean] }>()

const toCalendarDate = (date: string) => {
  const [year, month, day] = date.split('-').map(Number)
  return new CalendarDate(year, month, day)
}

const team = ref<Team>()
// shallowRef so the CalendarDate isn't unwrapped into a plain object
const approvedOnDate = shallowRef<CalendarDate>()
// Events can't be approved after today's CN date
const maxDate = toCalendarDate(cnToday())
const saving = ref(false)

const approvedOn = computed(() => approvedOnDate.value?.toString())
const endsOn = computed(() =>
  approvedOn.value ? addDays(approvedOn.value, TEAM_EVENT_DAYS) : '',
)

const teamOptions = EVENT_TEAMS.map((t) => ({ label: t.name, value: t.team }))

const saveErrors: Record<number, string> = {
  400: "That team or approval date isn't allowed. Events can't be approved in the future.",
  409: 'This team already has a +1 aid slot event during those 30 days.',
}

const handleClose = () => {
  emit('close', false)
}

const save = async () => {
  if (!team.value || !approvedOn.value) return

  saving.value = true
  try {
    await teamEventsStore.createTeamEvent({
      team: team.value,
      approvedOn: approvedOn.value,
    })
  } catch (e) {
    toast.add({
      color: 'error',
      title: 'Error',
      description:
        saveErrors[(e as ApiError)?.statusCode] ?? ERROR_MESSAGES.default,
      duration: 5000,
    })
    return
  } finally {
    saving.value = false
  }

  toast.add({
    color: 'success',
    title: 'Success',
    description: 'Team event added. Its nations now have a bonus aid slot.',
    duration: 3000,
  })
  emit('close', true)
}
</script>
