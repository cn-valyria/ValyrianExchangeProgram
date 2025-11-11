<template>
  <div>
    <p class="title">Free Aid Slots</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="bar" :options="options" :series="series"></ApexChart> -->
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
