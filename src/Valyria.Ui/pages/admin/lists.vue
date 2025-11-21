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
              <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Message Template</h3>
            </template>

            <div class="space-y-4">
              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Carbon Copy Rulers</label>
                <UTextarea
                  :model-value="getRulerList(selectedList)"
                  readonly
                  :rows="5"
                  autoresize
                  class="font-mono text-sm w-full"
                />
              </div>

              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Subject</label>
                <UInput
                  :model-value="selectedList.subject.replace('{{date}}', todayFormatted)"
                  readonly
                  class="w-full"
                />
              </div>

              <div>
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Message</label>
                <UTextarea
                  :model-value="selectedList.message"
                  readonly
                  :rows="10"
                  autoresize
                  class="font-mono text-sm w-full"
                />
              </div>
            </div>
          </UCard>

          <!-- Recipients Data Section -->
          <UCard>
            <template #header>
              <h3 class="text-lg font-semibold text-gray-900 dark:text-white">Recipients</h3>
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
const selectedTab = ref<number>(0)

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

// Helper function to get ruler list as string
const getRulerList = (aidList: AidList): string => {
  return aidList.recipients.map(recipient => recipient.nation.rulerName).join('\r\n')
}

// Helper function to get nation message link
const getNationMessageLink = (nation: NationSimplified): string => {
  return `https://www.cybernations.net/send_message.asp?Nation_ID=${nation.nationId}`
}

// Helper function to get activity description
const getActivityDescription = (activityId: number): string => {
  const activity = recentActivityValues.find(v => v.id === activityId)
  return activity ? activity.description : 'Unknown'
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
