<template>
  <div>
    <p class="title">Total Transactions</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="line" :options="options" :series="series"></ApexChart> -->
    </div>
  </div>
</template>

<script>
import dateFormat from 'dateformat';

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
        chart: {
          toolbar: {
            show: false
          }
        },
        stroke: {
          curve: "smooth"
        },
        xaxis: {
          categories: this.data && this.data.map(x => dateFormat(new Date(x.dayTxnRecorded), "mmm-dd")) || []
        }
      };
    },
    series() {
      return [
        {
          name: "Transactions",
          data: this.data && this.data.map(x => x.txnCount) || []
        }
      ];
    }
  }
}
</script>