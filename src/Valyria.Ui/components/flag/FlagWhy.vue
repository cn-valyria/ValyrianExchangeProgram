<template>
  <!-- Why the account was flagged. Anything after it in the list goes in the default slot -->
  <dl class="space-y-3 text-sm">
    <div>
      <dt class="font-semibold text-gray-900 dark:text-white">Flagged</dt>
      <dd class="text-gray-600 dark:text-gray-300">
        {{ formatDate(flag.flaggedOn) }} · {{ getFlagReasonLabel(flag, voice) }}
      </dd>
    </div>
    <div>
      <dt class="font-semibold text-gray-900 dark:text-white">Expected</dt>
      <dd class="text-gray-600 dark:text-gray-300">
        {{ describeExpectation(flag, voice) }}
      </dd>
    </div>
    <div>
      <dt class="font-semibold text-gray-900 dark:text-white">What happened</dt>
      <dd class="text-gray-600 dark:text-gray-300">
        {{ describeWhatHappened(flag, voice) }}
      </dd>
    </div>
    <slot />
  </dl>
</template>

<script setup lang="ts">
import {
  describeExpectation,
  describeWhatHappened,
  getFlagReasonLabel,
  type FlagVoice,
} from '~/infrastructure/flags'
import type { DealFlag } from '~/stores/adminDeals'

withDefaults(
  defineProps<{
    flag: DealFlag
    voice?: FlagVoice
  }>(),
  { voice: 'admin' },
)

const formatDate = (date: string): string => date.split('T')[0]
</script>
