<template>
  <UContainer>
    <div class="py-8 space-y-6">
      <!-- Header -->
      <div>
        <h1 class="text-3xl font-bold text-gray-900 dark:text-white">
          VEP Settings
        </h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">
          Manage administrative settings and configurations for the VEP program
        </p>
      </div>

      <!-- Manage Admin Access Section -->
      <UCard>
        <template #header>
          <div class="flex justify-between items-center">
            <h2 class="text-xl font-semibold text-gray-900 dark:text-white">
              Manage Admin Access
            </h2>
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
            <h2 class="text-xl font-semibold text-gray-900 dark:text-white">
              Manage Team Events
            </h2>
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
          <p class="text-sm text-gray-600 dark:text-gray-400">
            While a team's +1 aid slot event is in effect, every nation on the
            team gets a bonus aid slot, which VEP can match into a temporary
            deal. The slot and its deal end 30 days after the senate approves
            the event, or when the nation leaves the team.
          </p>

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
          />
        </div>
      </UCard>
    </div>
  </UContainer>
</template>

<script setup lang="ts">
import type { TableColumn } from '@nuxt/ui'
import {
  useAdminAccessStore,
  type AdminAccessSetting,
} from '~/stores/adminAccess'
import AdminAccessSettingsDialog from '~/components/admin/AdminAccessSettingsDialog.vue'
import AdminTeamEventDialog from '~/components/admin/AdminTeamEventDialog.vue'
import {
  useAdminTeamEventsStore,
  type TeamEvent,
} from '~/stores/adminTeamEvents'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import {
  cnToday,
  daysBetween,
  formatCnDate,
  teamName,
  teamSwatch,
} from '~/infrastructure/teamEvents'

const toast = useToast()
const overlay = useOverlay()

const adminAccessModal = overlay.create(AdminAccessSettingsDialog)
const teamEventModal = overlay.create(AdminTeamEventDialog)

const adminAccessStore = useAdminAccessStore()
const teamEventsStore = useAdminTeamEventsStore()

// Admin Access state
const adminSearchQuery = ref<string>('')
const adminLoading = computed(() => adminAccessStore.loading)
const adminAccessList = computed(() => adminAccessStore.adminAccessSettings)

// Team Events state
const teamEventLoading = computed(() => teamEventsStore.loading)
const teamEventSearchQuery = ref<string>('')
// Deleting cancels the event's bonus deals, so it takes a second click to confirm
const pendingDeleteId = ref<number | null>(null)
// Events in effect first, then newest first (the API's order)
const teamEventsList = computed(() =>
  [...teamEventsStore.teamEvents].sort(
    (a, b) => Number(b.isActive) - Number(a.isActive),
  ),
)

// Admin Access columns
const adminColumns: TableColumn<AdminAccessSetting>[] = [
  {
    accessorKey: 'rulerName',
    header: 'Ruler Name',
  },
  {
    accessorKey: 'nationName',
    header: 'Nation Name',
  },
  {
    accessorKey: 'adminRoleName',
    header: 'Role',
    cell: ({ row }) => {
      const roleColor =
        row.original.adminRoleName === 'Super Admin' ? 'tertiary' : 'secondary'
      return h(
        resolveComponent('UBadge'),
        {
          color: roleColor,
          variant: 'subtle',
        },
        () => row.original.adminRoleName,
      )
    },
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
        onClick: () => removeAdminAccess(admin.accountId, admin.adminRoleId),
      })
    },
  },
]

// Team Events columns
const teamEventColumns: TableColumn<TeamEvent>[] = [
  {
    accessorKey: 'team',
    header: 'Team',
    cell: ({ row }) =>
      h('div', { class: 'flex items-center gap-2' }, [
        h('div', { class: `w-4 h-4 rounded ${teamSwatch(row.original.team)}` }),
        h(
          'span',
          { class: 'font-medium text-gray-900 dark:text-white' },
          teamName(row.original.team),
        ),
      ]),
  },
  {
    accessorKey: 'approvedOn',
    header: 'Approved',
    cell: ({ row }) => formatCnDate(row.original.approvedOn),
  },
  {
    accessorKey: 'endsOn',
    header: 'In Effect Through',
    cell: ({ row }) => formatCnDate(row.original.endsOn),
  },
  {
    accessorKey: 'isActive',
    header: 'Status',
    cell: ({ row }) => {
      const event = row.original
      const daysLeft = daysBetween(cnToday(), event.endsOn) + 1
      return h(
        resolveComponent('UBadge'),
        { color: event.isActive ? 'success' : 'neutral', variant: 'subtle' },
        () =>
          event.isActive
            ? `Active, ${daysLeft} day${daysLeft === 1 ? '' : 's'} left`
            : 'Expired',
      )
    },
  },
  {
    accessorKey: 'accountsWithBonusSlot',
    header: 'Bonus Slots',
    cell: ({ row }) => {
      const event = row.original
      if (!event.isActive) return '-'
      return `${event.accountsWithBonusSlot} account${event.accountsWithBonusSlot === 1 ? '' : 's'}, ${event.activeBonusDeals} in deals`
    },
  },
  {
    accessorKey: 'createdBy',
    header: 'Added By',
  },
  {
    accessorKey: 'id',
    header: 'Actions',
    cell: ({ row }) => {
      const event = row.original
      if (pendingDeleteId.value !== event.id) {
        return h(resolveComponent('UButton'), {
          label: 'Delete',
          color: 'error',
          variant: 'soft',
          size: 'xs',
          onClick: () => (pendingDeleteId.value = event.id),
        })
      }

      return h('div', { class: 'flex items-center gap-2' }, [
        h(
          'span',
          { class: 'text-xs text-gray-600 dark:text-gray-400' },
          event.activeBonusDeals > 0
            ? `Cancels ${event.activeBonusDeals} deal${event.activeBonusDeals === 1 ? '' : 's'}.`
            : 'Delete this event?',
        ),
        h(resolveComponent('UButton'), {
          label: 'Confirm Delete',
          color: 'error',
          size: 'xs',
          onClick: () => deleteTeamEvent(event.id),
        }),
        h(resolveComponent('UButton'), {
          label: 'Keep',
          color: 'neutral',
          variant: 'soft',
          size: 'xs',
          onClick: () => (pendingDeleteId.value = null),
        }),
      ])
    },
  },
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
  return teamEventsList.value.filter((event) =>
    teamName(event.team).toLowerCase().includes(query),
  )
})

// Admin Access functions
const addAdminAccess = (): void => {
  adminAccessModal.open({
    editingAdmin: {} as AdminAccessSetting,
  })
}

const removeAdminAccess = async (
  accountId: number,
  adminRoleId: number,
): Promise<void> => {
  // TODO: Put this back once I'm comfortable with people being able to do this lol
  const success = await adminAccessStore.deleteAdminAccess({
    accountId,
    adminRoleId,
  })

  if (success) {
    toast.add({
      color: 'success',
      title: 'Success',
      description: 'Admin access removed successfully.',
      duration: 3000,
    })
  } else {
    toast.add({
      color: 'error',
      title: 'Error',
      description:
        'Failed to remove admin access. LWW still implementing this.',
      duration: 3000,
    })
  }
}

// Team Events functions
const addTeamEvent = (): void => {
  teamEventModal.open()
}

const deleteTeamEvent = async (eventId: number): Promise<void> => {
  pendingDeleteId.value = null
  try {
    await teamEventsStore.deleteTeamEvent(eventId)
  } catch {
    toast.add({
      color: 'error',
      title: 'Error',
      description: ERROR_MESSAGES.default,
      duration: 5000,
    })
    return
  }

  toast.add({
    color: 'success',
    title: 'Success',
    description: 'Team event deleted, and its bonus deals cancelled.',
    duration: 3000,
  })
}

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
      duration: 5000,
    })
  }

  try {
    await teamEventsStore.loadTeamEvents()
  } catch {
    toast.add({
      color: 'error',
      title: 'Error',
      description: 'Failed to load team events.',
      duration: 5000,
    })
  }
})
</script>
