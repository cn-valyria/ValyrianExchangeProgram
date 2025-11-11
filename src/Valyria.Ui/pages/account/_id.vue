<template>
  <div style="position: relative">
    <b-loading v-model="loading" :is-full-page="false" :can-cancel="false"></b-loading>
    <section class="hero is-light is-medium">
      <div class="hero-body">
        <p class="title">
          <span class="has-text-weight-bold">{{ account.rulerName }}</span> of {{ account.nationName }}
        </p>
        <p class="subtitle">
          <b-field grouped group-multiline>
            <div class="control">
              <ChipAccountRole :code="account.role"></ChipAccountRole>
            </div>
            <div class="control">
              <ChipAllianceAcronym :allianceName="account.allianceName"></ChipAllianceAcronym>
            </div>
          </b-field>
        </p>
      </div>
    </section>
    <div class="container">
      <div class="section">
        <AccountBalance :credit="account.credit" :debt="account.debt" :role="account.role"></AccountBalance>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <AccountAidList :account="account" :aidList="aidList"></AccountAidList>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <AccountCreditHistory :accountRole="account.role" :creditHistory="creditHistory"></AccountCreditHistory>
      </div>
    </div>
  </div>
</template>

<style>
.loading-overlay .loading-background {
  background: hsla(0, 0%, 100%, 1) !important;
}
</style>

<script>
import { mapActions, mapGetters, mapState } from 'vuex';
import { accountRoles } from '~/infrastructure/dataLists';

export default {
  name: "AccountPage",
  data: () => ({
    loading: false,
    roles: accountRoles
  }),
  computed: {
    ...mapState({
      account: state => state.account.account,
      aidList: state => state.account.aidList
    }),
    ...mapGetters({
      creditHistory: "account/creditHistory",
      loggedInUser: "loggedInUser"
    }),
    accountId() {
      const parsedId = parseInt(this.$route.params.id);
      return isNaN(parsedId) ? 0 : parsedId;
    }
  },
  async created() {
    this.loading = true;

    if (this.loggedInUser.accountId !== this.accountId) {
      this.$toast.error("You're not logged into the account that you tried to access.");
      this.$router.push("/");
      return;
    }

    await this.loadAccount(this.accountId);
    await this.loadAidList(this.accountId);
    await this.loadTransactionHistory(this.accountId);

    this.loading = false;
  },
  methods: {
    ...mapActions({
      loadAccount: "account/loadAccount",
      loadAidList: "account/loadAidList",
      loadTransactionHistory: "account/loadTransactionHistory"
    })
  }
}
</script>