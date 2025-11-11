<template>
  <b-taglist attached>
    <b-tag type="is-dark"><img :src="allianceImage" height="18" width="18" /></b-tag>
    <b-tag type="is-dark">{{ allianceAcronym }}</b-tag>
  </b-taglist>
</template>

<script>
export default {
  props: {
    allianceName: String
  },
  data: () => ({
    allianceAcronyms: [
      { code: "CCC", name: "Christian Coalition Of Countries" },
      { code: "FAN", name: "Federation of Armed Nations" },
      { code: "FARK", name: "Fark" },      
      { code: "FTW", name: "Freehold of the Wolves" },
      { code: "MHA", name: "Mostly Harmless Alliance" },
      { code: "NATO", name: "NATO" },
      { code: "NG", name: "Non Grata" },
      { code: "OG", name: "Old Guard" },
      { code: "RIA", name: "Random Insanity Alliance" },
      { code: "TIO", name: "The Imperial Order" }
    ]
  }),
  computed: {
    allianceAcronym() {
      if (this.allianceName === undefined) {
        return "";
      }

      const acronym = this.allianceAcronyms.find(aa => aa.name.localeCompare(this.allianceName, "en", { sensitivity: "base" }) === 0);
      if (acronym !== undefined) {
        return acronym.code;
      } else {
        return this.allianceName.split(/\s/).reduce((result, word) => result += word.slice(0, 1), "");
      }
    },
    allianceImage() {
      let fileName = "none";

      // Since we already have a computed property for figuring out a known acronym, only change 
      // the fileName if that property is one of those known values
      if (this.allianceAcronyms.some(aa => aa.code === this.allianceAcronym)) {
        fileName = this.allianceAcronym.toLowerCase();
      }

      return require(`@/assets/img/alliance/${fileName}.jpg`);
    }
  }
}
</script>
