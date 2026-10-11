<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
      Slots By Aid Direction
    </p>
    <div class="h-64">
      <Bar :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  Chart as ChartJS,
  BarElement,
  CategoryScale,
  LinearScale,
  Tooltip,
  Legend,
} from 'chart.js'
import { Bar } from 'vue-chartjs'
import { convertToAllianceAcronym } from '~/infrastructure/converters'
import type { TotalSlotGroupsByAlliance } from '~/stores/admin'

// Register Chart.js components
ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend)

interface Props {
  data: TotalSlotGroupsByAlliance[] | undefined
  alliances: string[]
}

const props = defineProps<Props>()

const chartData = computed(() => {
  if (props.data === undefined) {
    return {
      labels: [],
      datasets: [],
    }
  }

  console.log(props.data, props.alliances)
  const allianceAcronyms = props.alliances.map((aa) =>
    convertToAllianceAcronym(aa),
  )
  const colors = [
    'rgba(59, 130, 246, 0.8)', // blue
    'rgba(16, 185, 129, 0.8)', // green
    'rgba(245, 158, 11, 0.8)', // amber
    'rgba(239, 68, 68, 0.8)', // red
    'rgba(139, 92, 246, 0.8)', // violet
    'rgba(236, 72, 153, 0.8)', // pink
    'rgba(20, 184, 166, 0.8)', // teal
    'rgba(251, 146, 60, 0.8)', // orange
    'rgba(34, 197, 94, 0.8)', // emerald
    'rgba(168, 85, 247, 0.8)', // purple
    'rgba(249, 115, 22, 0.8)', // deep orange
    'rgba(6, 182, 212, 0.8)', // cyan
    'rgba(244, 63, 94, 0.8)', // rose
    'rgba(99, 102, 241, 0.8)', // indigo
    'rgba(234, 179, 8, 0.8)', // yellow
    'rgba(14, 165, 233, 0.8)', // sky blue
    'rgba(217, 70, 239, 0.8)', // fuchsia
    'rgba(132, 204, 22, 0.8)', // lime
    'rgba(248, 113, 113, 0.8)', // light red
    'rgba(52, 211, 153, 0.8)', // light green
  ]

  const result = {
    labels: props.data.map((x) => x.slotGroupName),
    datasets: allianceAcronyms.map((rangeName, index) => ({
      label: rangeName,
      data: props.data.map(
        (x) =>
          x.totalSlotGroups.find(
            (group) =>
              convertToAllianceAcronym(group.allianceName) === rangeName,
          )?.totalSlots ?? 0,
      ),
      backgroundColor: colors[index % colors.length],
      borderColor: colors[index % colors.length].replace('0.8', '1'),
      borderWidth: 2,
    })),
  }
  console.log(result)
  return result
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
