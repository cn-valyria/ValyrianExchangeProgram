<template>
  <div>
    <div class="container">
      <div class="section">
        <h1 class="title">VEP Lists</h1>
        <h2 class="subtitle">View and manage the lists of nations that should be sending or receiving aid</h2>
      </div>
    </div>
    <div class="container">
      <b-loading :is-full-page="false" v-model="isLoading"></b-loading>
      <div class="section">
        <b-tabs>
          <b-tab-item v-for="list in lists" v-bind:key="list.id" :label="list.name" type="is-boxed">
            <div class="content">
              <h3>Message Template</h3>
              <b-field label="Carbon Copy Rulers">
                <b-input type="textarea" readonly :value="getRulerList(list)"></b-input>
              </b-field>
              <b-field label="Subject">
                <b-input readonly :value="list.subject.replace('{{date}}', todayFormatted)"></b-input>
              </b-field>
              <b-field label="Message">
                <b-input type="textarea" readonly :value="list.message"></b-input>
              </b-field>
              <h3>Data</h3>
              <b-table :data="list.recipients">
                <b-table-column field="nation" label="Nation" v-slot="props">
                  {{ props.row.nation.rulerName }} of {{ props.row.nation.nationName }}
                </b-table-column>
                <b-table-column field="slotsFree" label="Slots Free" v-slot="props">
                  {{ props.row.slotsFree }}
                </b-table-column>
                <b-table-column v-if="list.name.includes('Incoming')" field="credit" label="Credit" v-slot="props">
                  {{ props.row.credit }}
                </b-table-column>
                <b-table-column v-else field="debt" label="Debt" v-slot="props">
                  {{ props.row.debt }}
                </b-table-column>
                <b-table-column field="recentActivity" label="Activity" v-slot="props">
                  {{ recentActivityValues.find(v => v.id === props.row.recentActivity).description }}
                </b-table-column>
                <b-table-column field="discord" label="Discord Tag" v-slot="props">
                  {{ props.row.discord }}
                </b-table-column>
                <b-table-column field="id" label="Message" v-slot="props">
                  <a :href="getNationMessageLink(props.row.nation)" target="_blank">Click to Send</a>
                </b-table-column>
              </b-table>
            </div>
          </b-tab-item>
        </b-tabs>
      </div>
    </div>
  </div>
</template>

<script>
import { mapActions, mapState, mapGetters } from 'vuex';
import dateFormat from "dateformat";
import { recentActivityValues } from "~/infrastructure/dataLists";

export default {
  name: 'AdminListsPage',
  data: () => ({
    isLoading: true,
    recentActivityValues
  }),
  computed: {
    ...mapState({
      lists: state => state.admin.lists.lists
    }),
    ...mapGetters({
      loggedInUser: "loggedInUser"
    }),
    todayFormatted() {
      return dateFormat(new Date(), "mmm dd");
    }
  },
  async created() {
    if (!this.loggedInUser.roles.some(role => role.name === "Admin" || role.name === "Admin - Lists")) {
      this.$log.warn("Tried to access an admin page without the admin role.");
      this.$router.push("/");
      return;
    }

    await this.loadAllLists();
    this.$log.debug(this.lists);
    this.isLoading = false;
  },
  methods: {
    ...mapActions({
      loadAllLists: "admin/lists/getAllAidLists"
    }),
    getRulerList(aidList) {
      return aidList.recipients.map(list => list.nation.rulerName).join("\r\n");
    },
    getNationMessageLink(nation) {
      return `https://www.cybernations.net/send_message.asp?Nation_ID=${nation.nationId}`;
    }
  }
}
</script>