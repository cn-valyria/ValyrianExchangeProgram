<template>
  <nav class="level">
    <div class="level-item has-text-centered">
      <div>
        <p id="balance-subtitle" class="subtitle">Your Current Balance</p>
        <p id="balance-title" class="title" :class="categoryClass">{{ positivityIndicator }}{{ amount }} {{ balanceType }} ({{ balanceCategory }})</p>
        <p id="balance-subheading" class="heading">{{ description }}</p>
      </div>
    </div>
  </nav>
</template>

<style>
#balance-subtitle {
  margin-bottom: 1.5rem !important;
}

#balance-title {
  margin-top: 0 !important;
}

#balance-subheading {
  margin-top: 0.75rem;
}
</style>

<script>
import { ACCOUNT_ROLE_CODES } from "@/infrastructure/constants";

export default {
  props: {
    credit: Number,
    debt: Number,
    role: String
  },
  computed: {
    balance() {
      if (this.credit === undefined || this.debt === undefined) {
        return null;
      }

      // At least one of credit or debt will always be 0, so the balance amount is always the max of the two
      return Math.max(this.credit, this.debt);
    },
    balanceCategory() {
      if (this.balance === null) {
        return null;
      }

      if (this.balance === 0) {
        return "Even"; // If the max is 0, then the account has neither credit nor debt
      } else if (this.balance === this.credit) {
        return "Credit";
      } else {
        return "Debt"
      }
    },
    balanceType() {
      if (this.balanceCategory === null || this.role === undefined) {
        return null;
      }

      switch (this.role) {
        // Buyers always show credit as tech, but debt as cash
        case ACCOUNT_ROLE_CODES.buyer:
          return this.balanceCategory === "Credit" ? "Tech" : "Cash";

        // Sellers always show credit as cash, but debt as tech
        case ACCOUNT_ROLE_CODES.seller:
        case ACCOUNT_ROLE_CODES.probationarySeller:
          return this.balanceCategory === "Credit" ? "Cash" : "Tech";

        // Donors and Collectors are always exchanging tech
        case ACCOUNT_ROLE_CODES.cashDonor:
        case ACCOUNT_ROLE_CODES.temporaryDonor:
        case ACCOUNT_ROLE_CODES.cashCollector:
        case ACCOUNT_ROLE_CODES.temporaryCollector:
          return "Cash";

        // Farms and Receivers are always exchanging tech
        case ACCOUNT_ROLE_CODES.techFarm:
        case ACCOUNT_ROLE_CODES.temporaryFarm:
        case ACCOUNT_ROLE_CODES.techReceiver:
        case ACCOUNT_ROLE_CODES.temporaryReceiver:
          return "Tech";
      }
    },
    categoryClass() {
      switch (this.balanceCategory) {
        case "Credit": return "has-text-success";
        case "Debt": return "has-text-danger";
        case "Even": return "has-text-success";
        default: return "has-text-grey-light";
      }
    },
    positivityIndicator() {
      if (this.balance !== 1 && this.balanceCategory !== "Even") {
        return this.balanceCategory === "Credit" ? "+" : "-";
      } else {
        return "";
      }
    },
    amount() {
      if (this.balance === 1) {
        return "\u221E";
      } else if (this.balance === 0) {
        return "0";
      } else {
        return `${this.balanceType === "Cash" ? "$" : ""}${this.balance}${this.balanceType === "Cash" ? "M" : ""}`;
      }
    },
    description() {
      if (this.balanceCategory === "Credit" && this.balance !== 1) {
        return `Your nation is due ${this.amount} ${this.balanceType} from other VEP accounts.`;
      }
      if (this.balanceCategory === "Debt" && this.balance !== 1) {
        return `Your nation owes ${this.amount} ${this.balanceType} to its VEP account.`;
      }
      if (this.balanceCategory === "Credit" && this.balance === 1) {
        return `Your nation receives infinite ${this.balanceType} and will always show a Credit balance.`;
      }
      if (this.balanceCategory === "Debt" && this.balance === 1) {
        return `Your nation produces infinite ${this.balanceType} and will always show a Debt balance.`;
      }
      if (this.balanceCategory === "Even") {
        switch (this.role) {
          case ACCOUNT_ROLE_CODES.buyer:
            return "Your account currently has an even balance! This means that you need to send cash to other VEP accounts.";
          case ACCOUNT_ROLE_CODES.seller:
          case ACCOUNT_ROLE_CODES.probationarySeller:
            return "Your account currently has an even balance! This means that you are due cash from other VEP accounts.";
        }
      }

      return "";
    }
  }
}
</script>