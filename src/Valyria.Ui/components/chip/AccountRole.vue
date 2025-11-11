<template>
  <div class="flex gap-0">
    <Tag :severity="codeColor" :value="code" />
    <Tag severity="secondary" :value="roleDescription" />
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
      return 'warn'
    case 'S': // Seller
    case 'F': // Farm
    case 'N': // Probationary Seller
    case 'Q': // Temp Collector
      return 'danger'
    case 'C': // Collector
    case 'R': // Receiver
    case 'P': // Temp Donor
    case 'V': // Temp Farm
      return 'info'
    default: // Unknown
      return 'secondary'
  }
})
</script>
