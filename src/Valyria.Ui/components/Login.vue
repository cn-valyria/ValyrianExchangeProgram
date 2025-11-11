<template>
  <div class="modal" :class="{ 'is-active': show }">
    <div class="modal-background"></div>
    <div class="modal-card">
      <header class="modal-card-head">
        <p class="modal-card-title">VEP Login Portal</p>
        <button type="button" class="delete" @click="$emit('close')"></button>
      </header>
      <section class="modal-card-body">
        <div class="columns">
          <div class="column">
            <b-field
              label="Nation ID"
              :type="{ 'is-danger': nationIdOrRulerNameIsMissing }"
              :message="{ 'Nation ID or Ruler Name is required.': nationIdOrRulerNameIsMissing }"
            >
              <b-input type="text" v-model="nationId"></b-input>
            </b-field>
          </div>
          <div class="is-divider-vertical" data-content="OR"></div>
          <div class="column">
            <b-field
              label="Ruler Name"
              :type="{ 'is-danger': nationIdOrRulerNameIsMissing }"
              :message="{ 'Nation ID or Ruler Name is required.': nationIdOrRulerNameIsMissing }"
            >
              <b-input type="text" v-model="rulerName"></b-input>
            </b-field>
          </div>
        </div>
        <b-field
          label="Unique Code"
          :type="{ 'is-danger': uniqueCodeIsMissing }"
          :message="{ 'Unique Code is required.': uniqueCodeIsMissing }"
        >
          <b-input type="text" v-model="uniqueCode"></b-input>
        </b-field>
        <!-- <b-checkbox>Remember Me</b-checkbox> -->
      </section>
      <footer class="modal-card-foot">
        <b-button label="Close" @click="$emit('close')" />
        <b-button type="is-primary" label="Log In" @click="login()" />
      </footer>
    </div>
  </div>
</template>

<style>
.checkbox {
  /* The buefy checkbox is stupid and only colors the text on hover, so this forces it to always be visible */
  color: #363636 !important;
}
</style>

<script>
export default {
  name: "LoginVepUser",
  props: {
    show: Boolean
  },
  data: () => ({
    nationId: "",
    rulerName: "",
    uniqueCode: "",
    nationIdOrRulerNameIsMissing: false,
    uniqueCodeIsMissing: false
  }),
  methods: {
    async login() {
      if (!this.formIsValid()) {
        return;
      }

      await this.$auth.loginWith("local", {
        data: {
          nationId: this.nationId,
          rulerName: this.rulerName,
          uniqueCode: this.uniqueCode
        }
      });

      this.$emit("close");
    },
    formIsValid() {
      this.nationIdOrRulerNameIsMissing = (this.nationId.length === 0 && this.rulerName.length === 0);
      this.uniqueCodeIsMissing = this.uniqueCode.length === 0;

      return !(this.nationIdOrRulerNameIsMissing || this.uniqueCodeIsMissing);
    }
  }
}
</script>