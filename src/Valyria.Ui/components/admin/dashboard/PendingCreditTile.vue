<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">Pending Credit</p>
    <div class="space-y-3">
      <!-- Chart component pending - using skeleton placeholder -->
      <USkeleton class="h-48 w-full" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface CreditGroup {
  dateRange: string
  totalCredit: number
}

interface CreditData {
  allianceName: string
  creditGroups: CreditGroup[]
}

interface Props {
  data: CreditData[]
}

const props = defineProps<Props>()

const options = computed(() => {
  return {
    chart: {
      stacked: true,
      toolbar: {
        show: false
      }
    },
    plotOptions: {
      bar: {
        horizontal: true,
        dataLabels: {
          total: {
            enabled: true,
            offsetX: 0
          }
        }
      }
    },
    xaxis: {
      categories: props.data?.map(x => getAllianceAcronym(x.allianceName)) ?? []
    }
  }
})

const series = computed(() => {
  if (props.data === undefined) {
    return []
  }

  const dateRanges = ["0-2 Days", "3-5 Days", "6-8 Days", "9+ Days"]
  return dateRanges.map(rangeName => ({
    name: rangeName,
    data: props.data.map(x => x.creditGroups.find(row => row.dateRange === rangeName)?.totalCredit ?? 0)
  }))
})

const getAllianceAcronym = (allianceName: string): string => {
  switch (allianceName) {
    case "NATO": return "NATO"
    case "Freehold of The Wolves": return "FTW"
    case "Christian Coalition Of Countries": return "CCC"
    default: return allianceName
  }
}
</script>
