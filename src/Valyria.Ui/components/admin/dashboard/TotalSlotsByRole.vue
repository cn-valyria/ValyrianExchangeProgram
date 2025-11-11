<template>
  <div>
    <p class="title">Total Aid Slots</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="bar" :options="options" :series="series"></ApexChart> -->
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
        chart: {
          toolbar: {
            show: false
          }
        },
        plotOptions: {
          bar: {
            distributed: true
          }
        },
        xaxis: {
          categories: this.data && this.data.map(x => accountRoles.find(role => role.code === x.role).description) || []
        },
        yaxis: {
          title: {
            text: "Total Aid Slots"
          }
        },
        legend: {
          show: false
        }
      };
    },
    series() {
      return [
        {
          name: "Total Aid Slots",
          data: this.data && this.data.map(x => x.totalSlots) || []
        }
      ]
    }
  }
}
</script>