<template>
  <div>
    <div class="container">
      <div class="section">
        <h1 class="title">VEP Dashboard</h1>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <nav class="level">
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Total Accounts</p>
              <p class="title">{{ dashboard.totalAccounts }}</p>
            </div>
          </div>
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Buyer to Seller Ratio</p>
              <p class="title">{{ parseFloat(dashboard.totalBuyers / dashboard.totalSellers).toFixed(2) }}</p>
            </div>
          </div>
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Total Aid Transactions</p>
              <p class="title">{{ dashboard.totalTransactions }}</p>
            </div>
          </div>
          <div class="level-item has-text-centered">
            <div>
              <p class="heading">Total Unused Credit</p>
              <p class="title">{{ dashboard.unusedCredit }}</p>
            </div>
          </div>
        </nav>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <div class="tile is-ancestor">
          <div class="tile is-parent">
            <article class="tile is-child box">
              <admin-dashboard-total-transactions-tile :data="dashboard.transactionsPerDay" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <admin-dashboard-pending-credit-tile :data="dashboard.pendingCreditByAlliance" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <admin-dashboard-accounts-by-role :data="dashboard.accountsPerRole" />
            </article>
          </div>
        </div>
        <div class="tile is-ancestor">
          <div class="tile is-parent">
            <article class="tile is-child box">
              <admin-dashboard-total-slots-by-role :data="dashboard.totalSlotsPerRole" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <admin-dashboard-free-slots-by-list :data="dashboard.freeSlotsPerList" />
            </article>
          </div>
          <div class="tile is-parent">
            <article class="tile is-child box">
              <admin-dashboard-accounts-by-alliance :data="dashboard.accountsPerAlliance" />
            </article>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { mapState, mapActions, mapGetters } from 'vuex';

export default {
  name: 'AdminIndex',
  computed: {
    ...mapState({
      dashboard: state => state.admin.dashboard
    }),
    ...mapGetters({
      loggedInUser: "loggedInUser"
    }),
  },
  async created() {
    if (!this.loggedInUser.roles.some(role => role.name === "Admin")) {
      this.$log.warn("Tried to access an admin page without the admin role.");
      this.$router.push("/");
      return;
    }

    await this.loadDashboard();
  },
  methods: {
    ...mapActions({
      loadDashboard: "admin/loadDashboard"
    })
  }
}
</script>