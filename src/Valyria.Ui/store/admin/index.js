import { LOAD_DASHBOARD } from "./mutation-types";

export const state = () => ({
  dashboard: Object
});

export const mutations = {
  [LOAD_DASHBOARD](state, payload) {
    state.dashboard = payload;
  }
};

export const actions = {
  async loadDashboard(context) {
    const response = await this.$axios.get("dashboard");
    this.$log.debug(response);
    context.commit(LOAD_DASHBOARD, response.data);
  }
};