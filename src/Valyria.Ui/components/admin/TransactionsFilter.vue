<template>
  <USlideover title="Filter Transactions" :close="{ onClick: () => emit('close', false, state) }">
    <template #body>
      <UForm ref="form" :schema="schema" :state="state" @submit="onSubmit" @error="onError" class="p-4 space-y-6">
        <!-- Search and Direction -->
        <div class="space-y-4">
          <UFormField label="Search Text" help="Search by ruler name, nation name, or alliance name">
            <UInput v-model="search" placeholder="Enter name to search..." class="w-full" />
          </UFormField>
          <UFormField label="Search Direction">
            <USelectMenu
              v-model="direction"
              :items="directionOptions"
              multiple
              placeholder="Select direction(s)"
              class="w-full"
            />
          </UFormField>
        </div>

        <!-- Date Range -->
        <!-- <UFormField label="Aid Sent On">
          <UPopover :popper="{ placement: 'bottom-start' }">
            <UButton
              icon="i-heroicons-calendar-days-20-solid"
              :label="dateRangeLabel"
              variant="outline"
              block
            />
            <template #panel="{ close }">
              <UCalendar v-model="dateRange" @update:model-value="close" />
            </template>
          </UPopover>
        </UFormField> -->
        <UFormField label="Aid Sent On">
          <UPopover>
            <UButton color="neutral" variant="subtle" icon="i-lucide-calendar">
              <template v-if="aidSentDateRange.start">
                <template v-if="aidSentDateRange.end">
                  {{ dateFormatter.format(aidSentDateRange.start.toDate(getLocalTimeZone())) }} - {{ dateFormatter.format(aidSentDateRange.end.toDate(getLocalTimeZone())) }}
                </template>

                <template v-else>
                  {{ dateFormatter.format(aidSentDateRange.start.toDate(getLocalTimeZone())) }}
                </template>
              </template>
              <template v-else>
                Pick a date
              </template>
            </UButton>

            <template #content>
              <UCalendar v-model="aidSentDateRange" class="p-2" :number-of-months="2" range />
            </template>
          </UPopover>
        </UFormField>

        <!-- Role Pairing -->
        <div class="space-y-4">
          <UFormField label="Sending Role">
            <USelectMenu
              v-model="sendingRole"
              :items="roleOptions"
              value-key="code"
              label-key="description"
              placeholder="Any role"
              clearable
              class="w-full"
            />
          </UFormField>
          <UFormField label="Receiving Role">
            <USelectMenu
              v-model="receivingRole"
              :items="roleOptions"
              value-key="code"
              label-key="description"
              placeholder="Any role"
              clearable
              class="w-full"
            />
          </UFormField>
        </div>

        <!-- Aid Status, Money/Tech, Classification -->
        <div class="space-y-4">
          <UFormField label="Aid Status">
            <USelectMenu
              v-model="state.aidStatus"
              :items="aidStatusOptions"
              value-key="id"
              label-key="label"
              placeholder="Any status"
              clearable
              class="w-full"
            />
          </UFormField>
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <UFormField label="Money">
              <UInput v-model="state.money" class="w-full" />
            </UFormField>
            <UFormField label="Tech">
              <UInput v-model="state.tech" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Classification">
            <USelectMenu
              v-model="state.classification"
              :items="classificationOptions"
              value-key="id"
              label-key="description"
              placeholder="Any classification"
              clearable
              class="w-full"
            />
          </UFormField>
        </div>

        <!-- Actions -->
        <div class="flex gap-2 pt-4">
          <UButton
            label="Apply Filters"
            color="primary"
            block
            @click="form.submit()"
          />
          <UButton
            label="Clear"
            variant="outline"
            @click="clearFilters"
          />
        </div>
      </UForm>
    </template>
  </USlideover>
</template>

<script setup lang="ts">
import { CalendarDate, DateFormatter, getLocalTimeZone } from '@internationalized/date'
import { z } from 'zod';
import type { FormErrorEvent, FormSubmitEvent } from '@nuxt/ui';
import { transactionClassifications, accountRoles } from '~/infrastructure/dataLists'

interface Props {
  filters: TransactionFilter
}

const props = withDefaults(defineProps<Props>(), {
  filters: () => ({} as TransactionFilter)
})

const emit = defineEmits<{ close: [boolean, TransactionFilter] }>()

const toast = useToast()
const form = ref()

// Define the form state
const state = reactive<TransactionFilter>(props.filters)

// Define the Zod schema
const schema = z.object({
  sentBy: z.string().optional().nullable(),
  receivedBy: z.string().optional().nullable(),
  sentSince: z.date().optional().nullable(),
  sentUntil: z.date().optional().nullable(),
  rolePairing: z.string().optional().nullable(),
  aidStatus: z.number().optional().nullable() ,
  classification: z.number().optional().nullable(),
  money: z.number().optional().nullable() ,
  tech: z.number().optional().nullable()
})

// UI-only filter values
const search = ref('')
const direction = ref<string[]>([])
const dateRange = ref()
const sendingRole = ref<string | null>()
const receivingRole = ref<string | null>()

// Filter options
const directionOptions = ['Sent', 'Received']
const roleOptions = accountRoles
const classificationOptions = transactionClassifications
const aidStatusOptions = [
  { id: 1, label: 'Pending' },
  { id: 2, label: 'Approved' },
  { id: 3, label: 'Cancelled' },
  { id: 4, label: 'Expired' }
]

const dateFormatter = new DateFormatter('en-US', {
  dateStyle: 'medium'
})

const aidSentDateRange = shallowRef({ start: null, end: null })

const onSubmit = async (event: FormSubmitEvent<any>) => {
  if (search.value) {
    if (direction.value.includes('Sent')) {
      state.sentBy = search.value
    }
    if (direction.value.includes('Received')) {
      state.receivedBy = search.value
    }
    // If no direction selected, search both
    if (direction.value.length === 0) {
      state.sentBy = search.value
      state.receivedBy = search.value
    }
  }

  // Convert date range to Date objects
  if (dateRange.value && dateRange.value.start) {
    state.sentSince = new Date(dateRange.value.start.year, dateRange.value.start.month - 1, dateRange.value.start.day)
    if (dateRange.value.end) {
      state.sentUntil = new Date(dateRange.value.end.year, dateRange.value.end.month - 1, dateRange.value.end.day)
    } else {
      state.sentUntil = state.sentSince
    }
  }

  // Convert sending + receiving role to role pairing
  if (sendingRole.value || receivingRole.value) {
    state.rolePairing = `${sendingRole.value ?? ''}${receivingRole.value ?? ''}`
  }

  emit('close', true, state)
}

const onError = async (event: FormErrorEvent) => {
  if (event?.errors?.length > 0) {
    toast.add({
      description: `Form validation failed: ${event.errors.map(error => error.message).join(', ')}`,
      title: 'Validation Error',
      color: 'success',
      duration: 3000
    })
  }
}

// Clear all filters
const clearFilters = (): void => {
  state.sentBy = null
  state.receivedBy = null
  state.sentSince = null 
  state.sentUntil = null
  state.aidStatus = null
  state.money = null
  state.tech = null
  state.classification = null

  emit('close', false, state)
}
</script>