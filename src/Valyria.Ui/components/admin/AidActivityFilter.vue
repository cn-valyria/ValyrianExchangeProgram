<template>
  <USlideover title="Filter Aid Activity" :close="{ onClick: () => emit('close') }">
    <template #body>
      <div class="p-4 space-y-6">
        <!-- Search and Direction -->
        <div class="space-y-4">
          <UFormField label="Search Text" help="Search by ruler name, nation name, or alliance name">
            <UInput v-model="search" placeholder="Enter name to search..." class="w-full" />
          </UFormField>
          <UFormField label="Search Direction" help="Which side of the aid the search text should match">
            <USelect
              v-model="direction"
              :items="directionOptions"
              :disabled="!search"
              class="w-full"
            />
          </UFormField>
        </div>

        <!-- Date Range -->
        <UFormField label="Aid Sent On">
          <UPopover>
            <UButton color="neutral" variant="subtle" icon="i-lucide-calendar">
              <template v-if="dateRange.start">
                <template v-if="dateRange.end">
                  {{ dateFormatter.format(dateRange.start.toDate(getLocalTimeZone())) }} - {{ dateFormatter.format(dateRange.end.toDate(getLocalTimeZone())) }}
                </template>
                <template v-else>
                  {{ dateFormatter.format(dateRange.start.toDate(getLocalTimeZone())) }}
                </template>
              </template>
              <template v-else>
                Pick a date
              </template>
            </UButton>

            <template #content>
              <UCalendar v-model="dateRange" class="p-2" :number-of-months="2" range />
            </template>
          </UPopover>
        </UFormField>

        <!-- Actions -->
        <div class="flex gap-2 pt-4">
          <UButton label="Apply Filters" color="primary" block @click="applyFilters()" />
          <UButton label="Clear" variant="outline" @click="clearFilters()" />
        </div>
      </div>
    </template>
  </USlideover>
</template>

<script setup lang="ts">
import { CalendarDate, DateFormatter, getLocalTimeZone } from '@internationalized/date'
import { useAdminAidActivityStore, type AidActivityFilter, type AidDirection } from '~/stores/adminAidActivity'

const props = defineProps<{
  filters: AidActivityFilter
}>()

const emit = defineEmits<{ close: [] }>()

const store = useAdminAidActivityStore()

const toCalendarDate = (date: Date | undefined) =>
  date ? new CalendarDate(date.getFullYear(), date.getMonth() + 1, date.getDate()) : undefined

const search = ref<string>(props.filters.search ?? '')
const direction = ref<AidDirection | 'either'>(props.filters.direction ?? 'either')
// shallowRef so the CalendarDate instances aren't unwrapped into plain objects
const dateRange = shallowRef<{ start: CalendarDate | undefined, end: CalendarDate | undefined }>({
  start: toCalendarDate(props.filters.sentSince),
  end: toCalendarDate(props.filters.sentUntil)
})

const directionOptions = [
  { label: 'Sent or received', value: 'either' },
  { label: 'Sent by', value: 'sent' },
  { label: 'Received by', value: 'received' }
]

const dateFormatter = new DateFormatter('en-US', {
  dateStyle: 'medium'
})

const applyFilters = () => {
  const start = dateRange.value?.start?.toDate(getLocalTimeZone())
  const end = dateRange.value?.end?.toDate(getLocalTimeZone()) ?? start

  store.filters = {
    search: search.value.trim() || undefined,
    direction: direction.value === 'either' ? undefined : direction.value,
    sentSince: start,
    sentUntil: end
  }
  emit('close')
}

const clearFilters = () => {
  search.value = ''
  direction.value = 'either'
  dateRange.value = { start: undefined, end: undefined }
}
</script>
