<template>
  <UCard :ui="{ body: 'space-y-4' }">
    <!-- What kind of flag, and where its deal stands -->
    <div class="flex flex-wrap items-center justify-between gap-2">
      <div class="flex flex-wrap items-center gap-2">
        <UBadge :color="typeInfo.color" variant="solid" icon="i-heroicons-flag">
          {{ typeInfo.label }}
        </UBadge>
        <span class="font-medium text-gray-900 dark:text-white">
          {{ getFlagReasonLabel(flag) }}
        </span>
      </div>
      <div class="flex flex-wrap items-center gap-2 text-sm">
        <span class="text-gray-500 dark:text-gray-400">
          Deal #{{ flag.dealId }} · flagged {{ formatDate(flag.flaggedOn) }}
        </span>
        <UBadge :color="dealStatus.color" variant="subtle">
          {{ dealStatus.label }}
        </UBadge>
      </div>
    </div>

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
            <NuxtLink
              :to="`/account/${party.slot.account.accountId}`"
              class="font-medium text-gray-900 dark:text-white hover:underline"
            >
              {{ getAccountName(party.slot.account) }}
            </NuxtLink>
            <div class="text-sm text-gray-500 dark:text-gray-400 truncate">
              {{ party.slot.account.nationName ?? 'Nation deleted' }} · Slot #{{
                party.slot.slotNumber
              }}
              · sends
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
          <UBadge v-if="party.note" color="error" variant="outline" size="md">{{
            party.note
          }}</UBadge>
        </div>
      </div>
    </div>

    <!-- Why, and what happens next -->
    <dl class="space-y-3 text-sm">
      <div>
        <dt class="font-semibold text-gray-900 dark:text-white">Expected</dt>
        <dd class="text-gray-600 dark:text-gray-300">
          {{ describeExpectation(flag) }}
        </dd>
      </div>
      <div>
        <dt class="font-semibold text-gray-900 dark:text-white">
          What happened
        </dt>
        <dd class="text-gray-600 dark:text-gray-300">
          {{ describeWhatHappened(flag) }}
        </dd>
      </div>
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
    </dl>

    <div class="flex flex-wrap items-center justify-between gap-2">
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
      <UButton
        v-if="!readOnly"
        label="Resolve..."
        color="primary"
        variant="soft"
        size="sm"
        @click="emit('resolve', flag)"
      />
    </div>

    <FlagEvidence
      v-if="showEvidence"
      :flag="flag"
      class="border-t border-gray-200 dark:border-gray-800 pt-4"
    />
  </UCard>
</template>

<script setup lang="ts">
import { describeAid } from '~/infrastructure/deals'
import {
  describeExpectation,
  describeWhatHappened,
  getAccountName,
  getDealStatusInfo,
  getFlagOutlook,
  getFlagReasonLabel,
  getFlagTypeInfo,
  isOnActiveDeal,
} from '~/infrastructure/flags'
import type { SlotDetail } from '~/stores/account'
import type { DealFlag } from '~/stores/adminDeals'
import FlagEvidence from '~/components/admin/FlagEvidence.vue'

const props = defineProps<{
  flag: DealFlag
  // CN date, e.g. "2026-10-09"
  today: string
  readOnly: boolean
}>()

const emit = defineEmits<{ resolve: [DealFlag] }>()

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

const formatDate = (date: string): string => date.split('T')[0]
</script>
