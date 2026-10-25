<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white">
      Aid Legs, Last {{ days }} Days
    </p>
    <p class="text-sm text-gray-500 dark:text-gray-400 mb-4">
      {{ totals.dealsCreated }} deals created ·
      {{ totals.dealsCancelled }} cancelled · {{ totals.legsFailed }} of
      {{ totals.legsSent }} legs failed
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
import dateFormat from 'dateformat'
import type { DailyDealActivity } from '~/stores/admin'

ChartJS.register(BarElement, CategoryScale, LinearScale, Tooltip, Legend)

interface Props {
  data: DailyDealActivity[] | undefined
  days: number
}

const props = defineProps<Props>()

const totals = computed(() =>
  (props.data ?? []).reduce(
    (sum, day) => ({
      legsSent: sum.legsSent + day.legsSent,
      legsFailed: sum.legsFailed + day.legsFailed,
      dealsCreated: sum.dealsCreated + day.dealsCreated,
      dealsCancelled: sum.dealsCancelled + day.dealsCancelled,
    }),
    { legsSent: 0, legsFailed: 0, dealsCreated: 0, dealsCancelled: 0 },
  ),
)

const chartData = computed(() => ({
  // Days are CN dates with no time; parsing the date alone would shift them back a day west of UTC
  labels:
    props.data?.map((x) =>
      dateFormat(new Date(`${x.day.split('T')[0]}T12:00:00`), 'mmm-dd'),
    ) ?? [],
  datasets: [
    {
      label: 'Legs sent',
      data: props.data?.map((x) => x.legsSent) ?? [],
      backgroundColor: 'rgb(59, 130, 246)',
      borderRadius: 4,
    },
    {
      label: 'Legs failed',
      data: props.data?.map((x) => x.legsFailed) ?? [],
      backgroundColor: 'rgb(239, 68, 68)',
      borderRadius: 4,
    },
  ],
}))

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  plugins: {
    legend: {
      position: 'bottom' as const,
      labels: { color: 'rgb(156, 163, 175)', boxWidth: 12 },
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
      ticks: { color: 'rgb(156, 163, 175)', precision: 0 },
      grid: { color: 'rgba(156, 163, 175, 0.1)' },
    },
    x: {
      ticks: { color: 'rgb(156, 163, 175)' },
      grid: { display: false },
    },
  },
}))
</script>
