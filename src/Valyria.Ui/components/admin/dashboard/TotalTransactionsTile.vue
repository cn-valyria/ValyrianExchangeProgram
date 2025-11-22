<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Total Transactions</p>
    <div class="h-64">
      <Line :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { Chart as ChartJS, LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend, Filler } from 'chart.js'
import { Line } from 'vue-chartjs'
import dateFormat from 'dateformat'

// Register Chart.js components
ChartJS.register(LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend, Filler)

interface TransactionData {
  dayTxnRecorded: string
  txnCount: number
}

interface Props {
  data: TransactionData[]
}

const props = defineProps<Props>()

const chartData = computed(() => {
  return {
    labels: props.data?.map(x => dateFormat(new Date(x.dayTxnRecorded), "mmm-dd")) ?? [],
    datasets: [
      {
        label: 'Transactions',
        data: props.data?.map(x => x.txnCount) ?? [],
        fill: true,
        backgroundColor: 'rgba(59, 130, 246, 0.1)',
        borderColor: 'rgb(59, 130, 246)',
        borderWidth: 2,
        tension: 0.4,
        pointRadius: 4,
        pointHoverRadius: 6,
        pointBackgroundColor: 'rgb(59, 130, 246)',
        pointBorderColor: '#fff',
        pointBorderWidth: 2,
        pointHoverBackgroundColor: 'rgb(59, 130, 246)',
        pointHoverBorderColor: '#fff',
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
