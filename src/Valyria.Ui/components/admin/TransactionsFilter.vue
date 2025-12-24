<template>
  <USlideover title="Filter Transactions" :close="{ onClick: () => emit('close', false, state) }">
    <template #body>
      <UForm ref="form" :schema="schema" :state="state" @submit="onSubmit" @error="onError" class="p-4 space-y-6">
        <!-- Search and Direction -->
        <div class="space-y-4">
          <UFormField label="Search" hint="Search by ruler name, nation name, or alliance name">
            <UInput v-model="search" placeholder="Enter name to search..." />
          </UFormField>
          <UFormField label="Direction">
            <USelectMenu
              v-model="direction"
              :options="directionOptions"
              multiple
              placeholder="Select direction(s)"
            />
          </UFormField>
        </div>

        <!-- Date Range -->
        <UFormField label="Aid Sent On">
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
        </UFormField>

        <!-- Role Pairing -->
        <div class="space-y-4">
          <UFormField label="Sending Role" hint="Filter by sending role">
            <USelectMenu
              v-model="sendingRole"
              :options="roleOptions"
              value-attribute="code"
              option-attribute="description"
              placeholder="Any role"
              clearable
            />
          </UFormField>
          <UFormField label="Receiving Role" hint="Filter by receiving role">
            <USelectMenu
              v-model="receivingRole"
              :options="roleOptions"
              value-attribute="code"
              option-attribute="description"
              placeholder="Any role"
              clearable
            />
          </UFormField>
        </div>

        <!-- Aid Status, Money/Tech, Classification -->
        <div class="space-y-4">
          <UFormField label="Aid Status">
            <USelectMenu
              v-model="state.aidStatus"
              :options="aidStatusOptions"
              value-attribute="id"
              option-attribute="label"
              placeholder="Any status"
              clearable
            />
          </UFormField>
          <!-- <UFormField label="Money">
            <USelectMenu
              v-model="state.resourceType"
              :options="resourceTypeOptions"
              placeholder="Any type"
              clearable
            />
          </UFormField> -->
          <UFormField label="Classification">
            <USelectMenu
              v-model="state.classification"
              :options="classificationOptions"
              value-attribute="id"
              option-attribute="description"
              placeholder="Any classification"
              clearable
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

// Computed label for date range picker
const dateRangeLabel = computed(() => {
  if (!dateRange.value || !dateRange.value.start) return 'Select date range'
  const start = new Date(dateRange.value.start.year, dateRange.value.start.month - 1, dateRange.value.start.day).toLocaleDateString()
  const end = dateRange.value.end
    ? new Date(dateRange.value.end.year, dateRange.value.end.month - 1, dateRange.value.end.day).toLocaleDateString()
    : start
  return `${start} - ${end}`
})

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