<template>
  <UCard v-if="warnings.length > 0" :ui="{ root: 'ring-2 ring-amber-500/50' }">
    <template #header>
      <div class="flex items-center justify-between gap-4">
        <div>
          <h2 class="text-xl font-semibold text-gray-900 dark:text-white">
            Waiting for an Aid Slot
          </h2>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            These deals are held up by aid outside VEP that has already been
            accepted. It can't be cancelled in CN, so there's nothing to do
            until a slot frees.
          </p>
        </div>
        <UBadge color="warning" size="lg" variant="subtle" class="shrink-0">
          {{ warnings.length }} {{ warnings.length === 1 ? 'deal' : 'deals' }}
        </UBadge>
      </div>
    </template>

    <ul class="divide-y divide-gray-200 dark:divide-gray-700">
      <li
        v-for="warning in warnings"
        :key="warning.deal.id"
        class="py-4 first:pt-0 last:pb-0"
      >
        <div class="flex flex-col sm:flex-row sm:items-center gap-3">
          <div
            class="flex items-center justify-center w-10 h-10 rounded-full shrink-0 bg-amber-100 text-amber-600 dark:bg-amber-900/40 dark:text-amber-300"
          >
            <UIcon name="i-heroicons-clock" class="w-5 h-5" />
          </div>

          <div class="flex-1 min-w-0 space-y-1">
            <div class="flex flex-wrap items-center gap-2">
              <span class="font-semibold text-gray-900 dark:text-white">
                {{ warning.title }} {{ warning.preposition }}
                <a
                  :href="getNationLink(warning.deal.partnerSlot.account)"
                  target="_blank"
                  class="text-fg-brand hover:underline"
                  >{{ warning.deal.partnerSlot.account.rulerName }} of
                  {{ warning.deal.partnerSlot.account.nationName }}</a
                >
              </span>
              <ChipAllianceAcronym
                :alliance-name="warning.deal.partnerSlot.account.allianceName"
              />
              <UBadge color="warning" variant="subtle" size="sm">
                Slot frees {{ formatDate(warning.deal.slotFreesOn) }}
              </UBadge>
              <UButton
                v-if="warning.flagged"
                color="warning"
                variant="soft"
                size="xs"
                icon="i-heroicons-flag"
                :trailing-icon="
                  expandedDealId === warning.deal.id
                    ? 'i-heroicons-chevron-up'
                    : 'i-heroicons-chevron-down'
                "
                label="Flagged"
                :aria-expanded="expandedDealId === warning.deal.id"
                :aria-label="
                  expandedDealId === warning.deal.id
                    ? 'Hide why this deal was flagged'
                    : 'Show why this deal was flagged'
                "
                @click="toggleFlag(warning.deal.id)"
              />
            </div>
            <p class="text-sm text-amber-800 dark:text-amber-200">
              {{ warning.detail }}
            </p>
            <p class="text-xs text-gray-500 dark:text-gray-500">
              Slot #{{ warning.deal.mySlot.slotNumber }}
              <template v-if="warning.deal.partnerDiscord">
                · Discord: {{ warning.deal.partnerDiscord }}</template
              >
            </p>
          </div>
        </div>

        <Transition
          enter-active-class="transition duration-150 ease-out"
          enter-from-class="opacity-0 -translate-y-1"
          leave-active-class="transition duration-100 ease-in"
          leave-to-class="opacity-0 -translate-y-1"
        >
          <DealFlagDetails
            v-if="warning.flagged && expandedDealId === warning.deal.id"
            :account-id="props.accountId"
            :deal-id="warning.deal.id"
            class="mt-3 sm:ml-13 rounded-md bg-amber-50/60 dark:bg-amber-950/20 ring-1 ring-amber-200 dark:ring-amber-900/50 p-4"
          />
        </Transition>
      </li>
    </ul>
  </UCard>
</template>

<script setup lang="ts">
import DealFlagDetails from '~/components/account/DealFlagDetails.vue'
import { getDealWarnings, getNationLink } from '~/infrastructure/deals'
import type { DealDetail } from '~/stores/account'

interface Props {
  deals: DealDetail[]
  accountId: number
}

const props = withDefaults(defineProps<Props>(), {
  deals: () => [],
  accountId: 0,
})

// One deal's flag details open at a time
const expandedDealId = ref<number | null>(null)

const warnings = computed(() => getDealWarnings(props.deals))

const formatDate = (date: string | null): string =>
  date ? date.split('T')[0] : 'soon'

const toggleFlag = (dealId: number): void => {
  expandedDealId.value = expandedDealId.value === dealId ? null : dealId
}
</script>
