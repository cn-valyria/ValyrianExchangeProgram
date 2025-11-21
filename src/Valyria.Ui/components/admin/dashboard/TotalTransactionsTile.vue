<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Total Transactions</p>
    <div class="space-y-3">
      <!-- Chart component pending - using skeleton placeholder -->
      <USkeleton class="h-48 w-full" />
    </div>
  </div>
</template>

<script setup lang="ts">
import dateFormat from 'dateformat'

interface TransactionData {
  dayTxnRecorded: string
  txnCount: number
}

interface Props {
  data: TransactionData[]
}

const props = defineProps<Props>()

const options = computed(() => {
  return {
    chart: {
      toolbar: {
        show: false
      }
    },
    stroke: {
      curve: "smooth"
    },
    xaxis: {
      categories: props.data?.map(x => dateFormat(new Date(x.dayTxnRecorded), "mmm-dd")) ?? []
    }
  }
})

const series = computed(() => {
  return [
    {
      name: "Transactions",
      data: props.data?.map(x => x.txnCount) ?? []
    }
  ]
})
</script>
