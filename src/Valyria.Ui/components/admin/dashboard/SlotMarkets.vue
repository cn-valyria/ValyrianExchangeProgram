<template>
  <div>
    <div class="mb-4">
      <p class="text-lg font-semibold text-gray-900 dark:text-white">
        Slot Balance
      </p>
      <p class="text-sm text-gray-500 dark:text-gray-400">
        A slot is only matched with slots set up as its exact opposite, so each
        row is its own pool. The bars compare open slots on each side; the
        longer side is waiting for partners.
      </p>
    </div>

    <p
      v-if="markets.length === 0"
      class="text-sm text-gray-500 dark:text-gray-400"
    >
      No slots yet.
    </p>

    <div class="divide-y divide-gray-200 dark:divide-gray-700">
      <div
        v-for="market in markets"
        :key="market.key"
        data-market
        class="py-4 first:pt-0 last:pb-0"
      >
        <div class="flex flex-wrap items-center gap-2 mb-3">
          <UBadge
            :label="verdicts[market.key].label"
            :color="verdicts[market.key].color"
            variant="subtle"
          />
          <UBadge
            v-if="market.unpairedSlots > 0"
            :label="`${market.unpairedSlots} unpaired`"
            color="neutral"
            variant="outline"
            :title="unpairedDetail"
          />
          <span class="text-sm text-gray-600 dark:text-gray-300">
            {{ verdicts[market.key].detail }}
          </span>
        </div>

        <div
          class="grid grid-cols-1 md:grid-cols-[1fr_minmax(8rem,14rem)_1fr] gap-3 items-center"
        >
          <MarketSide :side="market.first" />

          <div
            v-if="market.second"
            class="flex items-center gap-0.5 h-3"
            :title="`Open: ${market.first.summary.openSlots} ${market.first.name} · ${market.second.summary.openSlots} ${market.second.name}`"
          >
            <div class="flex-1 flex justify-end">
              <div
                class="h-3 rounded-l bg-primary-500"
                :style="{ width: barWidth(market.first.summary.openSlots) }"
              />
            </div>
            <div class="w-px h-5 bg-gray-400 dark:bg-gray-500" />
            <div class="flex-1">
              <div
                class="h-3 rounded-r bg-primary-500"
                :style="{ width: barWidth(market.second.summary.openSlots) }"
              />
            </div>
          </div>
          <div v-else />

          <MarketSide
            v-if="market.second"
            :side="market.second"
            class="md:text-right"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineComponent, h } from 'vue'
import {
  getMarketVerdict,
  type SlotMarket,
  type SlotMarketSide,
} from '~/infrastructure/dashboard'

interface Props {
  markets: SlotMarket[]
}

const props = defineProps<Props>()

const unpairedDetail =
  'Open on both sides, but matching left them unpaired. Usually the only open accounts on each side already share a deal (two accounts get one deal), or those exact slots were paired before.'

const verdicts = computed(() =>
  Object.fromEntries(props.markets.map((m) => [m.key, getMarketVerdict(m)])),
)

const maxOpen = computed(() =>
  Math.max(
    1,
    ...props.markets.flatMap((m) => [
      m.first.summary.openSlots,
      m.second?.summary.openSlots ?? 0,
    ]),
  ),
)

const barWidth = (open: number): string =>
  `${Math.round((open / maxOpen.value) * 100)}%`

const MarketSide = defineComponent({
  props: { side: { type: Object as () => SlotMarketSide, required: true } },
  setup: (sideProps) => () => {
    const { summary, name, terms } = sideProps.side
    const stats = [
      `${summary.totalSlots} slots`,
      `${summary.slotsInDeals} in deals`,
      `${summary.openSlots} open on ${summary.openAccounts} account${summary.openAccounts === 1 ? '' : 's'}`,
    ]
    if (summary.flaggedOpenSlots > 0) {
      stats.push(`${summary.flaggedOpenSlots} on flagged accounts`)
    }
    return h('div', [
      h(
        'p',
        { class: 'font-medium text-gray-900 dark:text-white' },
        `${name} · ${terms}`,
      ),
      h(
        'p',
        { class: 'text-xs text-gray-500 dark:text-gray-400' },
        stats.join(' · '),
      ),
    ])
  },
})
</script>
