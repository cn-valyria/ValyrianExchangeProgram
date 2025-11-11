<template>
  <b-taglist attached>
    <b-tag :type="codeColor">{{ code }}</b-tag>
    <b-tag type="is-dark">{{ code !== undefined ? roles.find(r => r.code === code).description : "" }}</b-tag>
  </b-taglist>
</template>

<script>
import { accountRoles } from '~/infrastructure/dataLists';

export default {
  props: {
    code: String
  },
  data: () => ({
    roles: accountRoles
  }),
  computed: {
    codeColor() {
      switch (this.code) {
        case "B": // Buyer
        case "D": // Donor
        case "W": // Temp Receiver
          return "is-warning";
        case "S": // Seller
        case "F": // Farm
        case "N": // Probationary Seller
        case "Q": // Temp Collector
          return "is-danger";
        case "C": // Collector
        case "R": // Receiver
        case "P": // Temp Donor
        case "V": // Temp Farm
          return "is-info";
        default: // Unknown
          return "is-light";
      }
    }
  }
}
</script>