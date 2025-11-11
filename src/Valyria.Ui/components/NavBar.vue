<template>
  <nav class="navbar is-dark">
    <div class="navbar-brand">
      <a class="navbar-item">
        <img src="~/assets/img/logo.png" />
      </a>

      <a
        role="button"
        class="navbar-burger"
        :class="{ 'is-active': dropdownIsVisible }"
        @click="dropdownIsVisible = !dropdownIsVisible"
      >
        <span aria-hidden="true"></span>
        <span aria-hidden="true"></span>
        <span aria-hidden="true"></span>
      </a>
    </div>

    <div class="navbar-menu" :class="{ 'is-active': dropdownIsVisible }">
      <div class="navbar-start">
        <NuxtLink to="/" class="navbar-item">Home</NuxtLink>
        <a href="https://docs.google.com/document/d/1xsOzQjIb98aJwvHr4-AZqfUlCXLcVYAlXihZnvwBQoc/edit?tab=t.0" target="_blank" class="navbar-item">How It Works</a>
      </div>

      <div class="navbar-end">
        <div v-if="isAuthenticated" class="navbar-item has-dropdown is-hoverable">
          <a class="navbar-link">Account</a>
          <div class="navbar-dropdown is-right">
            <NuxtLink :to="accountUrl" class="navbar-item">My Account</NuxtLink>
            <div v-if="accountIsAdmin || accountCanSeeLists">
              <hr class="navbar-divider" />
            </div>
            <div v-if="accountIsAdmin">
              <hr class="navbar-divider" />
              <div class="navbar-item is-uppercase is-size-7">Admin Portal</div>
              <NuxtLink to="/admin" class="navbar-item">Home</NuxtLink>
              <NuxtLink to="/admin/accounts" class="navbar-item">Accounts</NuxtLink>
              <NuxtLink to="/admin/transactions" class="navbar-item">Transactions</NuxtLink>
            </div>
            <div v-if="accountIsAdmin || accountCanSeeLists">
              <NuxtLink to="/admin/lists" class="navbar-item">Lists</NuxtLink>
            </div>
            <hr class="navbar-divider" />
            <a class="navbar-item" @click="handleLogout()">Log Out</a>
          </div>
        </div>

        <a v-else class="navbar-item">
          <div class="buttons">
            <Button label="Log In" @click="loginFormIsVisible = true" />
          </div>
        </a>
      </div>
    </div>

    <Login :show="loginFormIsVisible" @close="loginFormIsVisible = false" />
  </nav>
</template>

<style>
/* I like the divider, even on mobile, so force it to always show */
.navbar-divider {
  display: block !important;
}
</style>

<script setup lang="ts">
import { useAuthStore } from '~/stores/auth'

const dropdownIsVisible = ref(false)
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

const handleLogout = async () => {
  await authStore.logout()
}
</script>
