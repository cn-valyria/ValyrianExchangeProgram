<template>
  <UContainer>
    <div class="py-8 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">
          VEP Mass Messages
        </h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">
          Send today's CN mass messages to the nations that need to take action
          on their deals
        </p>
      </div>

      <!-- Loading State -->
      <div v-if="isLoading" class="flex justify-center items-center py-12">
        <div
          class="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-500"
        ></div>
      </div>

      <!-- Message Tabs -->
      <div v-else-if="summary">
        <UTabs v-model="selectedTab" :items="tabItems" class="w-full" />

        <div v-if="selectedMessage" class="mt-6 space-y-6">
          <!-- Message Template Section -->
          <UCard>
            <template #header>
              <div
                class="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-4"
              >
                <div class="flex-1">
                  <h3
                    class="text-lg font-semibold text-gray-900 dark:text-white"
                  >
                    Message Template
                  </h3>
                  <p class="text-sm text-gray-600 dark:text-gray-400 mt-1">
                    {{ selectedMessage.description }}
                  </p>
                  <p
                    v-if="summary.dealStatesUpdatedOn"
                    class="text-sm text-gray-500 dark:text-gray-400 mt-1"
                  >
                    Deal Activity Updated On:
                    {{ formatDateTime(summary.dealStatesUpdatedOn) }}
                  </p>
                </div>

                <div class="flex flex-col gap-2">
                  <div class="flex items-center gap-2">
                    <USwitch
                      :model-value="!!selectedMessage.sentOn"
                      :disabled="isUpdatingMessagesSent || !canMarkSent"
                      label="Messages Sent"
                      @update:model-value="handleMessagesSentToggle"
                    />
                    <div
                      v-if="isUpdatingMessagesSent"
                      class="animate-spin rounded-full h-4 w-4 border-b-2 border-primary-500"
                    ></div>
                  </div>
                  <p
                    v-if="selectedMessage.sentOn && selectedMessage.sentBy"
                    class="text-sm text-gray-500 dark:text-gray-400"
                  >
                    Sent by {{ selectedMessage.sentBy.rulerName }} on
                    {{ formatDateTime(selectedMessage.sentOn) }}
                  </p>
                </div>
              </div>
            </template>

            <div
              v-if="selectedMessage.recipients.length === 0"
              class="py-6 text-center text-gray-500 dark:text-gray-400"
            >
              No one needs this message today.
            </div>

            <div v-else class="space-y-4">
              <!-- First Recipient Row -->
              <div
                v-if="firstRecipient"
                class="flex items-center gap-4 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg"
              >
                <div class="flex-1">
                  <div
                    class="text-sm font-medium text-gray-700 dark:text-gray-300"
                  >
                    First Recipient
                  </div>
                  <div class="font-semibold text-gray-900 dark:text-white">
                    {{ firstRecipient.rulerName }}
                  </div>
                  <div class="text-sm text-gray-500 dark:text-gray-400">
                    {{ firstRecipient.nationName }}
                  </div>
                </div>
                <UButton
                  label="Send Message"
                  color="primary"
                  :to="getSendMessageLink(firstRecipient.nationId)"
                  target="_blank"
                  external
                  icon="i-heroicons-paper-airplane"
                />
              </div>

              <!-- Carbon Copy Rulers -->
              <div v-if="ccRecipients.length > 0">
                <label
                  class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2"
                >
                  Carbon Copy Rulers ({{ ccRecipients.length }})
                </label>
                <UTextarea
                  :model-value="ccRulerList"
                  readonly
                  :rows="5"
                  autoresize
                  class="font-mono text-sm w-full cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
                  @click="copyToClipboard(ccRulerList, 'Carbon Copy Rulers')"
                />
              </div>

              <!-- Subject -->
              <div>
                <label
                  class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2"
                  >Subject</label
                >
                <UInput
                  :model-value="filledSubject"
                  readonly
                  class="w-full cursor-pointer"
                  @click="copyToClipboard(filledSubject, 'Subject')"
                />
              </div>

              <!-- Message -->
              <div>
                <label
                  class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2"
                  >Message</label
                >
                <UTextarea
                  :model-value="filledMessage"
                  readonly
                  :rows="10"
                  autoresize
                  class="font-mono text-sm w-full cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
                  @click="copyToClipboard(filledMessage, 'Message')"
                />
              </div>

              <!-- Batch Pagination -->
              <div
                v-if="batches.length > 1"
                class="flex items-center justify-between pt-4 border-t border-gray-200 dark:border-gray-700"
              >
                <div class="text-sm text-gray-600 dark:text-gray-400">
                  Batch {{ currentBatchIndex + 1 }} of {{ batches.length }}
                  <span class="text-gray-500"
                    >({{ selectedMessage.recipients.length }} total
                    recipients)</span
                  >
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
                    :disabled="currentBatchIndex >= batches.length - 1"
                    @click="goToBatch(currentBatchIndex + 1)"
                  />
                </div>
              </div>
            </div>
          </UCard>

          <!-- Recipients Data Section -->
          <UCard>
            <template #header>
              <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                All Recipients
              </h3>
            </template>

            <UTable
              :data="selectedMessage.recipients"
              :columns="recipientColumns"
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
import {
  describeRecipientActions,
  fillTemplate,
  formatCnDate,
  getBatches,
  getSendMessageLink,
} from '~/infrastructure/massMessages'
import {
  MassMessageCode,
  useAdminMassMessagesStore,
  type MassMessage,
  type MassMessageRecipient,
} from '~/stores/adminMassMessages'
import { useAuthStore } from '~/stores/auth'

const toast = useToast()
const requestUrl = useRequestURL()

const adminMassMessagesStore = useAdminMassMessagesStore()
const authStore = useAuthStore()

const { summary } = storeToRefs(adminMassMessagesStore)
const { loggedInUser } = storeToRefs(authStore)

const isLoading = ref<boolean>(true)
const isUpdatingMessagesSent = ref<boolean>(false)
const selectedTab = ref<string>('')
const currentBatchIndex = ref<number>(0)

// Read-only admins can see the messages but not mark them sent
const canMarkSent = computed(() => {
  return (
    loggedInUser.value?.roles?.some((role) =>
      ['Super Admin', 'Admin - Lists'].includes(role.name),
    ) ?? false
  )
})

const tabItems = computed(() => {
  return (summary.value?.messages ?? []).map((message) => ({
    value: String(message.id),
    label: `${message.name} (${message.recipients.length})`,
    icon: message.sentOn ? 'i-heroicons-check-circle' : undefined,
  }))
})

const selectedMessage = computed<MassMessage | undefined>(() => {
  return summary.value?.messages.find(
    (message) => String(message.id) === selectedTab.value,
  )
})

const templateValues = computed(() => ({
  date: summary.value ? formatCnDate(summary.value.cnDate) : '',
  siteUrl: requestUrl.origin,
}))

const filledSubject = computed<string>(() => {
  return selectedMessage.value
    ? fillTemplate(selectedMessage.value.subject, templateValues.value)
    : ''
})

const filledMessage = computed<string>(() => {
  return selectedMessage.value
    ? fillTemplate(selectedMessage.value.message, templateValues.value)
    : ''
})

const batches = computed(() =>
  getBatches(selectedMessage.value?.recipients ?? []),
)

const currentBatch = computed(
  () => batches.value[currentBatchIndex.value] ?? [],
)

// First recipient in the current batch is who the message is addressed to
const firstRecipient = computed(() => currentBatch.value[0] ?? null)

// Everyone else in the batch is carbon-copied
const ccRecipients = computed(() => currentBatch.value.slice(1))

const ccRulerList = computed<string>(() =>
  ccRecipients.value.map((recipient) => recipient.rulerName).join('\r\n'),
)

const copyToClipboard = async (text: string, label: string) => {
  try {
    await navigator.clipboard.writeText(text)
    toast.add({
      color: 'success',
      title: 'Copied!',
      description: `${label} copied to clipboard.`,
      duration: 2000,
    })
  } catch (error) {
    console.error('Failed to copy to clipboard:', error)
    toast.add({
      color: 'error',
      title: 'Copy Failed',
      description: 'Failed to copy to clipboard. Please try again.',
      duration: 3000,
    })
  }
}

const goToBatch = (index: number) => {
  if (index >= 0 && index < batches.value.length) {
    currentBatchIndex.value = index
  }
}

watch(selectedTab, () => {
  currentBatchIndex.value = 0
})

// Recipients can change if the data loader runs while the page is open
watch(batches, () => {
  if (currentBatchIndex.value >= batches.value.length) {
    currentBatchIndex.value = Math.max(batches.value.length - 1, 0)
  }
})

const formatDateTime = (date: string): string => {
  const d = new Date(date)
  const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ]
  const month = months[d.getMonth()]
  const day = d.getDate().toString().padStart(2, '0')
  const year = d.getFullYear()
  const hours = d.getHours().toString().padStart(2, '0')
  const minutes = d.getMinutes().toString().padStart(2, '0')
  return `${month} ${day}, ${year} ${hours}:${minutes}`
}

const handleMessagesSentToggle = async (newValue: boolean) => {
  if (!selectedMessage.value) return

  isUpdatingMessagesSent.value = true

  try {
    await adminMassMessagesStore.setMessageSent(
      selectedMessage.value.id,
      newValue,
    )

    toast.add({
      color: 'success',
      title: 'Success',
      description: newValue
        ? 'Messages marked as sent.'
        : 'Messages marked as not sent.',
      duration: 3000,
    })
  } catch (error) {
    console.error('Failed to update messages sent:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to update messages sent status. Please try again.',
      duration: 5000,
    })
  } finally {
    isUpdatingMessagesSent.value = false
  }
}

// Overdue columns only mean something for the flagged messages
const isFlaggedMessage = computed(() => {
  const code = selectedMessage.value?.code
  return (
    code === MassMessageCode.Overdue || code === MassMessageCode.FinalWarning
  )
})

const recipientColumns = computed<TableColumn<MassMessageRecipient>[]>(() => {
  const columns: TableColumn<MassMessageRecipient>[] = [
    {
      accessorKey: 'rulerName',
      header: 'Nation',
      cell: ({ row }) =>
        h('div', { class: 'flex items-center gap-2' }, [
          h(resolveComponent('ChipAllianceAcronym'), {
            allianceName: row.original.allianceName,
          }),
          h('div', [
            h(
              'div',
              { class: 'font-medium text-gray-900 dark:text-white' },
              row.original.rulerName,
            ),
            h(
              'div',
              { class: 'text-sm text-gray-500 dark:text-gray-400' },
              row.original.nationName,
            ),
          ]),
        ]),
    },
    {
      id: 'actions',
      header: 'Needs To',
      cell: ({ row }) =>
        h(
          'span',
          { class: 'text-gray-900 dark:text-white' },
          describeRecipientActions(row.original),
        ),
    },
  ]

  if (isFlaggedMessage.value) {
    columns.push(
      {
        accessorKey: 'daysOverdue',
        header: 'Days Overdue',
        cell: ({ row }) =>
          h(
            'span',
            { class: 'font-mono text-gray-900 dark:text-white' },
            row.original.daysOverdue ?? '-',
          ),
      },
      {
        accessorKey: 'cancelsOn',
        header: 'Cancels On',
        cell: ({ row }) =>
          h(
            'span',
            { class: 'font-mono text-gray-900 dark:text-white' },
            row.original.cancelsOn?.split('T')[0] ?? '-',
          ),
      },
    )
  }

  columns.push(
    {
      accessorKey: 'discord',
      header: 'Discord Tag',
      cell: ({ row }) =>
        h(
          'span',
          { class: 'text-gray-900 dark:text-white' },
          row.original.discord || '-',
        ),
    },
    {
      id: 'message',
      header: 'Message',
      cell: ({ row }) =>
        h(resolveComponent('UButton'), {
          label: 'Send Message',
          color: 'primary',
          variant: 'soft',
          size: 'xs',
          to: getSendMessageLink(row.original.nationId),
          target: '_blank',
          external: true,
        }),
    },
  )

  return columns
})

onMounted(async () => {
  try {
    await adminMassMessagesStore.loadMassMessages()
    selectedTab.value = tabItems.value[0]?.value ?? ''
  } catch (error) {
    console.error('Failed to load mass messages:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load mass messages. Please try again.',
      duration: 5000,
    })
  } finally {
    isLoading.value = false
  }
})
</script>
