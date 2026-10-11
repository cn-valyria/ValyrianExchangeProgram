<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
      Most Accounts
    </p>
    <div class="space-y-3">
      <div v-for="(row, index) in allianceData" :key="row.allianceName">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3 flex-1">
            <chip-alliance-acronym :alliance-name="row.allianceName" />
            <span class="text-gray-700 dark:text-gray-300">{{
              row.allianceName
            }}</span>
          </div>
          <UBadge
            :label="row.totalAccounts.toString()"
            color="primary"
            variant="subtle"
            size="lg"
          />
        </div>
        <div
          v-if="index !== allianceData.length - 1"
          class="border-t border-gray-200 dark:border-gray-700 mt-3"
        ></div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
interface AllianceData {
  allianceName: string
  totalAccounts: number
}

interface Props {
  data: AllianceData[] | undefined
}

const props = defineProps<Props>()

const allianceData = computed<AllianceData[]>(() => props.data || [])
</script>
