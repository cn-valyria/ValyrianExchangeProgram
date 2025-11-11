<template>
  <div>
    <p class="title">Total Transactions</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="line" :options="options" :series="series"></ApexChart> -->
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
