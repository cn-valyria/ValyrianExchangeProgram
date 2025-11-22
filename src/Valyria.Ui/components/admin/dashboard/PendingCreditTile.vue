<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Pending Credit</p>
    <div class="h-64">
      <Bar :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { Chart as ChartJS, BarElement, CategoryScale, LinearScale, Tooltip, Legend } from 'chart.js'
import { Bar } from 'vue-chartjs'
import { convertToAllianceAcronym } from '~/infrastructure/converters'

// Register Chart.js components
ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend)

interface CreditGroup {
  dateRange: string
  totalCredit: number
}

interface CreditData {
  allianceName: string
  creditGroups: CreditGroup[]
}

interface Props {
  data: CreditData[] | undefined
}

const props = defineProps<Props>()

const chartData = computed(() => {
  if (props.data === undefined) {
    return {
      labels: [],
      datasets: [],
    }
  }

  const dateRanges = ["0-2 Days", "3-5 Days", "6-8 Days", "9+ Days"]
  const colors = [
    'rgba(59, 130, 246, 0.8)',   // blue
    'rgba(16, 185, 129, 0.8)',   // green
    'rgba(245, 158, 11, 0.8)',   // amber
    'rgba(239, 68, 68, 0.8)',    // red
  ]

  return {
    labels: props.data.map(x => convertToAllianceAcronym(x.allianceName)),
    datasets: dateRanges.map((rangeName, index) => ({
      label: rangeName,
      data: props.data.map(x => x.creditGroups.find(row => row.dateRange === rangeName)?.totalCredit ?? 0),
      backgroundColor: colors[index % colors.length],
      borderColor: colors[index % colors.length].replace('0.8', '1'),
      borderWidth: 2,
    })),
  }
})

const chartOptions = computed(() => {
  return {
    indexAxis: 'y' as const,
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom' as const,
        labels: {
          color: 'rgb(156, 163, 175)', // gray-400
          padding: 10,
          font: {
            size: 11,
          },
        },
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        padding: 12,
        cornerRadius: 8,
      },
    },
    scales: {
      x: {
        stacked: true,
        ticks: {
          color: 'rgb(156, 163, 175)', // gray-400
        },
        grid: {
          color: 'rgba(156, 163, 175, 0.1)',
        },
      },
      y: {
        stacked: true,
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
