<template>
  <UContainer>
    <div class="py-8 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">VEP Lists</h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">View and manage the lists of nations that should be sending or receiving aid</p>
      </div>

      <!-- Loading State -->
      <div v-if="isLoading" class="flex justify-center items-center py-12">
        <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"></div>
      </div>

      <!-- Lists Tabs -->
      <div v-else>
        <UTabs v-model="selectedTab" :items="tabItems" class="w-full" />

        <div v-if="selectedList" class="mt-6 space-y-6">
          <!-- Message Template Section -->
          <UCard>
            <template #header>
              <div class="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-4">
                <div class="flex-1">
                  <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Message Template</h3>
                  <p v-if="selectedList.recipientsLastUpdatedOn" class="text-sm text-gray-500 dark:text-gray-400 mt-1">
                    Updated On: {{ formatDate(selectedList.recipientsLastUpdatedOn) }}
                  </p>
                </div>

                <div class="flex flex-col gap-2">
                  <div class="flex items-center gap-2">
                    <USwitch
                      :model-value="!!selectedList.messagesSentOn"
                      :disabled="isUpdatingMessagesSent"
                      label="Messages Sent"
                      @update:model-value="handleMessagesSentToggle"
                    />
                    <div v-if="isUpdatingMessagesSent" class="animate-spin rounded-full h-4 w-4 border-b-2 border-primary-500"></div>
                  </div>
                  <p v-if="selectedList.messagesSentOn && selectedList.messagesSentBy" class="text-sm text-gray-500 dark:text-gray-400">
                    Sent by {{ selectedList.messagesSentBy.rulerName }} on {{ formatDate(selectedList.messagesSentOn) }}
                  </p>
                </div>
              </div>
            </template>

            <div class="space-y-4">
              <!-- First Recipient Row -->
              <div v-if="firstRecipient" class="flex items-center gap-4 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
                <div class="flex-1">
                  <div class="text-sm font-medium text-gray-700 dark:text-gray-300">First Recipient</div>
                  <div class="font-semibold text-gray-900 dark:text-white">{{ firstRecipient.nation.rulerName }}</div>
                  <div class="text-sm text-gray-500 dark:text-gray-400">{{ firstRecipient.nation.nationName }}</div>
                </div>
                <UButton
                  label="Send Message"
                  color="primary"
                  :to="getNationMessageLink(firstRecipient.nation)"
                  target="_blank"
                  external
                  icon="i-heroicons-paper-airplane"
                />
              </div>

              <!-- Carbon Copy Rulers -->
              <div v-if="ccRecipients.length > 0">
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                  Carbon Copy Rulers ({{ ccRecipients.length }})
                </label>
                <UTextarea
                  :model-value="getCCRulerList()"
                  readonly
                  :rows="5"
                  autoresize
                  class="font-mono text-sm w-full cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
                  @click="copyToClipboard(getCCRulerList(), 'Carbon Copy Rulers')"
                />
              </div>

              <!-- Subject -->
              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Subject</label>
                <UInput
                  :model-value="selectedList.subject.replace('{{date}}', todayFormatted)"
                  readonly
                  class="w-full cursor-pointer"
                  @click="copyToClipboard(selectedList.subject.replace('{{date}}', todayFormatted), 'Subject')"
                />
              </div>

              <!-- Message -->
              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Message</label>
                <UTextarea
                  :model-value="selectedList.message"
                  readonly
                  :rows="10"
                  autoresize
                  class="font-mono text-sm w-full cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
                  @click="copyToClipboard(selectedList.message, 'Message')"
                />
              </div>

              <!-- Batch Pagination -->
              <div v-if="totalBatches > 1" class="flex items-center justify-between pt-4 border-t border-gray-200 dark:border-gray-700">
                <div class="text-sm text-gray-600 dark:text-gray-400">
                  Batch {{ currentBatchIndex + 1 }} of {{ totalBatches }}
                  <span class="text-gray-500">({{ selectedList.recipients.length }} total recipients)</span>
                </div>
                <div class="flex gap-2">
                  <UButton
                    icon="i-heroicons-chevron-left"
                    color="neutral"
                    variant="soft"
                    :disabled="currentBatchIndex === 0"
                    @click="goToBatch(currentBatchIndex - 1)"
                  />
                  <UButton
                    icon="i-heroicons-chevron-right"
                    color="neutral"
                    variant="soft"
                    :disabled="currentBatchIndex >= totalBatches - 1"
                    @click="goToBatch(currentBatchIndex + 1)"
                  />
                </div>
              </div>
            </div>
          </UCard>

          <!-- Recipients Data Section -->
          <UCard>
            <template #header>
              <h3 class="text-lg font-semibold text-gray-900 dark:text-white">All Recipients</h3>
            </template>

            <UTable
              :data="selectedList.recipients"
              :columns="getColumnsForList(selectedList)"
            />
          </UCard>
        </div>
      </div>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { storeToRefs } from 'pinia'
import { useAdminListsStore, type AidList } from '~/stores/adminLists'
import { useAuthStore } from '~/stores/auth'
import { recentActivityValues } from '~/infrastructure/dataLists'
import type { NationSimplified } from '~/stores/shared/nationSimplified'

const router = useRouter()
const toast = useToast()

const adminListsStore = useAdminListsStore()
const authStore = useAuthStore()

const { lists } = storeToRefs(adminListsStore)
const { loggedInUser } = storeToRefs(authStore)

const isLoading = ref<boolean>(true)
const isUpdatingMessagesSent = ref<boolean>(false)
const selectedTab = ref<number>(0)
const currentBatchIndex = ref<number>(0)
const BATCH_SIZE = 25

// Computed property for today's formatted date
const todayFormatted = computed<string>(() => {
  const now = new Date()
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  const month = months[now.getMonth()]
  const day = now.getDate().toString().padStart(2, '0')
  return `${month} ${day}`
})

// Computed property for tab items
const tabItems = computed(() => {
  return lists.value.map(list => ({
    value: list.id - 1, // This is really hacky but I'm lazy and punishing my future self with having to fix this
    label: list.name,
    list: list
  }))
})

// Computed property for currently selected list
const selectedList = computed<AidList | undefined>(() => {
  if (selectedTab.value >= 0 && selectedTab.value < tabItems.value.length) {
    return tabItems.value[selectedTab.value].list
  }
  return undefined
})

// Computed property for total number of batches
const totalBatches = computed<number>(() => {
  if (!selectedList.value) return 0
  return Math.ceil(selectedList.value.recipients.length / BATCH_SIZE)
})

// Computed property for current batch recipients
const currentBatch = computed(() => {
  if (!selectedList.value) return []
  const startIndex = currentBatchIndex.value * BATCH_SIZE
  const endIndex = startIndex + BATCH_SIZE
  return selectedList.value.recipients.slice(startIndex, endIndex)
})

// Computed property for first recipient in current batch
const firstRecipient = computed(() => {
  return currentBatch.value[0] || null
})

// Computed property for CC recipients (all except first in batch)
const ccRecipients = computed(() => {
  return currentBatch.value.slice(1)
})

// Helper function to get ruler list as string for CC recipients
const getCCRulerList = (): string => {
  return ccRecipients.value.map(recipient => recipient.nation.rulerName).join('\r\n')
}

// Helper function to copy text to clipboard
const copyToClipboard = async (text: string, label: string) => {
  try {
    await navigator.clipboard.writeText(text)
    toast.add({
      color: 'success',
      title: 'Copied!',
      description: `${label} copied to clipboard.`,
      duration: 2000
    })
  } catch (error) {
    console.error('Failed to copy to clipboard:', error)
    toast.add({
      color: 'error',
      title: 'Copy Failed',
      description: 'Failed to copy to clipboard. Please try again.',
      duration: 3000
    })
  }
}

// Helper function to navigate batches
const goToBatch = (index: number) => {
  if (index >= 0 && index < totalBatches.value) {
    currentBatchIndex.value = index
  }
}

// Helper function to reset batch when tab changes
const resetBatch = () => {
  currentBatchIndex.value = 0
}

// Watch for tab changes and reset batch
watch(selectedTab, () => {
  resetBatch()
})

// Helper function to get nation message link
const getNationMessageLink = (nation: NationSimplified): string => {
  return `https://www.cybernations.net/send_message.asp?Nation_ID=${nation.nationId}`
}

// Helper function to get activity description
const getActivityDescription = (activityId: number): string => {
  const activity = recentActivityValues.find(v => v.id === activityId)
  return activity ? activity.description : 'Unknown'
}

// Helper function to format date
const formatDate = (date: Date): string => {
  const d = new Date(date)
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  const month = months[d.getMonth()]
  const day = d.getDate().toString().padStart(2, '0')
  const year = d.getFullYear()
  const hours = d.getHours().toString().padStart(2, '0')
  const minutes = d.getMinutes().toString().padStart(2, '0')
  const seconds = d.getSeconds().toString().padStart(2, '0')
  return `${month} ${day}, ${year} ${hours}:${minutes}:${seconds}`
}

// Helper function to handle messages sent toggle
const handleMessagesSentToggle = async (newValue: boolean) => {
  if (!selectedList.value || !loggedInUser.value) return

  isUpdatingMessagesSent.value = true

  try {
    let messagesSentOn: Date | null = null
    let messagesSentByAccountId: number | null = null

    if (newValue) {
      // Convert to CST (UTC-6)
      const now = new Date()
      const localOffset = now.getTimezoneOffset() // Local offset in minutes from UTC
      const cstOffset = 6 * 60 // CST is UTC-6, so 6 hours * 60 minutes
      const cstTime = new Date(now.getTime() + (localOffset - cstOffset) * 60 * 1000)
      console.log(localOffset, cstOffset, cstTime)

      messagesSentOn = cstTime
      messagesSentByAccountId = loggedInUser.value.accountId
    }

    await adminListsStore.updateMessagesSent(
      selectedList.value.id,
      messagesSentOn,
      messagesSentByAccountId
    )

    toast.add({
      color: 'success',
      title: 'Success',
      description: newValue ? 'Messages marked as sent.' : 'Messages marked as not sent.',
      duration: 3000
    })
  } catch (error) {
    console.error('Failed to update messages sent:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to update messages sent status. Please try again.',
      duration: 5000
    })
  } finally {
    isUpdatingMessagesSent.value = false
  }
}

// Helper function to get columns for a specific list
const getColumnsForList = (list: AidList): TableColumn<any>[] => {
  const baseColumns: TableColumn<any>[] = [
    {
      accessorKey: 'nation',
      header: 'Nation',
      cell: ({ row }) => {
        const recipient = row.original
        return h('div', [
          h('div', { class: 'font-medium text-gray-900 dark:text-white' }, recipient.nation.rulerName),
          h('div', { class: 'text-sm text-gray-500 dark:text-gray-400' }, recipient.nation.nationName)
        ])
      }
    },
    {
      accessorKey: 'slotsFree',
      header: 'Slots Free',
      cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.slotsFree)
    }
  ]

  // Conditionally add credit or debt column based on list type
  if (list.name.includes('Incoming')) {
    baseColumns.push({
      accessorKey: 'credit',
      header: 'Credit',
      cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.credit)
    })
  } else {
    baseColumns.push({
      accessorKey: 'debt',
      header: 'Debt',
      cell: ({ row }) => h('span', { class: 'font-mono text-gray-900 dark:text-white' }, row.original.debt)
    })
  }

  // Add remaining columns
  baseColumns.push(
    {
      accessorKey: 'recentActivity',
      header: 'Activity',
      cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' }, getActivityDescription(row.original.recentActivity))
    },
    {
      accessorKey: 'discord',
      header: 'Discord Tag',
      cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' }, row.original.discord || '-')
    },
    {
      accessorKey: 'id',
      header: 'Message',
      cell: ({ row }) => {
        const recipient = row.original
        return h(resolveComponent('UButton'), {
          label: 'Send Message',
          color: 'primary',
          variant: 'soft',
          size: 'xs',
          to: getNationMessageLink(recipient.nation),
          target: '_blank',
          external: true
        })
      }
    }
  )

  return baseColumns
}

// Check admin access and load lists on mount
onMounted(async () => {
  // Check if user has admin or admin-lists role
  const hasAccess = loggedInUser.value?.roles?.some((role: any) =>
    role.name === 'Admin' || role.name === 'Admin - Lists'
  )

  if (!hasAccess) {
    console.warn('Tried to access an admin page without the admin role.')
    toast.add({
      color: 'error',
      title: 'Access Denied',
      description: 'You do not have permission to access this page.',
      duration: 5000
    })
    await router.push('/')
    return
  }

  try {
    await adminListsStore.getAllAidLists()
    console.log('Lists loaded:', lists.value)
  } catch (error) {
    console.error('Failed to load lists:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load aid lists. Please try again.',
      duration: 5000
    })
  } finally {
    isLoading.value = false
  }
})
</script>
