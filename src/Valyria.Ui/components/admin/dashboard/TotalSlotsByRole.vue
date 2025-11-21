<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Total Aid Slots</p>
    <div class="space-y-3">
      <!-- Chart component pending - using skeleton placeholder -->
      <USkeleton class="h-48 w-full" />
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
