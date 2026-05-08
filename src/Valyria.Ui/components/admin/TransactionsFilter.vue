<template>
  <USlideover title="Filter Transactions" :close="{ onClick: () => emit('close') }">
    <template #body>
      <UForm ref="form" :validate="validate" :state="filters" @submit="onSubmit" @error="onError" class="p-4 space-y-6">
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

        <fieldset disabled class="space-y-4 mt-4 px-2">
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
                v-model="_filters.aidStatus"
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
                <UInput v-model="_filters.money" class="w-full" />
              </UFormField>
              <UFormField label="Tech">
                <UInput v-model="_filters.tech" class="w-full" />
              </UFormField>
            </div>
            <UFormField label="Classification">
              <USelectMenu
                v-model="_filters.classification"
                :items="classificationOptions"
                value-key="id"
                label-key="description"
                placeholder="Any classification"
                clearable
                class="w-full"
              />
            </UFormField>
          </div>
        </fieldset>

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
import { DateFormatter, getLocalTimeZone, CalendarDate } from '@internationalized/date'
import type { FormError, FormErrorEvent, FormSubmitEvent } from '@nuxt/ui';
import { transactionClassifications, accountRoles } from '~/infrastructure/dataLists'
import { type TransactionFilter, useAdminTransactionsStore } from '~/stores/adminTransactions'

const props = defineProps<{
  filters: TransactionFilter
}>()

const emit = defineEmits<{ close: [] }>()

const toast = useToast()
const form = ref()
const store = useAdminTransactionsStore()

// Local filter state that will be patched to store
const _filters = reactive<TransactionFilter>({ ...props.filters })

// Intermediate state for search/direction (needed to avoid circular dependency)
const _searchText = ref(_filters.sentBy || _filters.receivedBy || '')
const _directionSelection = ref<string[]>([
  ...(_filters.sentBy ? ['Sent'] : []),
  ...(_filters.receivedBy ? ['Received'] : [])
])

// Computed properties with getters/setters
const search = computed({
  get: () => {
    // Extract from _filters (sentBy or receivedBy), fallback to intermediate state
    return _filters.sentBy || _filters.receivedBy || _searchText.value
  },
  set: (value: string) => {
    _searchText.value = value
    // Update filters if direction is set
    if (_directionSelection.value.length > 0) {
      _filters.sentBy = _directionSelection.value.includes('Sent') ? (value || null) : null
      _filters.receivedBy = _directionSelection.value.includes('Received') ? (value || null) : null
    }
  }
})

const direction = computed({
  get: () => {
    // Derive from _filters, fallback to intermediate state
    const directions: string[] = []
    if (_filters.sentBy) directions.push('Sent')
    if (_filters.receivedBy) directions.push('Received')
    return directions.length > 0 ? directions : _directionSelection.value
  },
  set: (value: string[]) => {
    _directionSelection.value = value
    // Update filters if search has a value
    if (_searchText.value) {
      _filters.sentBy = value.includes('Sent') ? _searchText.value : null
      _filters.receivedBy = value.includes('Received') ? _searchText.value : null
    }
  }
})

const aidSentDateRange = computed({
  get: () => {
    // Convert Date objects back to CalendarDate format
    if (_filters.sentSince) {
      const start = _filters.sentSince
      const end = _filters.sentUntil

      return {
        start: start ? new CalendarDate(start.getFullYear(), start.getMonth() + 1, start.getDate()) : null,
        end: end && end !== start ? new CalendarDate(end.getFullYear(), end.getMonth() + 1, end.getDate()) : null
      }
    }
    return { start: null, end: null }
  },
  set: (value: { start: any, end: any }) => {
    // Convert CalendarDate to Date objects and update filters
    if (value && value.start) {
      _filters.sentSince = new Date(value.start.year, value.start.month - 1, value.start.day)
      if (value.end) {
        _filters.sentUntil = new Date(value.end.year, value.end.month - 1, value.end.day)
      } else {
        _filters.sentUntil = _filters.sentSince
      }
    } else {
      _filters.sentSince = null
      _filters.sentUntil = null
    }
  }
})

const sendingRole = computed({
  get: () => {
    // Extract sending role from rolePairing
    if (_filters.rolePairing && _filters.rolePairing.length > 0) {
      return _filters.rolePairing.charAt(0) || null
    }
    return null
  },
  set: (value: string | null) => {
    // Update rolePairing in filters
    const currentReceivingRole = receivingRole.value
    _filters.rolePairing = `${value ?? ''}${currentReceivingRole ?? ''}`
  }
})

const receivingRole = computed({
  get: () => {
    // Extract receiving role from rolePairing
    if (_filters.rolePairing && _filters.rolePairing.length > 1) {
      return _filters.rolePairing.charAt(1) || null
    }
    return null
  },
  set: (value: string | null) => {
    // Update rolePairing in filters
    const currentSendingRole = sendingRole.value
    _filters.rolePairing = `${currentSendingRole ?? ''}${value ?? ''}`
  }
})

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

const validate = (state: any): FormError[] => {
  const errors = []
  if (search.value && direction.value.length === 0) {
    errors.push({ name: 'direction', message: 'Must specify direction (sent/received) of the search text' })
  }
  return errors
}

const onSubmit = async (event: FormSubmitEvent<any>) => {
  store.$patch({ filters: _filters })
  emit('close')
}

const onError = async (event: FormErrorEvent) => {
  if (event?.errors?.length > 0) {
    toast.add({
      description: `Form validation failed: ${event.errors.map(error => error.message).join(', ')}`,
      title: 'Validation Error',
      color: 'error',
      duration: 3000
    })
  }
}

// Clear all filters
const clearFilters = (): void => {
  search.value = null
  direction.value = null
  aidSentDateRange.value = null
  sendingRole.value = null
  receivingRole.value = null
}
</script>