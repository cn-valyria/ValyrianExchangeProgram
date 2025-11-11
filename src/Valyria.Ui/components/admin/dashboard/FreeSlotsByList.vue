<template>
  <div>
    <p class="title">Free Aid Slots</p>
    <div class="content">
      <!-- <ApexChart width="100%" type="bar" :options="options" :series="series"></ApexChart> -->
    </div>
  </div>
</template>

<script>
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
          categories: this.data && this.data.map(x => x.listName) || []
        },
        yaxis: {
          title: {
            text: "Free Aid Slots"
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
          name: "Free Aid Slots",
          data: this.data && this.data.map(x => x.freeSlots) || []
        }
      ]
    }
  }
}
</script>