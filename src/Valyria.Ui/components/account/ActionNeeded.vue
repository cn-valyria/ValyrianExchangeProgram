<template>
  <UCard v-if="actions.length > 0" :ui="{ root: flaggedCount > 0 ? 'ring-2 ring-red-500/60' : 'ring-2 ring-amber-500/50' }">
    <template #header>
      <div class="flex items-center justify-between gap-4">
        <div>
          <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Action Needed Today</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400">
            Based on today's CN data. Deals not listed here don't need anything from you today.
          </p>
        </div>
        <UBadge :color="flaggedCount > 0 ? 'error' : 'warning'" size="lg" variant="subtle" class="shrink-0">
          {{ actions.length }} {{ actions.length === 1 ? 'deal' : 'deals' }}
        </UBadge>
      </div>
    </template>

    <div class="space-y-4">
      <UAlert
        v-if="flaggedCount > 0"
        color="error"
        variant="subtle"
        icon="i-heroicons-flag"
        :title="`${flaggedCount} of your deals ${flaggedCount === 1 ? 'is' : 'are'} flagged for inactivity`"
        description="You won't be matched with new partners while any of your deals are flagged. Flagged deals are cancelled 5 days after you start holding them up."
      />

      <ul class="divide-y divide-gray-200 dark:divide-gray-700">
        <li
          v-for="action in actions"
          :key="action.deal.id"
          class="flex flex-col sm:flex-row sm:items-center gap-3 py-4 first:pt-0 last:pb-0"
        >
          <div
            class="flex items-center justify-center w-10 h-10 rounded-full shrink-0"
            :class="iconClass(action)"
          >
            <UIcon :name="ACTION_ICONS[action.kind]" class="w-5 h-5" />
          </div>

          <div class="flex-1 min-w-0 space-y-1">
            <div class="flex flex-wrap items-center gap-2">
              <span class="font-semibold text-gray-900 dark:text-white">
                {{ action.title }} {{ action.preposition }}
                <a
                  :href="getNationLink(action.deal.partnerSlot.account)"
                  target="_blank"
                  class="text-fg-brand hover:underline"
                >{{ action.deal.partnerSlot.account.rulerName }} of {{ action.deal.partnerSlot.account.nationName }}</a>
              </span>
              <ChipAllianceAcronym :allianceName="action.deal.partnerSlot.account.allianceName" />
              <UBadge v-if="action.flagged" color="error" variant="solid" size="sm" icon="i-heroicons-flag">
                Flagged
              </UBadge>
            </div>
            <p class="text-sm" :class="action.flagged ? 'text-red-700 dark:text-red-300 font-medium' : 'text-gray-600 dark:text-gray-400'">
              {{ action.detail }}
            </p>
            <p class="text-xs text-gray-500 dark:text-gray-500">
              Slot #{{ action.deal.mySlot.slotNumber }}
              <template v-if="action.deal.partnerDiscord"> · Discord: {{ action.deal.partnerDiscord }}</template>
            </p>
          </div>

          <UButton
            v-if="action.kind === 'send'"
            :to="getAidFormLink(action.deal.partnerSlot.account, props.myNationId)"
            target="_blank"
            :color="action.flagged ? 'error' : 'primary'"
            icon="i-heroicons-paper-airplane"
            label="Open Aid Form"
            class="shrink-0 self-start sm:self-center"
          />
          <UButton
            v-else-if="action.kind === 'accept'"
            :to="AID_INFORMATION_LINK"
            target="_blank"
            :color="action.flagged ? 'error' : 'primary'"
            icon="i-heroicons-inbox-arrow-down"
            label="Accept Aid"
            class="shrink-0 self-start sm:self-center"
          />
        </li>
      </ul>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { AID_INFORMATION_LINK, getAidFormLink, getDealActions, getNationLink, type DealAction, type DealActionKind } from '~/infrastructure/deals'
import type { DealDetail } from '~/stores/account'

interface Props {
  deals: DealDetail[]
  myNationId: number
}

const props = withDefaults(defineProps<Props>(), {
  deals: () => [],
  myNationId: 0
})

const ACTION_ICONS: Record<DealActionKind, string> = {
  send: 'i-heroicons-paper-airplane',
  accept: 'i-heroicons-inbox-arrow-down',
  receive: 'i-heroicons-arrow-down-tray',
  freeSlot: 'i-heroicons-no-symbol',
  outOfTurn: 'i-heroicons-arrows-right-left'
}

const actions = computed(() => getDealActions(props.deals))

const flaggedCount = computed(() => actions.value.filter((action) => action.flagged).length)

const iconClass = (action: DealAction): string => {
  if (action.flagged) return 'bg-red-100 text-red-600 dark:bg-red-900/40 dark:text-red-300'
  if (action.kind === 'receive') return 'bg-blue-100 text-blue-600 dark:bg-blue-900/40 dark:text-blue-300'
  return 'bg-amber-100 text-amber-600 dark:bg-amber-900/40 dark:text-amber-300'
}
</script>
