<template>
  <div>
    <div v-if="slotsFree === 0" class="box py-6">
      <div class="content has-text-centered">
        <h5>You are not expected to interact with VEP today</h5>
        <p>
          Your nation does not have any free foreign aid slots, so you are not expected to send or receive any transactions today. Please check 
          again tomorrow for the updated status of your nation.
        </p>
      </div>
    </div>
    <b-collapse v-else class="card" animation="slide">
      <template #trigger>
        <div class="card-header">
          <p class="card-header-title">
            <span v-if="owesTech">Tech Targets</span>
            <span v-else-if="owesCash">Cash Targets</span>
            <span v-else-if="expectsCash">Incoming Cash</span>
            <span v-else-if="expectsTech">Incoming Tech</span>
            <span v-else>Potential Transactions</span>
            &nbsp;
            <span class="has-text-weight-normal">({{ aidList.length }} nations)</span>
          </p>
        </div>
      </template>
      <div class="card-content">
        <div class="content">
          <p>
            Please 
            <span v-if="owesTech">send tech to </span>
            <span v-else-if="owesCash">send cash to </span>
            <span v-else-if="expectsCash">accept cash from </span>
            <span v-else-if="expectsTech">accept tech from </span>
            <span v-else>Potential Transactions</span>
            any of the nations in the following list.
          </p>
          <p v-if="expectsCash || expectsTech">
            IMPORTANT: <strong>Do not use this list to send aid. </strong>
            It should only be used to verify that nations sending you aid are doing so legitimately.
          </p>
          <b-table :data="aidList">
            <b-table-column field="nation" label="Nation" v-slot="props">
              <a v-if="owesCash || owesTech" :href="getAidNationLink(props.row.nation)" target="_blank">
                {{ props.row.nation.rulerName }} of {{ props.row.nation.nationName }}
              </a>
              <span v-else>
                {{ props.row.nation.rulerName }} of {{ props.row.nation.nationName }}
              </span>
            </b-table-column>
            <b-table-column field="nation.allianceName" label="Alliance" v-slot="props">
              <div class="columns is-mobile">
                <div class="column is-narrow">
                  <ChipAllianceAcronym :allianceName="props.row.nation.allianceName"></ChipAllianceAcronym>
                </div>
                <div class="column">
                  {{ props.row.nation.allianceName }}
                </div>
              </div>
            </b-table-column>
            <b-table-column field="slotsFree" label="Slots Free" v-slot="props">
              {{ props.row.slotsFree }}
            </b-table-column>
            <b-table-column field="discord" label="Discord Tag" v-slot="props">
              {{ props.row.discord }}
            </b-table-column>
          </b-table>
        </div>
      </div>
    </b-collapse>
  </div>
</template>

<script>
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants';

export default {
  props: {
    account: {},
    aidList: []
  },
  computed: {
    slotsFree() {
      this.$log.debug(this.account);
      return this.account.slotsFull - this.account.slotsUsed;
    },
    owesCash() {
      const rolesThatSendCash = [ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.cashDonor, ACCOUNT_ROLE_CODES.temporaryDonor];
      return rolesThatSendCash.includes(this.account.role) && this.account.credit === 0;
    },
    expectsCash() {
      if (this.account.role === ACCOUNT_ROLE_CODES.seller || this.account.role === ACCOUNT_ROLE_CODES.probationarySeller) {
        return this.account.debt === 0;
      } else if ([ACCOUNT_ROLE_CODES.cashCollector, ACCOUNT_ROLE_CODES.temporaryCollector].includes(this.account.role)) {
        return this.account.credit === 0;
      } else {
        return false;
      }
    },
    owesTech() {
      const rolesThatSendTech = [
        ACCOUNT_ROLE_CODES.seller, ACCOUNT_ROLE_CODES.probationarySeller,
        ACCOUNT_ROLE_CODES.techFarm, ACCOUNT_ROLE_CODES.temporaryFarm
      ];

      return rolesThatSendTech.includes(this.account.role) && this.account.debt > 0;
    },
    expectsTech() {
      const rolesThatExpectTech = [
        ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.techReceiver, ACCOUNT_ROLE_CODES.temporaryReceiver
      ];

      return rolesThatExpectTech.includes(this.account.role) && this.account.credit > 0;
    }
  },
  methods: {
    getAidNationLink(nation) {
      return `https://www.cybernations.net/aid_form.asp?Nation_ID=${nation.nationId}&bynation=${this.account.nationId}`
    }
  },
  watch: {
    account() {
      this.$log.debug(this.expectsCash, this.expectsTech);
    }
  }
}
</script>