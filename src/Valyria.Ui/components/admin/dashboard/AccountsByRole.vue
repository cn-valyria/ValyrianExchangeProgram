<template>
  <div>
    <p class="title">VEP Accounts</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="pie" :options="options" :series="series"></ApexChart> -->
    </div>
  </div>
</template>

<script setup lang="ts">
import { accountRoles } from '~/infrastructure/dataLists'

interface RoleData {
  role: string
  totalAccounts: number
}

interface Props {
  data: RoleData[]
}

const props = defineProps<Props>()

const options = computed(() => {
  return {
    labels: props.data?.map(x => accountRoles.find(role => role.code === x.role)?.description ?? '') ?? []
  }
})

const series = computed(() => {
  return props.data?.map(x => x.totalAccounts) ?? []
})
</script>
