<template>
  <div class="flex gap-1">
    <UBadge :color="codeColor" variant="solid" size="md">{{ code }}</UBadge>
    <UBadge color="neutral" variant="soft" size="md">{{ roleDescription }}</UBadge>
  </div>
</template>

<script setup lang="ts">
import { accountRoles, getAccountRoleColor } from '~/infrastructure/dataLists'

const props = defineProps<{
  code: string | undefined
}>()

const roleDescription = computed(() => {
  const role = accountRoles.find((r) => r.code === props.code)
  return role ? role.description : ''
})

const codeColor = computed(() => getAccountRoleColor(props.code))
</script>
