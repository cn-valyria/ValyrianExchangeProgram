<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">
          Deal History
        </h2>
        <UBadge color="primary" size="lg" variant="subtle">
          {{ dealHistory.totalCount }}
          {{ dealHistory.totalCount === 1 ? 'deal' : 'deals' }}
        </UBadge>
      </div>
    </template>

    <div class="space-y-4">
      <UAlert
        color="info"
        icon="i-heroicons-information-circle"
        title="About Your Deal History"
        description="Every deal your nation has been part of, including deals that have ended. Expand a deal to see the aid exchanged under it. Please contact VEP Management if you have any concerns about your deal history."
      />

      <UTable
        v-model:expanded="expanded"
        :data="dealHistory.results"
        :columns="columns"
        :loading="tableIsLoading"
      >
        <template #expanded="{ row }">
          <div class="space-y-3 px-2 py-1">
            <p
              v-if="row.original.cancellationReason"
              class="text-sm text-gray-600 dark:text-gray-400"
            >
              <span class="font-semibold">Ended:</span>
              {{ row.original.cancellationReason }}
            </p>

            <div
              v-if="activityLoading[row.original.id]"
              class="flex items-center gap-2 text-sm text-gray-500"
            >
              <UIcon
                name="i-heroicons-arrow-path"
                class="w-4 h-4 animate-spin"
              />
              Loading aid activity...
            </div>
            <p
              v-else-if="(dealActivity[row.original.id] ?? []).length === 0"
              class="text-sm text-gray-500"
            >
              No aid has been recorded for this deal.
            </p>
            <UTable
              v-else
              :data="dealActivity[row.original.id]"
              :columns="activityColumns"
            />
          </div>
        </template>
      </UTable>

      <!-- Pagination -->
      <div class="flex justify-between items-center">
        <div class="text-sm text-gray-600 dark:text-gray-400">
          Showing
          {{ Math.min((currentPage - 1) * limit + 1, dealHistory.totalCount) }}
          to {{ Math.min(currentPage * limit, dealHistory.totalCount) }} of
          {{ dealHistory.totalCount }} results
        </div>
        <UPagination
          v-model:page="currentPage"
          :total="dealHistory.totalCount"
          :items-per-page="limit"
        />
      </div>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import type { TableColumn, TableRow } from '@nuxt/ui'
import type { BadgeColor } from '~/infrastructure/deals'
import {
  DealLegOutcome,
  DealStatus,
  type DealActivity,
  type DealHistoryItem,
} from '~/stores/account'
import type { NationSimplified } from '~/stores/shared/nationSimplified'

interface Props {
  accountId: number
}

const props = withDefaults(defineProps<Props>(), {
  accountId: () => 0,
})

const toast = useToast()

const accountStore = useAccountStore()
const { dealHistory, dealActivity } = storeToRefs(accountStore)

const tableIsLoading = ref<boolean>(false)
const currentPage = ref<number>(1)
const limit = ref<number>(10)
const expanded = ref<Record<string, boolean>>({})
const activityLoading = ref<Record<number, boolean>>({})

const formatDate = (date: string | null): string =>
  date ? date.split('T')[0] : '—'

const LEG_OUTCOMES: Record<
  DealLegOutcome,
  { label: string; color: BadgeColor }
> = {
  [DealLegOutcome.Pending]: { label: 'Pending', color: 'info' },
  [DealLegOutcome.Accepted]: { label: 'Accepted', color: 'success' },
  [DealLegOutcome.Cancelled]: { label: 'Cancelled', color: 'error' },
  [DealLegOutcome.Expired]: { label: 'Expired', color: 'error' },
}

const columns: TableColumn<DealHistoryItem>[] = [
  {
    id: 'expand',
    cell: ({ row }) =>
      h(resolveComponent('UButton'), {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-heroicons-chevron-down',
        square: true,
        'aria-label': 'Show aid activity',
        ui: {
          leadingIcon: [
            'transition-transform',
            row.getIsExpanded() ? 'duration-200 rotate-180' : '',
          ],
        },
        onClick: () => toggleRow(row),
      }),
  },
  {
    accessorKey: 'partner',
    header: 'Partner',
    cell: ({ row }) => {
      const partner = row.getValue<NationSimplified>('partner')
      return `${partner.rulerName} of ${partner.nationName}`
    },
  },
  {
    id: 'alliance',
    header: 'Alliance',
    cell: ({ row }) =>
      h(resolveComponent('ChipAllianceAcronym'), {
        allianceName: row.original.partner.allianceName,
      }),
  },
  {
    accessorKey: 'mySlotNumber',
    header: 'Slot',
    cell: ({ row }) => `#${row.original.mySlotNumber}`,
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => {
      const isActive = row.original.status === DealStatus.Active
      return h(
        resolveComponent('UBadge'),
        {
          color: isActive ? 'success' : 'neutral',
          variant: 'subtle',
        },
        () => (isActive ? 'Active' : 'Ended'),
      )
    },
  },
  {
    accessorKey: 'createdOn',
    header: 'Started',
    cell: ({ row }) => formatDate(row.original.createdOn),
  },
  {
    accessorKey: 'cancelledOn',
    header: 'Ended',
    cell: ({ row }) => formatDate(row.original.cancelledOn),
  },
  {
    accessorKey: 'aidSentCount',
    header: 'Aid Sent',
  },
  {
    accessorKey: 'aidReceivedCount',
    header: 'Aid Received',
  },
  {
    accessorKey: 'lastActivityDate',
    header: 'Last Aid',
    cell: ({ row }) => formatDate(row.original.lastActivityDate),
  },
]

const activityColumns: TableColumn<DealActivity>[] = [
  {
    accessorKey: 'activityDate',
    header: 'Date',
    cell: ({ row }) => formatDate(row.original.activityDate),
  },
  {
    accessorKey: 'isOutgoing',
    header: 'Direction',
    cell: ({ row }) =>
      h(
        resolveComponent('UBadge'),
        {
          color: row.original.isOutgoing ? 'primary' : 'secondary',
          variant: 'subtle',
        },
        () => (row.original.isOutgoing ? 'Sent' : 'Received'),
      ),
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
          }).format(amount)
    },
  },
  {
    accessorKey: 'technology',
    header: 'Tech',
  },
  {
    accessorKey: 'outcome',
    header: 'Outcome',
    cell: ({ row }) => {
      const outcome = LEG_OUTCOMES[row.original.outcome]
      return h(
        resolveComponent('UBadge'),
        { color: outcome.color, variant: 'subtle' },
        () => outcome.label,
      )
    },
  },
]

const toggleRow = async (row: TableRow<DealHistoryItem>): Promise<void> => {
  const isExpanding = !row.getIsExpanded()
  row.toggleExpanded()

  // Activity is loaded once per deal, the first time it's expanded
  const dealId = row.original.id
  if (!isExpanding || dealActivity.value[dealId] !== undefined) {
    return
  }

  activityLoading.value[dealId] = true
  try {
    await accountStore.loadDealActivity(props.accountId, dealId)
  } catch (error) {
    console.error('Failed to load deal activity:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description:
        'Failed to load aid activity for this deal. Please try again.',
      duration: 5000,
    })
  } finally {
    activityLoading.value[dealId] = false
  }
}

// Change page handler
const changePage = async (newPageNumber: number): Promise<void> => {
  tableIsLoading.value = true
  currentPage.value = newPageNumber
  expanded.value = {}

  try {
    await accountStore.loadDealHistory(
      props.accountId,
      limit.value,
      limit.value * (currentPage.value - 1),
    )
  } catch (error) {
    console.error('Failed to load deal history:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load deal history. Please try again.',
      duration: 5000,
    })
  } finally {
    tableIsLoading.value = false
  }
}

watch(currentPage, async (newPage) => {
  await changePage(newPage)
})
</script>
