<template>
  <div class="space-y-5 text-sm">
    <div v-if="loading" class="text-gray-500 dark:text-gray-400">
      Loading aid details...
    </div>
    <div v-else-if="!evidence" class="flex items-center gap-3">
      <span class="text-gray-500 dark:text-gray-400"
        >Couldn't load the aid details.</span
      >
      <UButton label="Try Again" size="xs" variant="soft" @click="load()" />
    </div>
    <template v-else>
      <section class="space-y-2">
        <h4 class="font-semibold text-gray-900 dark:text-white">
          Deal #{{ flag.dealId }} aid, oldest first
        </h4>
        <p
          v-if="evidence.legs.length === 0"
          class="text-gray-500 dark:text-gray-400"
        >
          No aid has counted toward this deal yet.
        </p>
        <ul v-else class="divide-y divide-gray-200 dark:divide-gray-800">
          <li
            v-for="leg in evidence.legs"
            :key="leg.legNumber"
            class="py-2 flex flex-wrap items-center gap-x-3 gap-y-1"
          >
            <span class="font-mono text-gray-500 dark:text-gray-400"
              >#{{ leg.legNumber }}</span
            >
            <span>{{ formatDate(leg.activityDate) }}</span>
            <span class="text-gray-900 dark:text-white">
              {{ leg.sentByFlaggedAccount ? flaggedName : partnerName }} →
              {{ leg.sentByFlaggedAccount ? partnerName : flaggedName }}
            </span>
            <span>{{ describeAidContents(leg) }}</span>
            <UBadge
              :color="getLegOutcomeInfo(leg).color"
              variant="subtle"
              size="sm"
              >{{ getLegOutcomeInfo(leg).label }}</UBadge
            >
          </li>
        </ul>
      </section>

      <section class="space-y-2">
        <h4 class="font-semibold text-gray-900 dark:text-white">
          {{ flaggedPossessive }} aid slots {{ slotsWhen }}
        </h4>
        <p class="text-gray-600 dark:text-gray-400">
          {{ slotSummary }}
        </p>
        <ul
          v-if="evidence.flaggedSlotAid.length > 0"
          class="divide-y divide-gray-200 dark:divide-gray-800"
        >
          <li
            v-for="aid in evidence.flaggedSlotAid"
            :key="aid.aidId"
            class="py-2 space-y-1"
          >
            <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
              <span>{{ formatDate(aid.sentOn) }}</span>
              <span class="text-gray-900 dark:text-white">
                {{ aid.isOutgoing ? 'Sent to' : 'Received from' }}
                {{ otherNationName(aid) }}
              </span>
              <span>{{ describeAidContents(aid) }}</span>
              <UBadge
                :color="getSlotAidKind(aid, flag).color"
                variant="subtle"
                size="sm"
                >{{ getSlotAidKind(aid, flag).label }}</UBadge
              >
              <UBadge
                v-if="aid.status === 1"
                color="warning"
                variant="outline"
                size="sm"
                >Not yet accepted</UBadge
              >
            </div>
            <div
              v-if="aid.reason"
              class="text-xs text-gray-500 dark:text-gray-400 break-words"
            >
              "{{ aid.reason }}"
            </div>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import {
  describeAidContents,
  getAccountName,
  getLegOutcomeInfo,
  getSlotAidKind,
  isOnActiveDeal,
  type FlagVoice,
} from '~/infrastructure/flags'
import {
  useAdminDealsStore,
  type DealFlag,
  type DealFlagEvidence,
  type FlagSlotAid,
} from '~/stores/adminDeals'

const props = withDefaults(
  defineProps<{
    flag: DealFlag
    // Already loaded, as on the account page. Otherwise it's loaded from the admin API
    evidence?: DealFlagEvidence
    voice?: FlagVoice
  }>(),
  { evidence: undefined, voice: 'admin' },
)

const adminDealsStore = useAdminDealsStore()
const { flagEvidence } = storeToRefs(adminDealsStore)

const loading = ref<boolean>(false)

const evidence = computed(
  () => props.evidence ?? flagEvidence.value[props.flag.id],
)
const flaggedName = computed(() =>
  props.voice === 'flagged' ? 'You' : getAccountName(props.flag.flaggedAccount),
)
const flaggedPossessive = computed(() =>
  props.voice === 'flagged' ? 'Your' : `${flaggedName.value}'s`,
)
const partnerName = computed(() => getAccountName(props.flag.innocentAccount))

const formatDate = (date: string): string => date.split('T')[0]

const slotsWhen = computed(() =>
  isOnActiveDeal(props.flag)
    ? 'right now'
    : `when flagged (${formatDate(evidence.value!.slotsAsOf)})`,
)

const slotSummary = computed(() => {
  const { flaggedSlotAid, flaggedSlotsTotal } = evidence.value!
  const outside = flaggedSlotAid.filter(
    (aid) => getSlotAidKind(aid, props.flag).label === 'Outside VEP',
  ).length
  const used = Math.min(flaggedSlotAid.length, flaggedSlotsTotal)
  const outsideText =
    outside === 0
      ? 'none of it outside VEP since this deal started'
      : `${outside} outside VEP since this deal started`
  return `${used} of ${flaggedSlotsTotal} in use, ${outsideText}. Every offer holds a slot on both nations for 10 days.`
})

const otherNationName = (aid: FlagSlotAid): string =>
  aid.otherNation.rulerName
    ? `${aid.otherNation.rulerName} (${aid.otherNation.nationName})`
    : `nation #${aid.otherNation.nationId}`

const load = async (): Promise<void> => {
  loading.value = true
  try {
    await adminDealsStore.loadFlagEvidence(props.flag.id)
  } catch (error) {
    console.error('Failed to load flag evidence:', error)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  if (!evidence.value) await load()
})
</script>
