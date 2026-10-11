<template>
  <UContainer>
    <div class="py-8 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">VEP Settings</h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">Manage administrative settings and configurations for the VEP program</p>
      </div>

      <!-- Manage Admin Access Section -->
      <UCard>
        <template #header>
          <div class="flex justify-between items-center">
            <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Manage Admin Access</h2>
            <UButton
              color="success"
              icon="i-heroicons-plus"
              @click="addAdminAccess()"
            >
              Add Admin
            </UButton>
          </div>
        </template>

        <div class="space-y-4">
          <!-- Search -->
          <UInput
            v-model="adminSearchQuery"
            placeholder="Search by account name or role..."
            icon="i-heroicons-magnifying-glass"
            class="w-full max-w-md"
          />

          <!-- Admin Access Table -->
          <UTable
            :data="filteredAdminAccess"
            :columns="adminColumns"
            :loading="adminLoading"
            :sort="{ column: 'accountName', direction: 'asc' }"
          />
        </div>
      </UCard>

      <!-- Manage Team Events Section -->
      <UCard>
        <template #header>
          <div class="flex justify-between items-center">
            <h2 class="text-xl font-semibold text-gray-900 dark:text-white">Manage Team Events</h2>
            <UButton
              color="success"
              icon="i-heroicons-plus"
              @click="addTeamEvent()"
            >
              Add Team Event
            </UButton>
          </div>
        </template>

        <div class="space-y-4">
          <!-- Search -->
          <UInput
            v-model="teamEventSearchQuery"
            placeholder="Search by team name..."
            icon="i-heroicons-magnifying-glass"
            class="w-full max-w-md"
          />

          <!-- Team Events Table -->
          <UTable
            :data="filteredTeamEvents"
            :columns="teamEventColumns"
            :loading="teamEventLoading"
            :sort="{ column: 'team', direction: 'asc' }"
          />
        </div>
      </UCard>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { useAdminAccessStore, type AdminAccessSetting } from '~/stores/adminAccess'
import AdminAccessSettingsDialog from '~/components/admin/AdminAccessSettingsDialog.vue'
import AdminTeamEventDialog from '~/components/admin/AdminTeamEventDialog.vue'
import type { TeamEvent } from '~/stores/shared/teamEvent'

const toast = useToast()
const overlay = useOverlay()

const adminAccessModal = overlay.create(AdminAccessSettingsDialog)
const teamEventModal = overlay.create(AdminTeamEventDialog)

const adminAccessStore = useAdminAccessStore()

// Admin Access state
const adminSearchQuery = ref<string>('')
const adminLoading = computed(() => adminAccessStore.loading)
const adminAccessList = computed(() => adminAccessStore.adminAccessSettings)

// Team Events state
const teamEventLoading = ref<boolean>(false)
const teamEventSearchQuery = ref<string>('')
const teamEventsList = ref<TeamEvent[]>([
  { id: 1, team: 'Aqua', eventType: '+1 Aid Slot', eventName: 'Tournament bonus' },
  { id: 2, team: 'Red', eventType: '-1 Aid Slot', eventName: 'Penalty for violation' },
  { id: 3, team: 'Green', eventType: '+1 Aid Slot', eventName: 'Event reward' }
])

// Admin Access columns
const adminColumns: TableColumn<AdminAccessSetting>[] = [
  {
    accessorKey: 'rulerName',
    header: 'Ruler Name'
  },
  {
    accessorKey: 'nationName',
    header: 'Nation Name'
  },
  {
    accessorKey: 'adminRoleName',
    header: 'Role',
    cell: ({ row }) => {
      const roleColor = row.original.adminRoleName === 'Super Admin' ? 'tertiary' : 'secondary'
      return h(resolveComponent('UBadge'), {
        color: roleColor,
        variant: 'subtle'
      }, () => row.original.adminRoleName)
    }
  },
  {
    accessorKey: 'accountId',
    header: 'Actions',
    cell: ({ row }) => {
      const admin = row.original
      return h(resolveComponent('UButton'), {
        label: 'Remove',
        color: 'error',
        variant: 'soft',
        size: 'xs',
        onClick: () => removeAdminAccess(admin.accountId, admin.adminRoleId)
      })
    }
  }
]

// Team Events columns
const teamEventColumns: TableColumn<TeamEvent>[] = [
  {
    accessorKey: 'team',
    header: 'Team',
    cell: ({ row }) => {
      const teamColors: Record<string, string> = {
        'Aqua': 'bg-cyan-500',
        'White': 'bg-gray-100 border border-gray-300',
        'Red': 'bg-red-500',
        'Green': 'bg-green-500',
        'Brown': 'bg-amber-700',
        'Blue': 'bg-blue-500',
        'Purple': 'bg-purple-500',
        'Orange': 'bg-orange-500',
        'Maroon': 'bg-red-900',
        'Black': 'bg-gray-900',
        'Yellow': 'bg-yellow-400',
        'Pink': 'bg-pink-500',
        'None (Grey)': 'bg-gray-400'
      }
      const team = row.original.team
      const colorClass = teamColors[team] || 'bg-gray-400'
      return h('div', { class: 'flex items-center gap-2' }, [
        h('div', { class: `w-4 h-4 rounded ${colorClass}` }),
        h('span', { class: 'font-medium text-gray-900 dark:text-white' }, team)
      ])
    }
  },
  {
    accessorKey: 'eventType',
    header: 'Event Type',
    cell: ({ row }) => {
      const eventType = row.original.eventType
      const color = eventType === '+1 Aid Slot' ? 'success' : 'error'
      return h(resolveComponent('UBadge'), {
        color: color,
        variant: 'subtle'
      }, () => eventType)
    }
  },
  {
    accessorKey: 'eventName',
    header: 'Event Name',
    cell: ({ row }) => h('span', { class: 'text-gray-900 dark:text-white' }, row.original.eventName || '-')
  },
  {
    accessorKey: 'id',
    header: 'Actions',
    cell: ({ row }) => {
      const event = row.original
      return h('div', { class: 'flex gap-2' }, [
        h(resolveComponent('UButton'), {
          label: 'Edit',
          color: 'primary',
          variant: 'soft',
          size: 'xs',
          onClick: () => editTeamEvent(event)
        }),
        h(resolveComponent('UButton'), {
          label: 'Delete',
          color: 'error',
          variant: 'soft',
          size: 'xs',
          onClick: () => deleteTeamEvent(event.id)
        })
      ])
    }
  }
]

// Filtered admin access list
const filteredAdminAccess = computed(() => {
  if (!adminSearchQuery.value) {
    return adminAccessList.value
  }

  const query = adminSearchQuery.value.toLowerCase()
  return adminAccessList.value.filter((admin) => {
    return (
      admin.rulerName?.toLowerCase().includes(query) ||
      admin.adminRoleName?.toLowerCase().includes(query)
    )
  })
})

// Filtered team events list
const filteredTeamEvents = computed(() => {
  if (!teamEventSearchQuery.value) {
    return teamEventsList.value
  }

  const query = teamEventSearchQuery.value.toLowerCase()
  return teamEventsList.value.filter((event) => {
    return event.team?.toLowerCase().includes(query)
  })
})

// Admin Access functions
const addAdminAccess = (): void => {
  adminAccessModal.open({
    editingAdmin: {} as AdminAccessSetting
  })
}

const removeAdminAccess = async (accountId: number, adminRoleId: number): Promise<void> => {
  // TODO: Put this back once I'm comfortable with people being able to do this lol
  const success = await adminAccessStore.deleteAdminAccess({ accountId, adminRoleId })

  if (success) {
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Admin access removed successfully.',
      duration: 3000
    })
  } else {
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to remove admin access. LWW still implementing this.',
      duration: 3000
    })
  }
}

// Team Events functions
const addTeamEvent = (): void => {
  teamEventModal.open({
    editingTeamEvent: {}
  })
}

const editTeamEvent = (event: TeamEvent): void => {
  teamEventModal.open({
    editingTeamEvent: { ...event }
  })
}

const deleteTeamEvent = (eventId: number): void => {
  const index = teamEventsList.value.findIndex(e => e.id === eventId)
  if (index !== -1) {
    teamEventsList.value.splice(index, 1)
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Team event deleted successfully.',
      duration: 3000
    })
  }
}

// Check admin access on mount
onMounted(async () => {
  // Load admin access data from backend API
  try {
    await adminAccessStore.loadAdminAccessSettings()
  } catch (error) {
    console.error('Failed to load admin access settings:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load admin access settings.',
      duration: 5000
    })
  }

  // TODO: Load team events data from backend API
  teamEventLoading.value = false
})
</script>
