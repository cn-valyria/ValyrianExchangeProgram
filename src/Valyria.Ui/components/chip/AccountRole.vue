<template>
  <div class="flex gap-1">
    <UBadge :color="codeColor" variant="solid" size="md">{{ code }}</UBadge>
    <UBadge color="gray" variant="soft" size="md">{{ roleDescription }}</UBadge>
  </div>
</template>

<script setup lang="ts">
import { accountRoles } from '~/infrastructure/dataLists'

const props = defineProps<{
  code: string
}>()

const roleDescription = computed(() => {
  const role = accountRoles.find((r) => r.code === props.code)
  return role ? role.description : ''
})

const codeColor = computed(() => {
  switch (props.code) {
    case 'B': // Buyer
    case 'D': // Donor
    case 'W': // Temp Receiver
      return 'yellow'
    case 'S': // Seller
    case 'F': // Farm
    case 'N': // Probationary Seller
    case 'Q': // Temp Collector
      return 'red'
    case 'C': // Collector
    case 'R': // Receiver
    case 'P': // Temp Donor
    case 'V': // Temp Farm
      return 'blue'
    default: // Unknown
      return 'gray'
  }
})
</script>
