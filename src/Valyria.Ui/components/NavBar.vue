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
          <a
            href="https://docs.google.com/document/d/1xsOzQjIb98aJwvHr4-AZqfUlCXLcVYAlXihZnvwBQoc/edit?tab=t.0"
            target="_blank"
            class="text-gray-300 hover:text-white px-3 py-2 rounded-md text-sm font-medium"
          >
            How It Works
          </a>

          <!-- Account Dropdown (Authenticated) -->
          <UDropdown v-if="isAuthenticated" :items="accountMenuItems" :popper="{ placement: 'bottom-end' }">
            <UButton color="white" variant="ghost" trailing-icon="i-heroicons-chevron-down-20-solid">
              Account
            </UButton>
          </UDropdown>

          <!-- Login Button (Not Authenticated) -->
          <UButton v-else @click="loginFormIsVisible = true" color="primary">
            Log In
          </UButton>
        </div>

        <!-- Mobile Menu Button -->
        <div class="md:hidden">
          <UButton
            @click="mobileMenuOpen = !mobileMenuOpen"
            color="white"
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
        <a
          href="https://docs.google.com/document/d/1xsOzQjIb98aJwvHr4-AZqfUlCXLcVYAlXihZnvwBQoc/edit?tab=t.0"
          target="_blank"
          class="block text-gray-300 hover:text-white px-3 py-2 rounded-md text-base font-medium"
        >
          How It Works
        </a>

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
          <UButton @click="loginFormIsVisible = true" color="primary" block class="mt-2">
            Log In
          </UButton>
        </template>
      </div>
    </div>

    <Login :show="loginFormIsVisible" @close="loginFormIsVisible = false" />
  </nav>
</template>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'

const mobileMenuOpen = ref(false)
const loginFormIsVisible = ref(false)

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
  const items = [
    [{
      label: 'My Account',
      icon: 'i-heroicons-user-circle',
      to: accountUrl.value
    }]
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
    click: handleLogout
  }])

  return items
})

const handleLogout = async () => {
  await authStore.logout()
  mobileMenuOpen.value = false
}
</script>
