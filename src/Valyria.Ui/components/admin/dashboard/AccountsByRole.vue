<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">VEP Accounts</p>
    <div class="text-gray-600 dark:text-gray-400">
      <!-- Chart component commented out in original - keeping this minimal -->
      <p class="text-sm">Chart visualization pending</p>
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
