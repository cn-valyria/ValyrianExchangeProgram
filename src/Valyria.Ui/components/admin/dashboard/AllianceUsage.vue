<template>
  <div>
    <div class="mb-3">
      <p class="text-lg font-semibold text-gray-900 dark:text-white">
        Alliances
      </p>
      <p class="text-sm text-gray-500 dark:text-gray-400">
        Each member alliance's slots and deals. A deal between two alliances
        counts for both. Open an alliance to see the pairings it's in.
      </p>
    </div>

    <AdminDashboardHealthLegend class="mb-4" />

    <p
      v-if="alliances.length === 0"
      class="text-sm text-gray-500 dark:text-gray-400"
    >
      No alliances yet.
    </p>

    <div class="divide-y divide-gray-200 dark:divide-gray-700">
      <div
        v-for="alliance in alliances"
        :key="alliance.allianceName"
        data-alliance
        class="py-3 first:pt-0 last:pb-0"
      >
        <button
          type="button"
          class="w-full text-left grid grid-cols-[1fr_auto] md:grid-cols-[minmax(12rem,1.5fr)_1fr_1fr_1fr_auto] gap-x-4 gap-y-1 items-center"
          :aria-expanded="expanded.has(alliance.allianceName)"
          @click="toggle(alliance.allianceName)"
        >
          <div class="flex items-center gap-2 min-w-0">
            <chip-alliance-acronym :alliance-name="alliance.allianceName" />
            <div class="min-w-0">
              <p class="font-medium text-gray-900 dark:text-white truncate">
                {{ alliance.allianceName }}
              </p>
              <p class="text-xs text-gray-500 dark:text-gray-400">
                {{ plural(alliance.accounts, 'account') }}
              </p>
            </div>
          </div>
          <UIcon
            :name="
              expanded.has(alliance.allianceName)
                ? 'i-heroicons-chevron-up'
                : 'i-heroicons-chevron-down'
            "
            class="size-5 text-gray-400 md:order-last"
          />
          <Stat
            :value="`${percent(alliance.slotsInDeals, alliance.totalSlots)} in deals`"
            :detail="`${alliance.slotsInDeals} of ${alliance.totalSlots} slots`"
          />
          <Stat
            :value="`${alliance.openSlots} open`"
            :detail="
              alliance.flaggedOpenSlots > 0
                ? `plus ${alliance.flaggedOpenSlots} on flagged accounts`
                : 'waiting for a match'
            "
          />
          <Stat
            :value="plural(alliance.activeDeals, 'deal')"
            :detail="`${percent(alliance.health[DealHealth.OnTrack], alliance.activeDeals)} on track`"
          />
        </button>

        <AdminDashboardHealthBar
          v-if="alliance.activeDeals > 0"
          :health="alliance.health"
          class="mt-2"
        />

        <div
          v-if="expanded.has(alliance.allianceName)"
          data-alliance-detail
          class="mt-3 ml-0 md:ml-4 pl-0 md:pl-4 md:border-l border-gray-200 dark:border-gray-700 space-y-4"
        >
          <p class="text-sm text-gray-600 dark:text-gray-300">
            {{ partnerSummary(alliance) }}
          </p>
          <div
            v-for="market in alliance.markets"
            :key="market.key"
            data-alliance-market
          >
            <div class="flex justify-between gap-2">
              <span class="text-sm font-medium text-gray-900 dark:text-white">
                {{ market.label }}
              </span>
              <span class="text-sm text-gray-500 dark:text-gray-400">
                {{ plural(market.activeDeals, 'deal') }}
              </span>
            </div>
            <p class="text-xs text-gray-500 dark:text-gray-400">
              {{ market.terms }}
            </p>
            <p
              v-for="side in market.sides"
              :key="side.terms"
              class="text-xs text-gray-600 dark:text-gray-300"
            >
              {{ side.name }} slots: {{ side.summary.totalSlots }} ·
              {{ side.summary.slotsInDeals }} in deals ·
              {{ side.summary.openSlots }} open<template
                v-if="side.summary.flaggedOpenSlots > 0"
              >
                · {{ side.summary.flaggedOpenSlots }} on flagged accounts
              </template>
            </p>
            <AdminDashboardHealthBar
              v-if="market.activeDeals > 0"
              :health="market.health"
              class="mt-1.5"
            />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, h } from 'vue'
import { DealHealth } from '~/stores/admin'
import type { AllianceSummary } from '~/infrastructure/dashboard'

interface Props {
  alliances: AllianceSummary[]
}

defineProps<Props>()

const expanded = ref(new Set<string>())

const toggle = (allianceName: string) => {
  const next = new Set(expanded.value)
  if (!next.delete(allianceName)) next.add(allianceName)
  expanded.value = next
}

const plural = (count: number, word: string): string =>
  `${count} ${word}${count === 1 ? '' : 's'}`

const percent = (part: number, whole: number): string =>
  whole > 0 ? `${Math.round((part / whole) * 100)}%` : '0%'

const partnerSummary = (alliance: AllianceSummary): string => {
  const withOthers = alliance.activeDeals - alliance.withinAllianceDeals
  return `${plural(alliance.activeDeals, 'active deal')}: ${withOthers} with other alliances, ${alliance.withinAllianceDeals} within ${alliance.allianceName}.`
}

const Stat = defineComponent({
  props: {
    value: { type: String, required: true },
    detail: { type: String, required: true },
  },
  setup: (statProps) => () =>
    h('div', { class: 'col-span-2 md:col-span-1' }, [
      h(
        'p',
        { class: 'text-sm font-semibold text-gray-900 dark:text-white' },
        statProps.value,
      ),
      h(
        'p',
        { class: 'text-xs text-gray-500 dark:text-gray-400' },
        statProps.detail,
      ),
    ]),
})
</script>
