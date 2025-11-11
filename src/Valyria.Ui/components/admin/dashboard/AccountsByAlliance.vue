<template>
  <div>
    <p class="title">Most Accounts</p>
    <div v-for="(row, index) in data" :key="row.allianceName">
      <div class="columns">
        <div class="column is-narrow">
          <chip-alliance-acronym :allianceName="row.allianceName" />
        </div>
        <div class="column">
          {{ row.allianceName }}
        </div>
        <div class="column is-narrow">
          <span class="tag is-rounded">{{ row.totalAccounts }}</span>
        </div>
      </div>
      <div v-if="index !== (data.length - 1)" class="is-divider"></div>
    </div>
  </div>
</template>

<style>
.is-divider {
  margin: 0.5rem 0;
}
</style>

<script>
export default {
  props: {
    data: []
  }
}
</script>