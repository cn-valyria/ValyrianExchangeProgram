<template>
  <div>
    <p class="text-lg font-semibold text-gray-900 dark:text-white mb-4">VEP Accounts</p>
    <div class="space-y-3">
      <!-- Chart component pending - using skeleton placeholder -->
      <USkeleton class="h-48 w-full" />
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
