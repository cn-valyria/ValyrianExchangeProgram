<template>
  <div>
    <p class="title">Total Aid Slots</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="bar" :options="options" :series="series"></ApexChart> -->
    </div>
  </div>
</template>

<script setup lang="ts">
import { accountRoles } from '~/infrastructure/dataLists'

interface SlotData {
  role: string
  totalSlots: number
}

interface Props {
  data: SlotData[]
}

const props = defineProps<Props>()

const options = computed(() => {
  return {
    chart: {
      toolbar: {
        show: false
      }
    },
    plotOptions: {
      bar: {
        distributed: true
      }
    },
    xaxis: {
      categories: props.data?.map(x => accountRoles.find(role => role.code === x.role)?.description ?? '') ?? []
    },
    yaxis: {
      title: {
        text: "Total Aid Slots"
      }
    },
    legend: {
      show: false
    }
  }
})

const series = computed(() => {
  return [
    {
      name: "Total Aid Slots",
      data: props.data?.map(x => x.totalSlots) ?? []
    }
  ]
})
</script>
