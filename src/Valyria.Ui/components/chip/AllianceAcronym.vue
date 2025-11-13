<template>
  <div class="flex gap-1 items-center">
    <UBadge color="gray" variant="soft" size="md" class="flex items-center gap-1">
      <img :src="allianceImage" height="18" width="18" alt="Alliance" class="rounded" />
      <span>{{ allianceAcronym }}</span>
    </UBadge>
  </div>
</template>

<script setup lang="ts">
const props = defineProps<{
  allianceName: string
}>()

const allianceAcronyms = [
  { code: 'CCC', name: 'Christian Coalition Of Countries' },
  { code: 'FAN', name: 'Federation of Armed Nations' },
  { code: 'FARK', name: 'Fark' },
  { code: 'FTW', name: 'Freehold of the Wolves' },
  { code: 'MHA', name: 'Mostly Harmless Alliance' },
  { code: 'NATO', name: 'NATO' },
  { code: 'NG', name: 'Non Grata' },
  { code: 'OG', name: 'Old Guard' },
  { code: 'RIA', name: 'Random Insanity Alliance' },
  { code: 'TIO', name: 'The Imperial Order' },
]

const allianceAcronym = computed(() => {
  if (!props.allianceName) {
    return ''
  }

  const acronym = allianceAcronyms.find(
    (aa) => aa.name.localeCompare(props.allianceName, 'en', { sensitivity: 'base' }) === 0
  )

  if (acronym) {
    return acronym.code
  } else {
    return props.allianceName
      .split(/\s/)
      .reduce((result, word) => result + word.slice(0, 1), '')
  }
})

const allianceImage = computed(() => {
  let fileName = 'none'

  // Since we already have a computed property for figuring out a known acronym, only change
  // the fileName if that property is one of those known values
  if (allianceAcronyms.some((aa) => aa.code === allianceAcronym.value)) {
    fileName = allianceAcronym.value.toLowerCase()
  }

  return `/assets/img/alliance/${fileName}.jpg`
})
</script>
