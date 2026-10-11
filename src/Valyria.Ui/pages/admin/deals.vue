<template>
  <UContainer>
    <div class="py-8 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">VEP Deals</h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">View and manage active deals, the accounts waiting to be matched, and flagged deals</p>
      </div>

      <UTabs v-model="activeTab" :items="tabs" class="w-full">
        <!-- Active Deals Tab -->
        <template #active>
          <UCard>
            <div class="space-y-4">
              <div class="flex justify-between items-center gap-4">
                <UInput
                  v-model="searchQuery"
                  placeholder="Search by nation, ruler, or alliance..."
                  icon="i-heroicons-magnifying-glass"
                  class="w-full max-w-md"
                />
                <UButton
                  v-if="!isReadOnly"
                  color="success"
                  icon="i-heroicons-plus"
                  label="Create Deal"
                  @click="openCreateDeal()"
                />
              </div>

              <UTable :data="activeDeals.results" :columns="displayDealColumns" :loading="dealsLoading" />

              <!-- Pagination -->
              <div class="flex justify-between items-center">
                <div class="text-sm text-gray-600 dark:text-gray-400">
                  Showing {{ Math.min((currentPage - 1) * limit + 1, activeDeals.totalCount) }} to {{ Math.min(currentPage * limit, activeDeals.totalCount) }} of {{ activeDeals.totalCount }} results
                </div>
                <UPagination
                  v-model:page="currentPage"
                  :total="activeDeals.totalCount"
                  :items-per-page="limit"
                />
              </div>
            </div>
          </UCard>
        </template>

        <!-- Wait Queue Tab -->
        <template #queue>
          <UCard>
            <div class="space-y-4">
              <UAlert
                color="info"
                icon="i-heroicons-information-circle"
                title="About the Wait Queue"
                description="Accounts with open slots, in the order the matcher will try to fill them. Higher priority scores are matched first."
              />
              <UTable :data="waitQueue" :columns="queueColumns" :loading="queueLoading" />
            </div>
          </UCard>
        </template>

        <!-- Flags Tab -->
        <template #flags>
          <UCard>
            <div class="space-y-4">
              <UAlert
                color="info"
                icon="i-heroicons-information-circle"
                title="About Flags"
                description="Accounts are flagged when they hold up a deal: not sending when it's their turn and both nations have a free slot, filling their aid slots with non-VEP aid, or sending aid out of turn. 1-day flags clear themselves once the account stops holding up the deal. 5-day flags cancel the deal. Aid Failed flags cancel the deal when an offer is cancelled or expires unaccepted, and flag both accounts because VEP can't tell who caused it. Flagged accounts aren't matched with new partners until their flags are resolved."
              />
              <UTable :data="flags" :columns="displayFlagColumns" :loading="flagsLoading" />
            </div>
          </UCard>
        </template>
      </UTabs>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { storeToRefs } from 'pinia'
import { watchDebounced } from '@vueuse/core'
import { describeAid, getAdminDealStatus } from '~/infrastructure/deals'
import type { SlotDetail } from '~/stores/account'
import { useAdminAccountsStore } from '~/stores/adminAccounts'
import { DealFlagReason, DealFlagType, useAdminDealsStore, type AdminDeal, type DealFlag, type WaitQueueEntry } from '~/stores/adminDeals'
import { useAuthStore } from '~/stores/auth'
import CancelDealDialog from '~/components/admin/CancelDealDialog.vue'
import CreateDealDialog from '~/components/admin/CreateDealDialog.vue'
import ResolveFlagDialog from '~/components/admin/ResolveFlagDialog.vue'

const toast = useToast()
const overlay = useOverlay()

const cancelModal = overlay.create(CancelDealDialog)
const createModal = overlay.create(CreateDealDialog)
const resolveModal = overlay.create(ResolveFlagDialog)

const adminDealsStore = useAdminDealsStore()
const adminAccountsStore = useAdminAccountsStore()
const authStore = useAuthStore()

const { activeDeals, waitQueue, flags } = storeToRefs(adminDealsStore)
const { accounts } = storeToRefs(adminAccountsStore)
const { loggedInUser } = storeToRefs(authStore)

const activeTab = ref<string>('active')
const searchQuery = ref<string>('')
const currentPage = ref<number>(1)
const limit = ref<number>(25)
const dealsLoading = ref<boolean>(false)
const queueLoading = ref<boolean>(false)
const flagsLoading = ref<boolean>(false)

// Check if user has read-only access
const isReadOnly = computed(() => {
  return loggedInUser.value?.roles?.some((role) => role.name === "Admin (Read-only)") ?? false
})

const tabs = computed(() => [
  { value: 'active', label: `Active Deals (${activeDeals.value.totalCount})`, slot: 'active' },
  { value: 'queue', label: `Wait Queue (${waitQueue.value.length})`, slot: 'queue' },
  { value: 'flags', label: `Flags (${flags.value.length})`, slot: 'flags' }
])

const formatDate = (date: string | null): string => date ? date.split('T')[0] : '—'

const FLAG_REASON_LABELS: Record<DealFlagReason, string> = {
  [DealFlagReason.AwaitingSend]: "Didn't send on their turn",
  [DealFlagReason.SenderSlotsFull]: 'Slots full (sender)',
  [DealFlagReason.ReceiverSlotsFull]: 'Slots full (receiver)',
  [DealFlagReason.OutOfSequenceAid]: 'Sent aid out of turn',
  [DealFlagReason.AidCancelled]: 'Aid offer cancelled',
  [DealFlagReason.AidExpired]: 'Aid offer expired unaccepted'
}

const renderParty = (slot: SlotDetail) => h('div', { class: 'flex items-center gap-2' }, [
  h(resolveComponent('ChipAllianceAcronym'), { allianceName: slot.account.allianceName }),
  h('div', [
    h('div', { class: 'font-medium text-gray-900 dark:text-white' }, slot.account.rulerName),
    h('div', { class: 'text-sm text-gray-500 dark:text-gray-400' }, `${slot.account.nationName} · Slot #${slot.slotNumber}`)
  ])
])

const dealColumns: TableColumn<AdminDeal>[] = [
  {
    accessorKey: 'id',
    header: 'Deal',
    cell: ({ row }) => `#${row.original.id}`
  },
  {
    id: 'slot1',
    header: 'Account A',
    cell: ({ row }) => renderParty(row.original.slot1)
  },
  {
    id: 'slot2',
    header: 'Account B',
    cell: ({ row }) => renderParty(row.original.slot2)
  },
  {
    id: 'exchange',
    header: 'Exchange (per 10 days)',
    cell: ({ row }) => {
      const { slot1, slot2 } = row.original
      return h('div', { class: 'text-sm' }, [
        h('div', `A sends ${describeAid(slot1.techSentCycles, slot1.cashSentCycles)}`),
        h('div', `B sends ${describeAid(slot2.techSentCycles, slot2.cashSentCycles)}`)
      ])
    }
  },
  {
    accessorKey: 'createdOn',
    header: 'Started',
    cell: ({ row }) => formatDate(row.original.createdOn)
  },
  {
    accessorKey: 'lastActivityDate',
    header: 'Last Aid',
    cell: ({ row }) => formatDate(row.original.lastActivityDate)
  },
  {
    id: 'heldUpBy',
    header: 'Held Up By',
    cell: ({ row }) => {
      const deal = row.original
      const account = [deal.slot1.account, deal.slot2.account].find((a) => a.accountId === deal.responsibleAccountId)
      if (!account) return '—'
      return h('div', [
        h('div', { class: 'font-medium text-gray-900 dark:text-white' }, account.rulerName),
        h('div', { class: 'text-sm text-gray-500 dark:text-gray-400' },
          `${deal.daysResponsible ?? 0}d · cancels ${formatDate(deal.cancelsOn)}`)
      ])
    }
  },
  {
    id: 'status',
    header: 'Status',
    cell: ({ row }) => {
      const status = getAdminDealStatus(row.original)
      const badge = h(resolveComponent('UBadge'), { color: status.color, variant: 'subtle' }, () => status.label)
      const flag = row.original.hasOpenFlag
        ? h(resolveComponent('UBadge'), { color: 'error', variant: 'solid', icon: 'i-heroicons-flag' }, () => 'Flagged')
        : null
      return h(resolveComponent('UTooltip'), { text: status.detail }, () => h('div', { class: 'flex gap-1' }, [badge, flag]))
    }
  },
  {
    id: 'actions',
    header: 'Actions',
    cell: ({ row }) => h(resolveComponent('UButton'), {
      label: 'Cancel',
      color: 'error',
      variant: 'soft',
      size: 'xs',
      onClick: () => openCancelDeal(row.original)
    })
  }
]

const queueColumns: TableColumn<WaitQueueEntry>[] = [
  {
    id: 'rank',
    header: '#',
    cell: ({ row }) => row.index + 1
  },
  {
    id: 'account',
    header: 'Account',
    cell: ({ row }) => {
      const account = row.original.account
      return h('div', { class: 'flex items-center gap-2' }, [
        h(resolveComponent('ChipAllianceAcronym'), { allianceName: account.allianceName }),
        h('div', [
          h('div', { class: 'font-medium text-gray-900 dark:text-white' }, account.rulerName),
          h('div', { class: 'text-sm text-gray-500 dark:text-gray-400' }, account.nationName)
        ])
      ])
    }
  },
  {
    accessorKey: 'slotsNeeded',
    header: 'Open Slots'
  },
  {
    accessorKey: 'enteredQueueOn',
    header: 'Waiting Since',
    cell: ({ row }) => formatDate(row.original.enteredQueueOn)
  },
  {
    accessorKey: 'nextSlotOpensOn',
    header: 'Next Slot Opens',
    cell: ({ row }) => formatDate(row.original.nextSlotOpensOn)
  },
  {
    accessorKey: 'priorityScore',
    header: 'Priority',
    cell: ({ row }) => h('span', { class: 'font-mono' }, row.original.priorityScore.toFixed(2))
  }
]

const flagColumns: TableColumn<DealFlag>[] = [
  {
    accessorKey: 'dealId',
    header: 'Deal',
    cell: ({ row }) => `#${row.original.dealId}`
  },
  {
    accessorKey: 'flagType',
    header: 'Type',
    cell: ({ row }) => {
      const flagType = row.original.flagType
      const label = flagType === DealFlagType.FiveDayReplacement
        ? '5-Day Replacement'
        : flagType === DealFlagType.AidFailed ? 'Aid Failed' : '1-Day Inactive'
      return h(resolveComponent('UBadge'), {
        color: flagType === DealFlagType.OneDayInactive ? 'warning' : 'error',
        variant: 'subtle'
      }, () => label)
    }
  },
  {
    accessorKey: 'flagReason',
    header: 'Reason',
    cell: ({ row }) => row.original.flagReason === null ? '—' : FLAG_REASON_LABELS[row.original.flagReason]
  },
  {
    id: 'flaggedAccount',
    header: 'Flagged Account',
    cell: ({ row }) => `${row.original.flaggedAccount.rulerName} of ${row.original.flaggedAccount.nationName}`
  },
  {
    id: 'innocentAccount',
    header: 'Partner',
    cell: ({ row }) => `${row.original.innocentAccount.rulerName} of ${row.original.innocentAccount.nationName}`
  },
  {
    accessorKey: 'flaggedOn',
    header: 'Flagged On',
    cell: ({ row }) => formatDate(row.original.flaggedOn)
  },
  {
    id: 'actions',
    header: 'Actions',
    cell: ({ row }) => h(resolveComponent('UButton'), {
      label: 'Resolve',
      color: 'success',
      variant: 'soft',
      size: 'xs',
      onClick: () => openResolveFlag(row.original)
    })
  }
]

// Read-only admins can see everything but take no actions
const displayDealColumns = computed(() =>
  isReadOnly.value ? dealColumns.filter((col) => col.id !== 'actions') : dealColumns
)

const displayFlagColumns = computed(() =>
  isReadOnly.value ? flagColumns.filter((col) => col.id !== 'actions') : flagColumns
)

const loadDealsPage = async (page: number): Promise<void> => {
  dealsLoading.value = true
  try {
    await adminDealsStore.loadActiveDeals(searchQuery.value.trim(), limit.value, limit.value * (page - 1))
  } catch (error) {
    console.error('Failed to load active deals:', error)
    toast.add({ color: 'error', title: 'Error', description: 'Failed to load active deals. Please try again.', duration: 5000 })
  } finally {
    dealsLoading.value = false
  }
}

const loadQueue = async (): Promise<void> => {
  queueLoading.value = true
  try {
    await adminDealsStore.loadWaitQueue()
  } catch (error) {
    console.error('Failed to load wait queue:', error)
    toast.add({ color: 'error', title: 'Error', description: 'Failed to load the wait queue. Please try again.', duration: 5000 })
  } finally {
    queueLoading.value = false
  }
}

const loadFlags = async (): Promise<void> => {
  flagsLoading.value = true
  try {
    await adminDealsStore.loadFlags()
  } catch (error) {
    console.error('Failed to load flags:', error)
    toast.add({ color: 'error', title: 'Error', description: 'Failed to load flagged deals. Please try again.', duration: 5000 })
  } finally {
    flagsLoading.value = false
  }
}

const openCancelDeal = async (deal: AdminDeal): Promise<void> => {
  const cancelled = await cancelModal.open({ deal }).result
  if (cancelled) {
    await Promise.all([loadDealsPage(currentPage.value), loadQueue(), loadFlags()])
  }
}

const openCreateDeal = async (): Promise<void> => {
  if (accounts.value.length === 0) {
    try {
      await adminAccountsStore.loadAllAccounts()
    } catch (error) {
      console.error('Failed to load accounts:', error)
      toast.add({ color: 'error', title: 'Error', description: 'Failed to load accounts. Please try again.', duration: 5000 })
      return
    }
  }

  const created = await createModal.open({ accounts: accounts.value }).result
  if (created) {
    await Promise.all([loadDealsPage(currentPage.value), loadQueue()])
  }
}

const openResolveFlag = async (flag: DealFlag): Promise<void> => {
  await resolveModal.open({ flag }).result
}

watch(currentPage, async (newPage) => {
  await loadDealsPage(newPage)
})

watchDebounced(searchQuery, async () => {
  if (currentPage.value === 1) {
    await loadDealsPage(1)
  } else {
    // The page watcher reloads
    currentPage.value = 1
  }
}, { debounce: 300 })

onMounted(async () => {
  await Promise.all([loadDealsPage(1), loadQueue(), loadFlags()])
})
</script>
