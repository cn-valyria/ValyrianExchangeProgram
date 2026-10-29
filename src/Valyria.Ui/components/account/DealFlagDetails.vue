<template>
  <div class="space-y-4">
    <div
      v-if="!dealFlag && loading"
      class="text-sm text-gray-500 dark:text-gray-400"
    >
      Loading why this deal was flagged...
    </div>
    <div v-else-if="!dealFlag" class="flex items-center gap-3 text-sm">
      <span class="text-gray-500 dark:text-gray-400"
        >Couldn't load why this deal was flagged.</span
      >
      <UButton label="Try Again" size="xs" variant="soft" @click="load()" />
    </div>
    <template v-else>
      <FlagWhy :flag="dealFlag.flag" voice="flagged" />
      <FlagEvidence
        :flag="dealFlag.flag"
        :evidence="dealFlag.evidence"
        voice="flagged"
        class="border-t border-gray-200 dark:border-gray-700 pt-4"
      />
    </template>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import FlagEvidence from '~/components/flag/FlagEvidence.vue'
import FlagWhy from '~/components/flag/FlagWhy.vue'
import { useAccountStore } from '~/stores/account'

const props = defineProps<{
  accountId: number
  dealId: number
}>()

const accountStore = useAccountStore()
const { dealFlags } = storeToRefs(accountStore)

const loading = ref<boolean>(false)

const dealFlag = computed(() => dealFlags.value[props.dealId])

const load = async (): Promise<void> => {
  loading.value = true
  try {
    await accountStore.loadDealFlag(props.accountId, props.dealId)
  } catch (error) {
    console.error('Failed to load deal flag:', error)
  } finally {
    loading.value = false
  }
}

// Reloaded on every open, since the slots change through the day. Anything already loaded shows meanwhile
onMounted(load)
</script>
