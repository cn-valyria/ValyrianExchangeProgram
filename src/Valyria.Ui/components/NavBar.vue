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
            <a class="navbar-item" @click="logout()">Log Out</a>
          </div>
        </div>

        <a v-else class="navbar-item">
          <div class="buttons">
            <b-button type="is-primary" label="Log In" @click="loginFormIsVisible = true" />
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

<script>
import { mapGetters } from 'vuex';

export default {
  data: () => ({
    dropdownIsVisible: false,
    loginFormIsVisible: false
  }),
  computed: {
    ...mapGetters({
      isAuthenticated: "isAuthenticated",
      loggedInUser: "loggedInUser"
    }),
    accountUrl() {
      return `/account/${this.loggedInUser.accountId}`;
    },
    accountIsAdmin() {
      return this.loggedInUser.roles.some(role => role.name === "Admin");
    },
    accountCanSeeLists() {
      return this.loggedInUser.roles.some(role => role.name === "Admin - Lists");
    }
  },
  methods: {
    async logout() {
      await this.$auth.logout();
    }
  }
}
</script>