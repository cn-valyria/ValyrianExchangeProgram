<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Total Aid Slots</p>
    <div class="text-gray-600 dark:text-gray-400">
      <!-- Chart component commented out in original - keeping this minimal -->
      <p class="text-sm">Chart visualization pending</p>
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
