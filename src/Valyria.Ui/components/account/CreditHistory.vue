<template>
  <b-collapse class="card" animation="slide">
    <template #trigger>
      <div class="card-header">
        <p class="card-header-title">
          Credit History&nbsp;
          <span class="has-text-weight-normal">
            +{{ creditHistory.filter(txn => txn.balanceChangeType === "Credit").length }} out, 
            -{{ creditHistory.filter(txn => txn.balanceChangeType === "Debt").length }} in
          </span>
        </p>
      </div>
    </template>
    <div class="card-content">
      <div class="content">
        <p>
          Your entire history of credit changes, as recorded in the VEP. This history includes both aid-based transactions, as well as manually-
          configured credits or debts that may have been added to your account by an admin. Please contact VEP Management if you have any 
          concerns about your credit history.
        </p>
        <b-table :data="creditHistory">
          <b-table-column field="otherNation" column="Other Nation" v-slot="props">
            {{ props.row.otherNation !== null ? `${props.row.otherNation.rulerName} of ${props.row.otherNation.nationName}` : "VEP Management" }}
          </b-table-column>
          <b-table-column field="type" label="Type" v-slot="props">
            {{ props.row.type }}
          </b-table-column>
          <b-table-column field="status" label="Status" v-slot="props">
            {{ props.row.otherNation !== null ? toAidStatusDescription(props.row.status) : "Active" }}
          </b-table-column>
          <b-table-column field="money" label="Money" v-slot="props">
            {{ props.row.money !== null ? props.row.money : "N/A" }}
          </b-table-column>
          <b-table-column field="technology" label="Tech" v-slot="props">
            {{ props.row.technology !== null ? props.row.technology : "N/A" }}
          </b-table-column>
          <b-table-column field="balanceChangeType" label="Credit Change" v-slot="props">
            <span :class="{
              'has-text-success': props.row.balanceChangeType === 'Credit',
              'has-text-error': props.row.balanceChangeType === 'Debt'
            }">
              {{ toCreditChange(props.row) }}
            </span>
          </b-table-column>
          <b-table-column field="sentOn" label="Sent On" v-slot="props">
            {{ props.row.sentOn !== null ? props.row.sentOn.split('T')[0] : "N/A" }}
          </b-table-column>
        </b-table>
      </div>
    </div>
  </b-collapse>
</template>

<script>
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants';

export default {
  props: {
    accountRole: String,
    creditHistory: []
  },
  methods: {
    toAidStatusDescription(statusId) {
      switch (statusId) {
        case 1: return "Pending";
        case 2: return "Approved";
        case 3: return "Cancelled";
        case 4: return "Expired";
        default: return "Unknown";
      }
    },
    toCreditChange(txn) {
      const positivityIndicator = txn.balanceChangeType === "Credit" ? "+" : "-";
      let cashOrTech, receivedOrSent;
      if (txn.money !== null) {
        cashOrTech = txn.money > 0 ? "$9m" : "100 tech";
        receivedOrSent = txn.type === "Incoming" ? "received" : "sent";
      } else {
        const cash = `$${txn.rate}m`;
        const tech = `${txn.rate / 9 * 100} tech`;
        switch (this.accountRole) {
          case ACCOUNT_ROLE_CODES.buyer:
            cashOrTech = txn.balanceChangeType === "Credit" ? tech : cash;
            break;
          case ACCOUNT_ROLE_CODES.seller:
          case ACCOUNT_ROLE_CODES.probationarySeller:
            cashOrTech = txn.balanceChangeType === "Credit" ? cash : tech;
            break;
          default:
            // If we can't figure out why they were given a manual txn, then just call it units
            return `${txn.rate} units`;
        }

        receivedOrSent = txn.balanceChangeType === "Credit" ? "added" : "deducted";
      }

      return `${positivityIndicator}${cashOrTech} ${receivedOrSent}`;
    }
  }
}
</script>