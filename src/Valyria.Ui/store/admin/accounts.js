import {
  CLEAR_ACCOUNTS,
  ADD_ACCOUNTS,
  UPDATE_ACCOUNT,
  REMOVE_ACCOUNT
} from "./mutation-types";

export const state = () => ({
  accounts: []
});

export const mutations = {
  [CLEAR_ACCOUNTS](state) {
    state.accounts = [];
  },
  [ADD_ACCOUNTS](state, payload) {
    state.accounts.push(...payload);
  },
  [UPDATE_ACCOUNT](state, payload) {
    // let allAccounts = [...state.accounts];
    const i = state.accounts.findIndex(a => a.id === payload.id);
    if (i === -1) {
      this.$log.warn(`No account was found by ID ${payload.id}, so nothing will be updated`);
      return;
    }

    const updatedAccount = {
      ...state.accounts[i],
      role: payload.role,
      uniqueCode: payload.uniqueCode,
      discord: payload.discord,
      discordUniqueId: payload.discordUniqueId,
      hasForeignMinistry: payload.hasForeignMinistry,
      hasFederalAidCommission: payload.hasFederalAidCommission,
      hasDisasterReliefAgency: payload.hasDisasterReliefAgency
    };
    state.accounts[i] = updatedAccount;
  },
  [REMOVE_ACCOUNT](state, payload) {
    const i = state.accounts.findIndex(a => a.id === payload);
    if (i === -1) {
      this.$log.warn(`No account was found by ID ${payload}, so nothing will be removed.`);
      return;
    }

    state.accounts.splice(i, 1);
  }
};

export const actions = {
  async loadAllAccounts(context) {
    const response = await this.$axios.get("accounts");
    this.$log.debug(response);
    context.commit(CLEAR_ACCOUNTS);
    context.commit(ADD_ACCOUNTS, response.data);
  },
  async createAccount(context, payload) {
    const response = await this.$axios.post("accounts", payload);
    this.$log.debug(response);
    context.commit(ADD_ACCOUNTS, [response.data]);
  },
  async updateAccount(context, payload) {
    const response = await this.$axios.put("accounts", payload);
    this.$log.debug(response);
    context.commit(UPDATE_ACCOUNT, payload);
  },
  async deleteAccount(context, payload) {
    const response = await this.$axios.delete(`accounts/${payload}`);
    this.$log.debug(response);
    context.commit(REMOVE_ACCOUNT, payload);
  }
};