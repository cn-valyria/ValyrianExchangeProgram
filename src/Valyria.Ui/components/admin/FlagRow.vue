<template>
  <div>
    <!-- Summary row: wraps onto a few short lines on phones, a table row from md up -->
    <div
      :class="[
        'flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/50',
        FLAG_ROW_GRID,
      ]"
      @click="emit('toggle', flag)"
    >
      <div class="flex items-center gap-2 min-w-0">
        <UButton
          :icon="
            expanded ? 'i-heroicons-chevron-down' : 'i-heroicons-chevron-right'
          "
          :aria-label="expanded ? 'Hide details' : 'Show details'"
          :aria-expanded="expanded"
          color="neutral"
          variant="ghost"
          size="xs"
          @click.stop="emit('toggle', flag)"
        />
        <div class="min-w-0">
          <UBadge :color="typeInfo.color" variant="subtle" size="sm">
            {{ typeInfo.label }}
          </UBadge>
          <div
            class="hidden md:block text-xs text-gray-500 dark:text-gray-400 truncate"
          >
            {{ getFlagReasonLabel(flag) }}
          </div>
        </div>
      </div>

      <div class="flex items-center gap-2 min-w-0">
        <ChipAllianceAcronym
          :alliance-name="flag.flaggedAccount.allianceName"
        />
        <span class="font-medium text-gray-900 dark:text-white truncate">
          {{ getAccountName(flag.flaggedAccount) }}
        </span>
        <span class="text-xs text-gray-500 dark:text-gray-400">
          {{ flag.flaggedAccountRole }}
        </span>
        <UBadge
          v-if="flag.flaggedAccountOpenFlagCount > 1"
          color="error"
          variant="outline"
          size="sm"
          >{{ flag.flaggedAccountOpenFlagCount }} flags</UBadge
        >
      </div>

      <div class="text-sm text-gray-600 dark:text-gray-300 truncate min-w-0">
        <span class="md:hidden">with </span
        >{{ getAccountName(flag.innocentAccount) }}
      </div>

      <div class="flex items-center gap-2 text-sm min-w-0">
        <span class="text-gray-500 dark:text-gray-400">#{{ flag.dealId }}</span>
        <UBadge :color="dealStatus.color" variant="subtle" size="sm">
          {{ dealStatus.label }}
        </UBadge>
      </div>

      <div :class="['text-sm min-w-0', OUTLOOK_TEXT[outlook.color]]">
        {{ outlook.title }}
      </div>

      <div class="ml-auto md:ml-0 md:text-right">
        <UButton
          v-if="!readOnly"
          label="Resolve..."
          color="primary"
          variant="soft"
          size="xs"
          @click.stop="emit('resolve', flag)"
        />
      </div>
    </div>

    <!-- Everything else: the full card -->
    <div v-if="expanded" class="px-4 pb-4 pt-2 space-y-4 md:pl-14">
      <!-- Who -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div
          v-for="party in parties"
          :key="party.heading"
          class="rounded-md bg-gray-50 dark:bg-gray-800/50 p-3 space-y-1"
        >
          <div
            class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400"
          >
            {{ party.heading }}
          </div>
          <div class="flex items-center gap-2">
            <ChipAllianceAcronym
              :alliance-name="party.slot.account.allianceName"
            />
            <div class="min-w-0">
              <div class="flex items-center gap-1">
                <NuxtLink
                  :to="{
                    path: '/admin/accounts',
                    query: { account: party.slot.account.accountId },
                  }"
                  class="font-medium text-gray-900 dark:text-white hover:underline"
                >
                  {{ getAccountName(party.slot.account) }}
                </NuxtLink>
                <UButton
                  :to="getNationLink(party.slot.account)"
                  target="_blank"
                  icon="i-heroicons-arrow-top-right-on-square"
                  aria-label="Open nation in CN"
                  color="neutral"
                  variant="ghost"
                  size="xs"
                />
              </div>
              <div class="text-sm text-gray-500 dark:text-gray-400 truncate">
                {{ party.slot.account.nationName ?? 'Nation deleted' }} · Slot
                #{{ party.slot.slotNumber }} · sends
                {{
                  describeAid(
                    party.slot.techSentCycles,
                    party.slot.cashSentCycles,
                  )
                }}
              </div>
            </div>
          </div>
          <div class="flex flex-wrap items-center gap-1">
            <ChipAccountRole :code="party.role" />
            <UBadge
              v-if="party.note"
              color="error"
              variant="outline"
              size="md"
              >{{ party.note }}</UBadge
            >
          </div>
        </div>
      </div>

      <!-- Why, and what happens next -->
      <FlagWhy :flag="flag">
        <UAlert
          :color="outlook.color"
          variant="subtle"
          :icon="
            isOnActiveDeal(flag)
              ? 'i-heroicons-clock'
              : 'i-heroicons-exclamation-triangle'
          "
          :title="`If left alone: ${outlook.title}`"
          :description="outlook.detail"
        />
        <div v-if="!isOnActiveDeal(flag) && flag.cancellationReason">
          <dt class="font-semibold text-gray-900 dark:text-white">
            Cancellation reason
          </dt>
          <dd class="text-gray-600 dark:text-gray-300">
            {{ flag.cancellationReason }}
          </dd>
        </div>
      </FlagWhy>

      <UButton
        :label="showEvidence ? 'Hide aid details' : 'Show aid details'"
        :trailing-icon="
          showEvidence ? 'i-heroicons-chevron-up' : 'i-heroicons-chevron-down'
        "
        color="neutral"
        variant="ghost"
        size="sm"
        @click="showEvidence = !showEvidence"
      />

      <FlagEvidence
        v-if="showEvidence"
        :flag="flag"
        class="border-t border-gray-200 dark:border-gray-800 pt-4"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  describeAid,
  getNationLink,
  type BadgeColor,
} from '~/infrastructure/deals'
import {
  getAccountName,
  getDealStatusInfo,
  getFlagOutlook,
  getFlagReasonLabel,
  getFlagTypeInfo,
  isOnActiveDeal,
  FLAG_ROW_GRID,
} from '~/infrastructure/flags'
import type { SlotDetail } from '~/stores/account'
import type { DealFlag } from '~/stores/adminDeals'
import FlagEvidence from '~/components/flag/FlagEvidence.vue'
import FlagWhy from '~/components/flag/FlagWhy.vue'

const props = defineProps<{
  flag: DealFlag
  // CN date, e.g. "2026-10-09"
  today: string
  readOnly: boolean
  expanded: boolean
}>()

const emit = defineEmits<{ resolve: [DealFlag]; toggle: [DealFlag] }>()

// What happens next, colored like the alert it summarises
const OUTLOOK_TEXT: Record<BadgeColor, string> = {
  error: 'text-red-600 dark:text-red-400 font-medium',
  warning: 'text-amber-600 dark:text-amber-400 font-medium',
  success: 'text-green-600 dark:text-green-400',
  info: 'text-sky-600 dark:text-sky-400',
  neutral: 'text-gray-500 dark:text-gray-400',
}

const showEvidence = ref<boolean>(false)

const typeInfo = computed(() => getFlagTypeInfo(props.flag))
const dealStatus = computed(() => getDealStatusInfo(props.flag))
const outlook = computed(() => getFlagOutlook(props.flag, props.today))

const parties = computed(
  (): {
    heading: string
    slot: SlotDetail
    role: string
    note: string | null
  }[] => [
    {
      heading: 'Flagged',
      slot: props.flag.flaggedSlot,
      role: props.flag.flaggedAccountRole,
      note:
        props.flag.flaggedAccountOpenFlagCount > 1
          ? `${props.flag.flaggedAccountOpenFlagCount} open flags`
          : null,
    },
    {
      heading: 'Partner',
      slot: props.flag.innocentSlot,
      role: props.flag.innocentAccountRole,
      note: null,
    },
  ],
)
</script>
