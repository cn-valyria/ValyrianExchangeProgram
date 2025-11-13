<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Total Transactions</p>
    <div class="text-gray-600 dark:text-gray-400">
      <!-- Chart component commented out in original - keeping this minimal -->
      <p class="text-sm">Chart visualization pending</p>
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
