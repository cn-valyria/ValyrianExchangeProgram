<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Free Aid Slots</p>
    <div class="h-64">
      <Bar :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { Chart as ChartJS, BarElement, CategoryScale, LinearScale, Tooltip, Legend } from 'chart.js'
import { Bar } from 'vue-chartjs'

// Register Chart.js components
ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend)

interface ListData {
  listName: string
  freeSlots: number
}

interface Props {
  data: ListData[] | undefined
}

const props = defineProps<Props>()

const chartData = computed(() => {
  const colors = [
    'rgba(59, 130, 246, 0.8)',   // blue
    'rgba(16, 185, 129, 0.8)',   // green
    'rgba(245, 158, 11, 0.8)',   // amber
    'rgba(239, 68, 68, 0.8)',    // red
    'rgba(168, 85, 247, 0.8)',   // purple
    'rgba(236, 72, 153, 0.8)',   // pink
  ]

  return {
    labels: props.data?.map(x => x.listName) ?? [],
    datasets: [
      {
        label: 'Free Aid Slots',
        data: props.data?.map(x => x.freeSlots) ?? [],
        backgroundColor: props.data?.map((_, index) => colors[index % colors.length]) ?? [],
        borderColor: props.data?.map((_, index) => colors[index % colors.length].replace('0.8', '1')) ?? [],
        borderWidth: 2,
      },
    ],
  }
})

const chartOptions = computed(() => {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: false,
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        padding: 12,
        cornerRadius: 8,
      },
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          color: 'rgb(156, 163, 175)', // gray-400
        },
        grid: {
          color: 'rgba(156, 163, 175, 0.1)',
        },
      },
      x: {
        ticks: {
          color: 'rgb(156, 163, 175)', // gray-400
        },
        grid: {
          display: false,
        },
      },
    },
  }
})
</script>
