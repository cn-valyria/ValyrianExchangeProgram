<template>
  <div>
    <div class="container">
      <div class="section">
        <h1 class="title">VEP Transactions</h1>
        <h2 class="subtitle">Manage every transaction that VEP tracks, as well as manual balance adjustments</h2>
      </div>
    </div>
    <div class="container">
      <div class="section">
        <b-field grouped>
          <b-field label="Sent By" expanded>
            <b-input v-model="filters.sentBy"></b-input>
          </b-field>
          <b-field label="Received By" expanded>
            <b-input v-model="filters.receivedBy"></b-input>
          </b-field>
          <b-field label="Since" grouped expanded>
            <b-datepicker v-model="filters.sentSince" :mobile-native="false">
              <template v-slot:trigger>
                <b-button icon-left="calendar-today" type="is-link" />
              </template>
              <template v-slot:default>
                <b-button type="is-white" @click="filters.sentSince = null">Clear</b-button>
              </template>
            </b-datepicker>
            <b-input expanded readonly :value="filterSinceString"></b-input>
          </b-field>
          <b-field label="Until" grouped expanded>
            <b-datepicker v-model="filters.sentUntil" :mobile-native="false">
              <template v-slot:trigger>
                <b-button icon-left="calendar-today" type="is-link" />
              </template>
              <template v-slot:default>
                <b-button type="is-white" @click="filters.sentUntil = null">Clear</b-button>
              </template>
            </b-datepicker>
            <b-input expanded readonly :value="filterUntilString"></b-input>
          </b-field>
          <p class="control">
            <b-button label="Filter" type="is-primary" style="margin-top: 32px" @click="changePage(1)" />
          </p>
        </b-field>
        <div class="tabs is-boxed">
          <ul>
            <li :class="{ 'is-active': currentTab === TRANSACTION_TYPES.aidBased }">
              <a @click="changeTab(TRANSACTION_TYPES.aidBased)">
                <span>Aid-Based</span>
              </a>
            </li>
            <li :class="{ 'is-active': currentTab === TRANSACTION_TYPES.manual }">
              <a @click="changeTab(TRANSACTION_TYPES.manual)">
                <span>Manual</span>
              </a>
            </li>
          </ul>
        </div>
        <div v-if="currentTab === TRANSACTION_TYPES.aidBased">
          <b-table
            :data="aidBasedTransactions"
            :loading="tableIsLoading"

            paginated 
            backend-pagination 
            :total="currentTransactionsPage.totalCount"
            :per-page="limit"
            @page-change="changePage"
          >
            <b-table-column field="id" label="Actions" width="140px" v-slot="props">
              <div class="buttons are-small">
                <button class="button is-info" @click="editTransaction(props.row, TRANSACTION_TYPES.aidBased)">View</button>
                <button
                  class="button is-danger"
                  :class="{ 'is-loading': transactionIdBeingDeleted === props.row.id }"
                  @click="deleteTransactionById(props.row.id)"
                >
                  Delete
                </button>
              </div>
            </b-table-column>
            <b-table-column field="sentBy" label="Sent By" v-slot="props">
              {{ props.row.sentBy.rulerName }} of {{ props.row.sentBy.nationName }}
            </b-table-column>
            <b-table-column field="receivedBy" label="Received By" v-slot="props">
              {{ props.row.receivedBy.rulerName }} of {{ props.row.receivedBy.nationName }}
            </b-table-column>
            <b-table-column field="code" label="Role Pairing" v-slot="props">
              {{ props.row.code.sendingRole }}{{ props.row.code.receivingRole}}
            </b-table-column>
            <b-table-column field="status" label="Status" v-slot="props">
              {{ toAidStatusDescription(props.row.status) }}
            </b-table-column>
            <b-table-column field="money" label="Money" v-slot="props">
              {{ props.row.money }}
            </b-table-column>
            <b-table-column field="technology" label="Tech" v-slot="props">
              {{ props.row.technology }}
            </b-table-column>
            <b-table-column field="reason" label="Reason" v-slot="props">
              {{ props.row.reason }}
            </b-table-column>
            <b-table-column field="startsOn" label="Sent On" v-slot="props">
              {{ props.row.startsOn.split('T')[0] }}
            </b-table-column>
            <template #empty>
              <div class="has-text-centered">No transactions currently loaded</div>
            </template>
          </b-table>
        </div>
        <div v-if="currentTab === TRANSACTION_TYPES.manual">
          <button class="button is-success" @click="createTransaction()">Create</button>
          <b-table
            :data="manualTransactions"
            :loading="tableIsLoading"

            paginated 
            backend-pagination 
            :total="currentTransactionsPage.totalCount"
            :per-page="limit"
            @page-change="changePage"
          >
            <b-table-column field="id" label="Actions" width="140px" v-slot="props">
              <div class="buttons are-small">
                <button class="button is-info" @click="editTransaction(props.row, TRANSACTION_TYPES.manual)">Edit</button>
                <button
                  class="button is-danger"
                  :class="{ 'is-loading': transactionIdBeingDeleted === props.row.id }"
                  @click="deleteTransactionById(props.row.id)"
                >
                  Delete
                </button>
              </div>
            </b-table-column>
            <b-table-column field="nation" label="Account" v-slot="props">
              {{ props.row.nation.rulerName }} of {{ props.row.nation.nationName }}
            </b-table-column>
            <b-table-column field="adjustmentType" label="Adjustment Type" v-slot="props">
              {{ props.row.adjustmentType }}
            </b-table-column>
            <b-table-column field="reason" label="Reason" v-slot="props">
              {{ props.row.reason }}
            </b-table-column>
            <b-table-column field="accountCode" label="Role" v-slot="props">
              {{ props.row.accountCode }}
            </b-table-column>
            <b-table-column field="classification" label="Classification" v-slot="props">
              {{ classifications.find(c => c.id === props.row.classification).description }}
            </b-table-column>
            <b-table-column field="rate" label="Rate" v-slot="props">
              {{ props.row.rate }}
            </b-table-column>
            <template #empty>
              <div class="has-text-centered">No transactions currently loaded</div>
            </template>
          </b-table>
        </div>
      </div>
    </div>
    <AdminAidBasedTransactionDialog
      :show="aidBasedDialogIsVisible" 
      :transaction="transactionBeingEdited" 
      @close="closeDialog(TRANSACTION_TYPES.aidBased)"
    />
    <AdminManualTransactionDialog 
      :show="manualDialogIsVisible" 
      :transaction="transactionBeingEdited" 
      :accounts="accounts" 
      @close="closeDialog(TRANSACTION_TYPES.manual)"
    />
  </div>
</template>

<script>
import { mapState, mapGetters, mapActions } from "vuex";
import dateFormat from "dateformat";
import { TRANSACTION_TYPES } from "~/infrastructure/constants";
import { transactionClassifications } from '~/infrastructure/dataLists';

export default {
  name: "AdminTransactionsPage",
  data: () => ({
    TRANSACTION_TYPES,
    currentTab: TRANSACTION_TYPES.aidBased,
    currentPage: 1,
    limit: 25,
    limitOptions: [ 10, 25, 100, 500 ],
    tableIsLoading: false,
    aidBasedDialogIsVisible: false,
    manualDialogIsVisible: false,
    transactionBeingEdited: undefined,
    transactionIdBeingDeleted: 0,
    classifications: transactionClassifications,
    filters: {
      sentBy: null,
      receivedBy: null,
      sentSince: null,
      sentUntil: null
    }
  }),
  computed: {
    ...mapState({
      currentTransactionsPage: state => state.admin.transactions.currentTransactionsPage,
      accounts: state => state.admin.accounts.accounts
    }),
    ...mapGetters({
      aidBasedTransactions: "admin/transactions/aidBasedTransactions",
      manualTransactions: "admin/transactions/manualTransactions",
      loggedInUser: "loggedInUser"
    }),
    totalPages() {
      return Math.ceil(this.currentTransactionsPage.totalCount / 100);
    },
    tabIsAidBased() {
      return this.currentTab === TRANSACTION_TYPES.aidBased;
    },
    tabIsManual() {
      return this.currentTab === TRANSACTION_TYPES.manual;
    },
    filterSinceString() {
      return this.filters.sentSince ? dateFormat(this.filters.sentSince, "mm/dd/yyyy") : "";
    },
    filterUntilString() {
      return this.filters.sentUntil ? dateFormat(this.filters.sentUntil, "mm/dd/yyyy") : "";
    }
  },
  async created() {
    if (!this.loggedInUser.roles.some(role => role.name === "Admin")) {
      this.$log.warn("Tried to access an admin page without the admin role.");
      this.$router.push("/");
      return;
    }

    await this.changePage(1);
    this.$log.info(this.currentTransactionsPage);

    await this.loadAllAccounts();
    this.$log.info(this.accounts);
  },
  methods: {
    ...mapActions({
      reloadCurrentTransactionsPage: "admin/transactions/reloadCurrentTransactionsPage",
      deleteTransaction: "admin/transactions/deleteTransaction",
      loadAllAccounts: "admin/accounts/loadAllAccounts"
    }),
    async changePage(newPageNumber) {
      this.tableIsLoading = true;
      this.currentPage = newPageNumber;
      try {
        await this.reloadCurrentTransactionsPage({
          type: this.currentTab,
          filter: {
            sentBy: this.filters.sentBy,
            receivedBy: this.filters.receivedBy,
            sentSince: this.filters.sentSince,
            sentUntil: this.filters.sentUntil
          },
          limit: this.limit,
          offset: this.limit * (this.currentPage - 1)
        });
      } catch (e) {
        this.$log.error(e);
        this.$toast.error("The current page failed to load");
      }

      this.tableIsLoading = false;
    },
    async changeTab(tabName) {
      if (this.currentTab === tabName) {
        this.$log.warn("The same tab that is already active was clicked, so nothing will happen.");
        return;
      }

      this.currentTab = tabName;
      await this.changePage(1);
    },
    toAidStatusDescription(statusId) {
      switch (statusId) {
        case 1: return "Pending";
        case 2: return "Approved";
        case 3: return "Cancelled";
        case 4: return "Expired";
        default: return "Unknown";
      }
    },
    createTransaction() {
      this.transactionBeingEdited = {};
      this.manualDialogIsVisible = true;
    },
    editTransaction(transaction, transactionType) {
      this.transactionBeingEdited = transaction;

      if (transactionType === TRANSACTION_TYPES.aidBased) {
        this.aidBasedDialogIsVisible = true;
      } else if (transactionType === TRANSACTION_TYPES.manual) {
        this.manualDialogIsVisible = true;
      }
    },
    async deleteTransactionById(id) {
      this.transactionIdBeingDeleted = id;

      try {
        await this.deleteTransaction(id);
      } catch (e) {
        this.$log.error(e);
        this.$toast.error("VEP encountered an error while deleting the transaction. Please try again, or contact @lilweirdward if the error persists.");
      }

      this.transactionIdBeingDeleted = 0;
      await this.changePage(this.currentPage);
    },
    closeDialog(transactionType) {
      if (transactionType === TRANSACTION_TYPES.aidBased) {
        this.aidBasedDialogIsVisible = false;
      } else if (transactionType === TRANSACTION_TYPES.manual) {
        this.manualDialogIsVisible = false;
      }
      
      this.transactionBeingEdited = undefined;
      this.changePage(this.currentPage);
    }
  }
}
</script>