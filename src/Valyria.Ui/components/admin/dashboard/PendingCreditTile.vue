<template>
  <div>
    <p class="title">Pending Credit</p>
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
  //   ApexChart: () => import("vue-apexcharts")
  // },
  computed: {
    options() {
      return {
        chart: {
          stacked: true,
          toolbar: {
            show: false
          }
        },
        plotOptions: {
          bar: {
            horizontal: true,
            dataLabels: {
              total: {
                enabled: true,
                offsetX: 0
              }
            }
          }
        },
        xaxis: {
          categories: this.data && this.data.map(x => this.getAllianceAcronym(x.allianceName)) || []
        }
      };
    },
    series() {
      if (this.data === undefined) {
        return [];
      }

      this.$log.debug(this.data);
      
      const dateRanges = ["0-2 Days", "3-5 Days", "6-8 Days", "9+ Days"];
      return dateRanges.map(rangeName => ({
        name: rangeName,
        data: this.data.map(x => x.creditGroups.find(row => row.dateRange === rangeName)?.totalCredit ?? 0)
      }));
    }
  },
  methods: {
    getAllianceAcronym(allianceName) {
      switch (allianceName) {
        case "NATO": return "NATO";
        case "Freehold of The Wolves": return "FTW";
        case "Christian Coalition Of Countries": return "CCC";
        default: return allianceName;
      }
    }
  }
}
</script>