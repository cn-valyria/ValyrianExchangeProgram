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
          <NuxtLink to="/" class="text-gray-300 hover:text-white px-3 py-2 rounded-md text-sm font-medium">
            Home
          </NuxtLink>
          <NuxtLink to="/how-it-works" class="text-gray-300 hover:text-white px-3 py-2 rounded-md text-sm font-medium">
            How It Works
          </NuxtLink>

          <!-- Account Dropdown (Authenticated) -->
          <UDropdownMenu v-if="isAuthenticated" :items="accountMenuItems" :popper="{ placement: 'bottom-end' }">
            <UButton color="neutral" variant="ghost" trailing-icon="i-heroicons-chevron-down-20-solid" class="text-gray-300 hover:text-gray-900">
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
            @click="mobileMenuOpen = !mobileMenuOpen"
            color="neutral"
            variant="ghost"
            :icon="mobileMenuOpen ? 'i-heroicons-x-mark-20-solid' : 'i-heroicons-bars-3-20-solid'"
          />
        </div>
      </div>

      <!-- Mobile Menu -->
      <div v-if="mobileMenuOpen" class="md:hidden py-4 space-y-2 border-t border-gray-800">
        <NuxtLink to="/" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium">
          Home
        </NuxtLink>
        <NuxtLink to="/how-it-works" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium">
          How It Works
        </NuxtLink>

        <template v-if="isAuthenticated">
          <NuxtLink :to="accountUrl" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium">
            My Account
          </NuxtLink>
          <template v-if="accountIsAdmin">
            <div class="px-3 py-2 text-xs font-semibold text-gray-500 uppercase">Admin Portal</div>
            <NuxtLink to="/admin" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium">
              Home
            </NuxtLink>
            <NuxtLink to="/admin/accounts" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium">
              Accounts
            </NuxtLink>
            <NuxtLink to="/admin/transactions" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium">
              Transactions
            </NuxtLink>
          </template>
          <NuxtLink v-if="accountIsAdmin || accountCanSeeLists" to="/admin/lists" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium">
            Lists
          </NuxtLink>
          <a @click="handleLogout()" class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium cursor-pointer">
            Log Out
          </a>
        </template>
        <template v-else>
          <NuxtLink to="/login">
            <UButton color="primary" block class="mt-2">
              Log In
            </UButton>
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
  return loggedInUser.value?.roles?.some((role: any) => role.name === "Admin") ?? false
})

const accountCanSeeLists = computed(() => {
  return loggedInUser.value?.roles?.some((role: any) => role.name === "Admin - Lists") ?? false
})

const accountMenuItems = computed(() => {
  const items: DropdownMenuItem[][] = [
    [
      {
        label: 'My Account',
        icon: 'i-heroicons-user-circle',
        to: accountUrl.value
      },
      {
        label: 'Leaderboard',
        icon: 'i-heroicons-trophy',
        to: '/leaderboard'
      }
    ]
  ]

  if (accountIsAdmin.value || accountCanSeeLists.value) {
    const adminItems = []

    if (accountIsAdmin.value) {
      adminItems.push({
        label: 'Admin Portal',
        slot: 'header',
        disabled: true
      })
      adminItems.push({
        label: 'Home',
        icon: 'i-heroicons-home',
        to: '/admin'
      })
      adminItems.push({
        label: 'Accounts',
        icon: 'i-heroicons-users',
        to: '/admin/accounts'
      })
      adminItems.push({
        label: 'Transactions',
        icon: 'i-heroicons-arrow-path',
        to: '/admin/transactions'
      })
    }

    if (accountIsAdmin.value || accountCanSeeLists.value) {
      adminItems.push({
        label: 'Lists',
        icon: 'i-heroicons-list-bullet',
        to: '/admin/lists'
      })
    }

    items.push(adminItems)
  }

  items.push([{
    label: 'Log Out',
    icon: 'i-heroicons-arrow-right-on-rectangle',
    onSelect() {
      handleLogout()
    }
  }])

  return items
})

const handleLogout = () => {
  authStore.logout()
  mobileMenuOpen.value = false
}
</script>
