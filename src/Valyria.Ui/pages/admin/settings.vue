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

    <!-- Add/Edit Admin Access Dialog -->
    

    <!-- Add/Edit Team Event Dialog -->
    <UModal v-model="teamEventDialogOpen">
      <UCard>
        <template #header>
          <h3 class="text-lg font-semibold">{{ editingTeamEvent.id ? 'Edit' : 'Add' }} Team Event</h3>
        </template>

        <div class="space-y-4">
          <UFormGroup label="Team">
            <USelect
              v-model="editingTeamEvent.team"
              :options="teamOptions"
              placeholder="Select team"
            />
          </UFormGroup>

          <UFormGroup label="Event Type">
            <USelect
              v-model="editingTeamEvent.eventType"
              :options="eventTypeOptions"
              placeholder="Select event type"
            />
          </UFormGroup>

          <UFormGroup label="Description (optional)">
            <UTextarea v-model="editingTeamEvent.description" placeholder="Event description" />
          </UFormGroup>
        </div>

        <template #footer>
          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="teamEventDialogOpen = false">
              Cancel
            </UButton>
            <UButton color="primary" @click="saveTeamEvent()">
              Save
            </UButton>
          </div>
        </template>
      </UCard>
    </UModal>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import { useAuthStore } from '~/stores/auth'
import AdminAccessSettingsDialog from '~/components/admin/AdminAccessSettingsDialog.vue'

const router = useRouter()
const toast = useToast()
const overlay = useOverlay()

const adminAccessModal = overlay.create(AdminAccessSettingsDialog)

const authStore = useAuthStore()
const loggedInUser = computed(() => authStore.loggedInUser)

// Admin Access state
const adminLoading = ref<boolean>(false)
const adminSearchQuery = ref<string>('')
const adminDialogOpen = ref<boolean>(false)
const editingAdmin = ref<any>({})
const adminAccessList = ref<any[]>([
  { id: 1, rulerName: 'lilweirdward', nationName: 'Land of Too Much Fun', accountId: 12345, role: 'Admin' },
  { id: 2, rulerName: 'dev0win', nationName: 'dont remember D:', accountId: 67890, role: 'Admin - Lists' },
  { id: 3, rulerName: 'A1ph4 0m3ga', nationName: 'Paradisia', accountId: 54321, role: 'Admin' }
])

// Team Events state
const teamEventLoading = ref<boolean>(false)
const teamEventSearchQuery = ref<string>('')
const teamEventDialogOpen = ref<boolean>(false)
const editingTeamEvent = ref<any>({})
const teamEventsList = ref<any[]>([
  { id: 1, team: 'Aqua', eventType: '+1 Aid Slot', eventName: 'Tournament bonus' },
  { id: 2, team: 'Red', eventType: '-1 Aid Slot', eventName: 'Penalty for violation' },
  { id: 3, team: 'Green', eventType: '+1 Aid Slot', eventName: 'Event reward' }
])

// Team options
const teamOptions = [
  { label: 'Aqua', value: 'Aqua' },
  { label: 'White', value: 'White' },
  { label: 'Red', value: 'Red' },
  { label: 'Green', value: 'Green' },
  { label: 'Brown', value: 'Brown' },
  { label: 'Blue', value: 'Blue' },
  { label: 'Purple', value: 'Purple' },
  { label: 'Orange', value: 'Orange' },
  { label: 'Maroon', value: 'Maroon' },
  { label: 'Black', value: 'Black' },
  { label: 'Yellow', value: 'Yellow' },
  { label: 'Pink', value: 'Pink' },
  { label: 'None (Grey)', value: 'None (Grey)' }
]

// Event type options
const eventTypeOptions = [
  { label: '+1 Aid Slot', value: '+1 Aid Slot' },
  { label: '-1 Aid Slot', value: '-1 Aid Slot' }
]

// Admin Access columns
const adminColumns: TableColumn<any>[] = [
  {
    accessorKey: 'rulerName',
    header: 'Ruler Name'
  },
  {
    accessorKey: 'nationName',
    header: 'Nation Name'
  },
  {
    accessorKey: 'role',
    header: 'Role',
    cell: ({ row }) => {
      const roleColor = row.original.role === 'Admin' ? 'error' : 'warning'
      return h(resolveComponent('UBadge'), {
        color: roleColor,
        variant: 'subtle'
      }, () => row.original.role)
    }
  },
  {
    accessorKey: 'id',
    header: 'Actions',
    cell: ({ row }) => {
      const admin = row.original
      return h('div', { class: 'flex gap-2' }, [
        h(resolveComponent('UButton'), {
          label: 'Edit',
          color: 'primary',
          variant: 'soft',
          size: 'xs',
          onClick: () => editAdminAccess(admin)
        }),
        h(resolveComponent('UButton'), {
          label: 'Remove',
          color: 'error',
          variant: 'soft',
          size: 'xs',
          onClick: () => removeAdminAccess(admin.id)
        })
      ])
    }
  }
]

// Team Events columns
const teamEventColumns: TableColumn<any>[] = [
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
  return adminAccessList.value.filter((admin: any) => {
    return (
      admin.accountName?.toLowerCase().includes(query) ||
      admin.role?.toLowerCase().includes(query)
    )
  })
})

// Filtered team events list
const filteredTeamEvents = computed(() => {
  if (!teamEventSearchQuery.value) {
    return teamEventsList.value
  }

  const query = teamEventSearchQuery.value.toLowerCase()
  return teamEventsList.value.filter((event: any) => {
    return event.team?.toLowerCase().includes(query)
  })
})

// Admin Access functions
const addAdminAccess = (): void => {
  adminAccessModal.open({
    editingAdmin: {} as AdminAccount
  })
}

const editAdminAccess = (admin: any): void => {
  adminAccessModal.open({
    editingAdmin: { ...admin }
  })
}

const removeAdminAccess = (adminId: number): void => {
  const index = adminAccessList.value.findIndex(a => a.id === adminId)
  if (index !== -1) {
    adminAccessList.value.splice(index, 1)
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Admin access removed successfully.',
      duration: 3000
    })
  }
}

// Team Events functions
const addTeamEvent = (): void => {
  editingTeamEvent.value = {}
  teamEventDialogOpen.value = true
}

const editTeamEvent = (event: any): void => {
  editingTeamEvent.value = { ...event }
  teamEventDialogOpen.value = true
}

const saveTeamEvent = (): void => {
  if (editingTeamEvent.value.id) {
    // Update existing
    const index = teamEventsList.value.findIndex(e => e.id === editingTeamEvent.value.id)
    if (index !== -1) {
      teamEventsList.value[index] = { ...editingTeamEvent.value }
    }
  } else {
    // Add new
    const newId = Math.max(...teamEventsList.value.map(e => e.id), 0) + 1
    teamEventsList.value.push({
      ...editingTeamEvent.value,
      id: newId
    })
  }

  toast.add({
    color: 'success',
    title: 'Success',
    description: 'Team event saved successfully.',
    duration: 3000
  })

  teamEventDialogOpen.value = false
  editingTeamEvent.value = {}
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
  // Check if user has admin role
  if (!loggedInUser.value?.roles?.some((role: any) => role.name === 'Admin')) {
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

  // TODO: Load actual data from backend API
  adminLoading.value = false
  teamEventLoading.value = false
})
</script>
