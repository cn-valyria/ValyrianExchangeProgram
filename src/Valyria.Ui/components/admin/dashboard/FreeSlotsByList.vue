<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Free Aid Slots</p>
    <div class="text-gray-600 dark:text-gray-400">
      <!-- Chart component commented out in original - keeping this minimal -->
      <p class="text-sm">Chart visualization pending</p>
    </div>
  </div>
</template>

<script setup lang="ts">
interface ListData {
  listName: string
  freeSlots: number
}

interface Props {
  data: ListData[]
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
      categories: props.data?.map(x => x.listName) ?? []
    },
    yaxis: {
      title: {
        text: "Free Aid Slots"
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
      name: "Free Aid Slots",
      data: props.data?.map(x => x.freeSlots) ?? []
    }
  ]
})
</script>
