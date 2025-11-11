import { RELOAD_LISTS } from "./mutation-types";

export const state = () => ({
  lists: []
});

export const mutations = {
  [RELOAD_LISTS](state, data) {
    state.lists = data;
  }
};

export const actions = {
  async getAllAidLists(context) {
    const response = await this.$axios.get("lists");
    this.$log.debug(response);
    context.commit(RELOAD_LISTS, response.data);
  }
};