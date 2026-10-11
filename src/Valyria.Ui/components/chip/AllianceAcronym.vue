<template>
  <div class="flex gap-1 items-center">
    <UBadge
      color="neutral"
      variant="soft"
      size="md"
      class="flex items-center gap-1"
    >
      <img
        :src="allianceImage"
        height="18"
        width="18"
        alt="Alliance"
        class="rounded"
      />
      <span>{{ allianceAcronym }}</span>
    </UBadge>
  </div>
</template>

<script setup lang="ts">
import { convertToAllianceAcronym } from '~/infrastructure/converters'
import { allianceAcronyms } from '~/infrastructure/dataLists'

const props = defineProps<{
  allianceName: string | undefined
}>()

const allianceAcronym = computed(() => {
  return convertToAllianceAcronym(props.allianceName)
})

const allianceImage = computed(() => {
  let fileName = 'none'

  // Since we already have a computed property for figuring out a known acronym, only change
  // the fileName if that property is one of those known values
  if (allianceAcronyms.some((aa) => aa.code === allianceAcronym.value)) {
    fileName = allianceAcronym.value.toLowerCase()
  }

  return new URL(`../../assets/img/alliance/${fileName}.jpg`, import.meta.url)
    .href
})
</script>
