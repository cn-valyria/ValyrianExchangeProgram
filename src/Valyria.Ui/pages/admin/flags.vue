<template>
  <UContainer>
    <div class="py-8 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">
          Flagged Deals
        </h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">
          Why VEP flagged each account, what happens next if nobody steps in,
          and the ways to resolve it
        </p>
      </div>

      <!-- Summary -->
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <UCard v-for="stat in stats" :key="stat.label">
          <div class="text-sm text-gray-500 dark:text-gray-400">
            {{ stat.label }}
          </div>
          <div class="text-2xl font-bold text-gray-900 dark:text-white">
            {{ stat.value }}
          </div>
          <div class="text-xs text-gray-500 dark:text-gray-400">
            {{ stat.detail }}
          </div>
        </UCard>
      </div>

      <UAlert
        color="info"
        variant="subtle"
        icon="i-heroicons-information-circle"
        title="How flags work"
        description="VEP checks deals once a day, when the day's CN aid file loads. An account holding up a deal gets a 1-day warning, which clears itself once they stop. After 5 days the deal is cancelled and the warning becomes a 5-day flag. An aid offer that's cancelled or expires unaccepted cancels the deal and flags both accounts. Flagged accounts aren't matched with new partners until all their flags are resolved."
      />

      <!-- Filters -->
      <div class="space-y-3">
        <UInput
          v-model="searchQuery"
          placeholder="Search by nation, ruler, alliance, role, or deal #..."
          icon="i-heroicons-magnifying-glass"
          class="w-full sm:max-w-md"
        />
        <UTabs
          v-model="view"
          :items="viewTabs"
          :content="false"
          class="w-full sm:w-auto"
        />
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div class="text-sm text-gray-600 dark:text-gray-400">
            {{ resultSummary }}
          </div>
          <UButton
            :label="expandAll ? 'Collapse all' : 'Expand all'"
            :icon="
              expandAll
                ? 'i-heroicons-arrows-pointing-in'
                : 'i-heroicons-arrows-pointing-out'
            "
            color="neutral"
            variant="outline"
            size="sm"
            @click="toggleExpandAll()"
          />
        </div>
      </div>

      <!-- Flags -->
      <div v-if="loading" class="py-12 text-center text-gray-500">
        <UIcon
          name="i-heroicons-arrow-path"
          class="w-8 h-8 animate-spin text-primary-500"
        />
      </div>
      <UCard v-else-if="visibleFlags.length === 0">
        <div class="py-6 text-center text-gray-500 dark:text-gray-400">
          {{
            flags.length === 0
              ? 'No open flags. Every account is clear.'
              : 'No flags match this search.'
          }}
        </div>
      </UCard>
      <UCard v-else :ui="{ body: 'p-0 sm:p-0' }">
        <div
          :class="[
            'hidden px-4 py-2 text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 border-b border-gray-200 dark:border-gray-800',
            FLAG_ROW_GRID,
          ]"
        >
          <div class="pl-9">Flag</div>
          <div>Flagged Account</div>
          <div>Partner</div>
          <div>Deal</div>
          <div>If Left Alone</div>
          <div />
        </div>
        <div class="divide-y divide-gray-200 dark:divide-gray-800">
          <FlagRow
            v-for="flag in visibleFlags"
            :key="flag.id"
            :data-flag-id="flag.id"
            :flag="flag"
            :today="today"
            :read-only="isReadOnly"
            :expanded="isExpanded(flag)"
            @toggle="toggleFlag"
            @resolve="openResolveFlag"
          />
        </div>
      </UCard>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import {
  FLAG_ROW_GRID,
  findPartnerFlag,
  getCnToday,
  isOnActiveDeal,
  isStillHoldingUpDeal,
  matchesFlagSearch,
  matchesFlagView,
  sortFlags,
  type FlagDealView,
} from '~/infrastructure/flags'
import { useAdminDealsStore, type DealFlag } from '~/stores/adminDeals'
import { useAuthStore } from '~/stores/auth'
import FlagRow from '~/components/admin/FlagRow.vue'
import ResolveFlagDialog from '~/components/admin/ResolveFlagDialog.vue'

const toast = useToast()
const overlay = useOverlay()
const route = useRoute()

const resolveModal = overlay.create(ResolveFlagDialog)

const adminDealsStore = useAdminDealsStore()
const authStore = useAuthStore()

const { flags } = storeToRefs(adminDealsStore)
const { loggedInUser } = storeToRefs(authStore)

// Other pages link here pre-filtered, e.g. ?search=%2312 for deal #12
const searchQuery = ref<string>(
  typeof route.query.search === 'string' ? route.query.search : '',
)
const view = ref<FlagDealView>('all')
const loading = ref<boolean>(true)
const today = getCnToday()

// Check if user has read-only access
const isReadOnly = computed(() => {
  return (
    loggedInUser.value?.roles?.some(
      (role) => role.name === 'Admin (Read-only)',
    ) ?? false
  )
})

const plural = (count: number, word: string): string =>
  `${count} ${word}${count === 1 ? '' : 's'}`

const countDeals = (list: DealFlag[]): number =>
  new Set(list.map((flag) => flag.dealId)).size

const stats = computed(() => {
  const active = flags.value.filter(isOnActiveDeal)
  const cancelled = flags.value.filter((flag) => !isOnActiveDeal(flag))
  const accounts = new Set(
    flags.value.map((flag) => flag.flaggedAccount.accountId),
  ).size
  return [
    {
      label: 'Flagged accounts',
      value: accounts,
      detail: `${plural(flags.value.length, 'open flag')} across ${plural(countDeals(flags.value), 'deal')}`,
    },
    {
      label: 'On active deals',
      value: countDeals(active),
      detail: `${active.filter(isStillHoldingUpDeal).length} still held up, the rest clearing themselves`,
    },
    {
      label: 'Deals cancelled by a flag',
      value: countDeals(cancelled),
      detail: 'Waiting for an admin to resolve or restore',
    },
  ]
})

const searchedFlags = computed(() =>
  flags.value.filter((flag) => matchesFlagSearch(flag, searchQuery.value)),
)

const viewTabs = computed(() => {
  const count = (target: FlagDealView) =>
    searchedFlags.value.filter((flag) => matchesFlagView(flag, target)).length
  return [
    { value: 'all', label: `All (${count('all')})` },
    { value: 'active', label: `Active Deals (${count('active')})` },
    { value: 'cancelled', label: `Cancelled Deals (${count('cancelled')})` },
  ]
})

const visibleFlags = computed(() =>
  sortFlags(
    searchedFlags.value.filter((flag) => matchesFlagView(flag, view.value)),
  ),
)

const resultSummary = computed(() => {
  const accounts = new Set(
    visibleFlags.value.map((flag) => flag.flaggedAccount.accountId),
  ).size
  return `Showing ${plural(visibleFlags.value.length, 'flag')} on ${plural(accounts, 'account')}, most urgent first`
})

// Rows start collapsed, or all open for admins who prefer working through them that way. The
// choice is remembered in this browser; rows toggled by hand are the exceptions to it
const EXPAND_ALL_KEY = 'vep.adminFlags.expandAll'
const expandAll = ref<boolean>(false)
const toggledIds = ref<Set<number>>(new Set())

const isExpanded = (flag: DealFlag): boolean =>
  expandAll.value !== toggledIds.value.has(flag.id)

const toggleFlag = (flag: DealFlag): void => {
  const toggled = new Set(toggledIds.value)
  if (!toggled.delete(flag.id)) toggled.add(flag.id)
  toggledIds.value = toggled
}

const toggleExpandAll = (): void => {
  expandAll.value = !expandAll.value
  toggledIds.value = new Set()
  try {
    localStorage.setItem(EXPAND_ALL_KEY, String(expandAll.value))
  } catch {
    // Storage can be blocked; the choice just won't be remembered
  }
}

const loadFlags = async (): Promise<void> => {
  try {
    await adminDealsStore.loadFlags()
  } catch (error) {
    console.error('Failed to load flags:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load flagged deals. Please try again.',
      duration: 5000,
    })
  }
}

const openResolveFlag = async (flag: DealFlag): Promise<void> => {
  const resolved = await resolveModal.open({
    flag,
    partnerFlag: findPartnerFlag(flag, flags.value),
  }).result
  // Other flags' open counts and restore checks may have changed too
  if (resolved) await loadFlags()
}

onMounted(async () => {
  try {
    expandAll.value = localStorage.getItem(EXPAND_ALL_KEY) === 'true'
  } catch {
    // Storage can be blocked; start collapsed
  }
  await loadFlags()
  loading.value = false
})
</script>
