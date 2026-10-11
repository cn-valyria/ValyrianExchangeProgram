<template>
  <UContainer>
    <div class="py-8 space-y-8">
      <!-- Header -->
      <div class="flex justify-between items-start">
        <div>
          <h1 class="text-3xl font-bold text-gray-900 dark:text-white">
            Aid Activity
          </h1>
          <p class="text-gray-600 dark:text-gray-400 mt-2">
            Every aid sent or received by VEP accounts, and the deal each one
            counted toward
          </p>
        </div>
        <UButton
          icon="i-heroicons-funnel"
          label="Filters"
          color="primary"
          @click="openFilterSlideover()"
        />
      </div>

      <UCard>
        <div class="space-y-4">
          <div class="flex flex-wrap items-center gap-6">
            <USwitch v-model="includeOnHold">
              <template #label>
                <span class="text-sm text-gray-600 dark:text-gray-400"
                  >Include On Hold Accounts</span
                >
              </template>
            </USwitch>
            <USwitch v-model="unmatchedOnly">
              <template #label>
                <span class="text-sm text-gray-600 dark:text-gray-400"
                  >Only Aid With No Deal</span
                >
              </template>
            </USwitch>
            <div
              v-if="activeFilterSummary"
              class="text-sm text-gray-500 dark:text-gray-400"
            >
              Filtered: {{ activeFilterSummary }}
            </div>
          </div>

          <UTable
            :data="currentPage.results"
            :columns="columns"
            :loading="tableIsLoading"
          />

          <!-- Pagination -->
          <div class="flex justify-between items-center">
            <div class="text-sm text-gray-600 dark:text-gray-400">
              Showing
              {{
                Math.min((pageNumber - 1) * limit + 1, currentPage.totalCount)
              }}
              to {{ Math.min(pageNumber * limit, currentPage.totalCount) }} of
              {{ currentPage.totalCount }} results
            </div>
            <UPagination
              v-model:page="pageNumber"
              :total="currentPage.totalCount"
              :items-per-page="limit"
            />
          </div>
        </div>
      </UCard>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { storeToRefs } from 'pinia'
import { accountRoles } from '~/infrastructure/dataLists'
import {
  useAdminAidActivityStore,
  type AidActivityItem,
  type AidParty,
} from '~/stores/adminAidActivity'
import AidActivityFilter from '~/components/admin/AidActivityFilter.vue'

const toast = useToast()
const overlay = useOverlay()

const filterSlideover = overlay.create(AidActivityFilter)

const store = useAdminAidActivityStore()
const { currentPage, filters, includeOnHold, unmatchedOnly } =
  storeToRefs(store)

const pageNumber = ref<number>(1)
const limit = ref<number>(25)
const tableIsLoading = ref<boolean>(false)

const formatDate = (date: string | null): string =>
  date ? date.split('T')[0] : '—'

const activeFilterSummary = computed<string>(() => {
  const parts: string[] = []
  const { search, direction, sentSince, sentUntil } = filters.value
  if (search) {
    const side =
      direction === 'sent'
        ? 'sent by '
        : direction === 'received'
          ? 'received by '
          : ''
    parts.push(`${side}"${search}"`)
  }
  if (sentSince) {
    const since = sentSince.toLocaleDateString()
    const until = sentUntil?.toLocaleDateString()
    parts.push(until && until !== since ? `${since} to ${until}` : since)
  }
  return parts.join(', ')
})

const renderParty = (party: AidParty) => {
  const role = accountRoles.find((r) => r.code === party.role)
  return h('div', [
    h(
      'div',
      { class: 'font-medium text-gray-900 dark:text-white' },
      party.rulerName ?? `Nation #${party.nationId}`,
    ),
    h(
      'div',
      {
        class:
          'flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400',
      },
      [
        h('span', party.nationName ?? ''),
        party.accountId === null
          ? h(
              resolveComponent('UBadge'),
              { color: 'neutral', variant: 'outline', size: 'sm' },
              () => 'Not in VEP',
            )
          : role
            ? h(
                resolveComponent('UBadge'),
                {
                  color: role.code === 'H' ? 'warning' : 'neutral',
                  variant: 'subtle',
                  size: 'sm',
                },
                () => role.description,
              )
            : null,
      ],
    ),
  ])
}

const columns: TableColumn<AidActivityItem>[] = [
  {
    accessorKey: 'sentOn',
    header: 'Sent On',
    cell: ({ row }) => formatDate(row.original.sentOn),
  },
  {
    id: 'sender',
    header: 'Sent By',
    cell: ({ row }) => renderParty(row.original.sender),
  },
  {
    id: 'receiver',
    header: 'Received By',
    cell: ({ row }) => renderParty(row.original.receiver),
  },
  {
    accessorKey: 'money',
    header: 'Money',
    cell: ({ row }) => {
      const amount = row.original.money
      return amount === 0
        ? '0'
        : new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD',
            maximumFractionDigits: 0,
          }).format(amount)
    },
  },
  {
    accessorKey: 'technology',
    header: 'Tech',
    cell: ({ row }) =>
      h('span', { class: 'font-mono' }, row.original.technology),
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) =>
      h(
        resolveComponent('UBadge'),
        {
          color: getStatusColor(row.original.status),
          variant: 'subtle',
        },
        () => toAidStatusDescription(row.original.status),
      ),
  },
  {
    accessorKey: 'dealId',
    header: 'Deal',
    cell: ({ row }) => {
      const dealId = row.original.dealId
      return dealId !== null
        ? h(
            resolveComponent('UBadge'),
            { color: 'success', variant: 'subtle' },
            () => `Deal #${dealId}`,
          )
        : h(
            resolveComponent('UBadge'),
            {
              color: 'warning',
              variant: 'subtle',
              icon: 'i-heroicons-exclamation-triangle',
            },
            () => 'No deal',
          )
    },
  },
  {
    accessorKey: 'reason',
    header: 'Reason',
    cell: ({ row }) =>
      h(
        'span',
        { class: 'text-gray-900 dark:text-white' },
        row.original.reason || '',
      ),
  },
]

// Helper function to convert aid status ID to description
const toAidStatusDescription = (status: number): string => {
  switch (status) {
    case 1:
      return 'Pending'
    case 2:
      return 'Approved'
    case 3:
      return 'Cancelled'
    case 4:
      return 'Expired'
    default:
      return 'Unknown'
  }
}

// Helper function to get status color for badges
const getStatusColor = (
  status: number,
): 'success' | 'error' | 'neutral' | 'warning' => {
  switch (status) {
    case 1:
      return 'warning' // Pending
    case 2:
      return 'success' // Approved
    case 3:
      return 'error' // Cancelled
    default:
      return 'neutral' // Expired
  }
}

const loadCurrentPage = async (): Promise<void> => {
  tableIsLoading.value = true

  try {
    await store.loadPage(limit.value, limit.value * (pageNumber.value - 1))
  } catch (error) {
    console.error('Failed to load aid activity:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'The current page failed to load',
      duration: 5000,
    })
  } finally {
    tableIsLoading.value = false
  }
}

// Changing the page triggers the page watcher, so only load directly when already on page 1
const reloadFromFirstPage = async (): Promise<void> => {
  if (pageNumber.value === 1) {
    await loadCurrentPage()
  } else {
    pageNumber.value = 1
  }
}

const openFilterSlideover = async (): Promise<void> => {
  await filterSlideover.open({ filters: filters.value }).result
  await reloadFromFirstPage()
}

watch(pageNumber, async () => {
  await loadCurrentPage()
})

watch([includeOnHold, unmatchedOnly], async () => {
  await reloadFromFirstPage()
})

onMounted(async () => {
  await reloadFromFirstPage()
})
</script>
