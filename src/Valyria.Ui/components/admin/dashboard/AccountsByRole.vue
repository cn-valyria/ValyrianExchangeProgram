<template>
  <div>
    <p class="title">VEP Accounts</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="pie" :options="options" :series="series"></ApexChart> -->
    </div>
  </div>
</template>

<script>
import { accountRoles } from '~/infrastructure/dataLists';

export default {
  props: {
    data: []
  },
  // components: {
  //   ApexChart: () => import('vue-apexcharts')
  // },
  computed: {
    options() {
      return {
        labels: this.data && this.data.map(x => accountRoles.find(role => role.code === x.role).description) || []
      };
    },
    series() {
      return this.data && this.data.map(x => x.totalAccounts) || [];
    }
  }
}
</script>