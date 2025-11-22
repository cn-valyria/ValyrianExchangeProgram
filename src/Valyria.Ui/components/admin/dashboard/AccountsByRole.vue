<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">VEP Accounts</p>
    <div class="h-64">
      <Pie :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js'
import { Pie } from 'vue-chartjs'
import { accountRoles } from '~/infrastructure/dataLists'

// Register Chart.js components
ChartJS.register(ArcElement, Tooltip, Legend)

interface RoleData {
  role: string
  totalAccounts: number
}

interface Props {
  data: RoleData[]
}

const props = defineProps<Props>()

const chartData = computed(() => {
  return {
    labels: props.data?.map(x => accountRoles.find(role => role.code === x.role)?.description ?? '') ?? [],
    datasets: [
      {
        data: props.data?.map(x => x.totalAccounts) ?? [],
        backgroundColor: [
          'rgba(59, 130, 246, 0.8)',   // blue
          'rgba(16, 185, 129, 0.8)',   // green
          'rgba(245, 158, 11, 0.8)',   // amber
          'rgba(239, 68, 68, 0.8)',    // red
          'rgba(168, 85, 247, 0.8)',   // purple
          'rgba(236, 72, 153, 0.8)',   // pink
        ],
        borderColor: [
          'rgb(59, 130, 246)',
          'rgb(16, 185, 129)',
          'rgb(245, 158, 11)',
          'rgb(239, 68, 68)',
          'rgb(168, 85, 247)',
          'rgb(236, 72, 153)',
        ],
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
        position: 'bottom' as const,
        labels: {
          color: 'rgb(156, 163, 175)', // gray-400
          padding: 15,
          font: {
            size: 12,
          },
        },
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        padding: 12,
        cornerRadius: 8,
      },
    },
  }
})
</script>
