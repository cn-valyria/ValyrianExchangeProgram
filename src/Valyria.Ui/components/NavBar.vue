<template>
  <nav class="bg-gray-900 border-b border-gray-800">
    <div class="container mx-auto px-4">
      <div class="flex items-center justify-between h-16">
        <!-- Logo -->
        <div class="flex items-center">
          <NuxtLink to="/" class="flex items-center">
            <img src="~/assets/img/logo.png" class="h-10" alt="VEP Logo" />
          </NuxtLink>
        </div>

        <!-- Desktop Navigation -->
        <div class="hidden md:flex items-center space-x-4">
          <NuxtLink
            to="/"
            class="text-gray-300 hover:text-white px-3 py-2 rounded-md text-sm font-medium"
          >
            Home
          </NuxtLink>
          <NuxtLink
            to="/how-it-works"
            class="text-gray-300 hover:text-white px-3 py-2 rounded-md text-sm font-medium"
          >
            How It Works
          </NuxtLink>

          <!-- Account Dropdown (Authenticated) -->
          <UDropdownMenu
            v-if="isAuthenticated"
            :items="accountMenuItems"
            :popper="{ placement: 'bottom-end' }"
          >
            <UButton
              color="neutral"
              variant="ghost"
              trailing-icon="i-heroicons-chevron-down-20-solid"
              class="text-gray-300 hover:text-gray-900"
            >
              Account
            </UButton>
          </UDropdownMenu>

          <!-- Login Button (Not Authenticated) -->
          <NuxtLink v-if="!isAuthenticated" to="/login">
            <UButton color="primary">Log In</UButton>
          </NuxtLink>
        </div>

        <!-- Mobile Menu Button -->
        <div class="md:hidden">
          <UButton
            color="neutral"
            variant="solid"
            :icon="
              mobileMenuOpen
                ? 'i-heroicons-x-mark-20-solid'
                : 'i-heroicons-bars-3-20-solid'
            "
            @click="mobileMenuOpen = !mobileMenuOpen"
          />
        </div>
      </div>

      <!-- Mobile Menu -->
      <div
        v-if="mobileMenuOpen"
        class="md:hidden py-4 space-y-2 border-t border-gray-800"
      >
        <NuxtLink
          to="/"
          class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
        >
          Home
        </NuxtLink>
        <NuxtLink
          to="/how-it-works"
          class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
        >
          How It Works
        </NuxtLink>

        <template v-if="isAuthenticated">
          <NuxtLink
            :to="accountUrl"
            class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
          >
            My Account
          </NuxtLink>
          <template v-if="accountIsAdmin || accountIsReadOnlyAdmin">
            <div
              class="px-3 py-2 text-xs font-semibold text-gray-500 uppercase"
            >
              Admin Portal
            </div>
            <NuxtLink
              to="/admin"
              class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
            >
              Home
            </NuxtLink>
            <NuxtLink
              to="/admin/accounts"
              class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
            >
              Accounts
            </NuxtLink>
            <NuxtLink
              to="/admin/aid-activity"
              class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
            >
              Aid Activity
            </NuxtLink>
            <NuxtLink
              to="/admin/deals"
              class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
            >
              Deals
            </NuxtLink>
            <NuxtLink
              to="/admin/flags"
              class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
            >
              Flagged Deals
            </NuxtLink>
          </template>
          <NuxtLink
            v-if="
              accountIsAdmin || accountCanSendMessages || accountIsReadOnlyAdmin
            "
            to="/admin/messages"
            class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
          >
            Messages
          </NuxtLink>
          <NuxtLink
            v-if="accountIsAdmin"
            to="/admin/settings"
            class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
          >
            Settings
          </NuxtLink>
          <a
            class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium cursor-pointer"
            @click="handleLogout()"
          >
            Log Out
          </a>
        </template>
        <template v-else>
          <NuxtLink to="/login">
            <UButton color="primary" block class="mt-2"> Log In </UButton>
          </NuxtLink>
        </template>
      </div>
    </div>
  </nav>
</template>

<script setup lang="ts">
import type { DropdownMenuItem } from '@nuxt/ui'
import { useAuthStore } from '~/stores/auth'

const mobileMenuOpen = ref(false)

const authStore = useAuthStore()

const isAuthenticated = computed(() => authStore.isAuthenticated)
const loggedInUser = computed(() => authStore.loggedInUser)

const accountUrl = computed(() => {
  return loggedInUser.value ? `/account/${loggedInUser.value.accountId}` : ''
})

const accountIsAdmin = computed(() => {
  return (
    loggedInUser.value?.roles?.some((role) => role.name === 'Super Admin') ??
    false
  )
})

// The "Admin - Lists" role is named for VEP 2.5's lists, which the Messages page replaced
const accountCanSendMessages = computed(() => {
  return (
    loggedInUser.value?.roles?.some((role) => role.name === 'Admin - Lists') ??
    false
  )
})

const accountIsReadOnlyAdmin = computed(() => {
  return (
    loggedInUser.value?.roles?.some(
      (role) => role.name === 'Admin (Read-only)',
    ) ?? false
  )
})

const accountMenuItems = computed(() => {
  const items: DropdownMenuItem[][] = [
    [
      {
        label: 'My Account',
        icon: 'i-heroicons-user-circle',
        to: accountUrl.value,
      },
    ],
  ]

  if (
    accountIsAdmin.value ||
    accountCanSendMessages.value ||
    accountIsReadOnlyAdmin.value
  ) {
    const adminItems = []

    if (accountIsAdmin.value || accountIsReadOnlyAdmin.value) {
      adminItems.push({
        label: 'Admin Portal',
        slot: 'header',
        disabled: true,
      })
      adminItems.push({
        label: 'Home',
        icon: 'i-heroicons-home',
        to: '/admin',
      })
      adminItems.push({
        label: 'Accounts',
        icon: 'i-heroicons-users',
        to: '/admin/accounts',
      })
      adminItems.push({
        label: 'Aid Activity',
        icon: 'i-heroicons-arrows-right-left',
        to: '/admin/aid-activity',
      })
      adminItems.push({
        label: 'Deals',
        icon: 'i-heroicons-link',
        to: '/admin/deals',
      })
      adminItems.push({
        label: 'Flagged Deals',
        icon: 'i-heroicons-flag',
        to: '/admin/flags',
      })
    }

    adminItems.push({
      label: 'Messages',
      icon: 'i-heroicons-envelope',
      to: '/admin/messages',
    })

    if (accountIsAdmin.value) {
      adminItems.push({
        label: 'Settings',
        icon: 'i-heroicons-cog-6-tooth',
        to: '/admin/settings',
      })
    }

    items.push(adminItems)
  }

  items.push([
    {
      label: 'Log Out',
      icon: 'i-heroicons-arrow-right-on-rectangle',
      onSelect() {
        handleLogout()
      },
    },
  ])

  return items
})

const handleLogout = () => {
  authStore.logout()
  mobileMenuOpen.value = false
}
</script>
