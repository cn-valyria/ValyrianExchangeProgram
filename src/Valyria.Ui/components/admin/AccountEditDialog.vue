<template>
  <div class="modal" :class="{ 'is-active': show }">
    <div class="modal-background"></div>
    <div class="modal-card">
      <header class="modal-card-head">
        <p v-if="isInUpdateState" class="modal-card-title">{{ account.rulerName }}'s Account</p>
        <p v-else class="modal-card-title">New Account</p>
        <button class="delete" aria-label="close" @click="$emit('close')"></button>
      </header>
      <section class="modal-card-body">
        <div class="content">
          <h2>Account Data</h2>
          <fieldset :disabled="isInUpdateState">
            <div class="field">
              <label class="label">Nation ID</label>
              <div class="control">
                <input v-model="nationId" class="input" type="text" placeholder="The account's NationID" />
              </div>
            </div>
          </fieldset>
          <fieldset disabled>
            <div class="field is-horizontal">
              <div class="field-body">
                <div class="field">
                  <label class="label">Nation Name</label>
                  <div class="control">
                    <input v-model="accountBeingEdited.nationName" class="input" type="text" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Ruler Name</label>
                  <div class="control">
                    <input v-model="accountBeingEdited.rulerName" class="input" type="text" />
                  </div>
                </div>
              </div>
            </div>
            <div class="field">
              <div class="label">Alliance Name</div>
              <div class="control">
                <input v-model="accountBeingEdited.allianceName" class="input" type="text" />
              </div>
            </div>
          </fieldset>
          <fieldset v-if="isInUpdateState || foundProspectAccount">
            <div class="field">
              <label class="label">Role</label>
              <div class="control is-expanded">
                <div class="select is-fullwidth" :class="{ 'is-danger': propertyHasValidationError('role') }">
                  <select v-model="accountBeingEdited.role">
                    <option disabled value=""></option>
                    <option 
                      v-for="role of roles" 
                      :key="role.code" 
                      :value="role.code"
                    >
                      {{ role.description }}
                    </option>
                  </select>
                </div>
              </div>
              <p v-if="propertyHasValidationError('role')" class="help is-danger">
                Every account must have a role that they are enrolled as
              </p>
            </div>
            <div class="field">
              <label class="label">Secret Code</label>
              <div class="field has-addons">
                <div class="control is-expanded">
                  <input
                    v-model="accountBeingEdited.uniqueCode"
                    class="input"
                    :class="{ 'is-danger': propertyHasValidationError('uniqueCode') }"
                    type="text" />
                </div>
                <div class="control">
                  <button class="button is-info" @click="generateSecretCode()">
                    Generate
                  </button>
                </div>
              </div>
              <p v-if="propertyHasValidationError('uniqueCode')" class="help is-danger">
                Every account must have a unique code provided
              </p>
            </div>
            <div class="field is-horizontal">
              <div class="field-body">
                <div class="field">
                  <label class="label">Discord Username</label>
                  <div class="control">
                    <input 
                      v-model="accountBeingEdited.discord" 
                      class="input" 
                      type="text"
                      placeholder="e.g. username or username#1234" />
                  </div>
                </div>
                <div class="field">
                  <label class="label">Discord User ID</label>
                  <div class="control">
                    <input 
                      v-model="accountBeingEdited.discordUniqueId" 
                      class="input" 
                      type="text"
                      placeholder="e.g. 318737615257993226" />
                  </div>
                </div>
              </div>
            </div>
            <div class="field">
              <div class="control">
                <label class="label">Has Foreign Ministry?</label>
                <label class="radio">
                  <input 
                    v-model="accountBeingEdited.hasForeignMinistry" 
                    :value="true" 
                    type="radio"
                    name="hasForeignMinistry" />
                  Yes
                </label>
                <label class="radio">
                  <input 
                    v-model="accountBeingEdited.hasForeignMinistry" 
                    :value="false" 
                    type="radio"
                    name="hasForeignMinistry" />
                  No
                </label>
              </div>
              <p v-if="propertyHasValidationError('hasForeignMinistry')" class="help is-danger">
                Must indicate if the nation has the FM improvement or not
              </p>
            </div>
            <div class="field">
              <div class="control">
                <label class="label">Has Federal Aid Commission?</label>
                <label class="radio">
                  <input 
                    v-model="accountBeingEdited.hasFederalAidCommission" 
                    :value="true" 
                    type="radio"
                    name="hasFederalAidCommission" />
                  Yes
                </label>
                <label class="radio">
                  <input 
                    v-model="accountBeingEdited.hasFederalAidCommission" 
                    :value="false" 
                    type="radio"
                    name="hasFederalAidCommission" />
                  No
                </label>
              </div>
              <p v-if="propertyHasValidationError('hasFederalAidCommission')" class="help is-danger">
                Must indicate if the nation has the FAC wonder or not
              </p>
            </div>
            <div class="field">
              <div class="control">
                <label class="label">Has Disaster Relief Agency?</label>
                <label class="radio">
                  <input 
                    v-model="accountBeingEdited.hasDisasterReliefAgency" 
                    :value="true" 
                    type="radio"
                    name="hasDisasterReliefAgency" />
                  Yes
                </label>
                <label class="radio">
                  <input 
                    v-model="accountBeingEdited.hasDisasterReliefAgency" 
                    :value="false" 
                    type="radio"
                    name="hasDisasterReliefAgency" />
                  No
                </label>
              </div>
              <p v-if="propertyHasValidationError('hasDisasterReliefAgency')" class="help is-danger">
                Must indicate if the nation has the DRA wonder or not
              </p>
            </div>
            <div v-if="isInUpdateState">
              <h2>Transactional Data</h2>
              <fieldset disabled>
                <div class="field is-horizontal">
                  <div class="field-body">
                    <div class="field">
                      <label class="label">Slots Free</label>
                      <div class="control">
                        <input 
                          :value="accountBeingEdited.slotsFull - accountBeingEdited.slotsUsed" 
                          class="input"
                          type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Slots Full</label>
                      <div class="control">
                        <input :value="accountBeingEdited.slotsFull" class="input" type="text" />
                      </div>
                    </div>
                  </div>
                </div>
                <div class="field is-horizontal">
                  <div class="field-body">
                    <div class="field">
                      <label class="label">Credit</label>
                      <div class="control">
                        <input :value="accountBeingEdited.credit" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Debt</label>
                      <div class="control">
                        <input :value="accountBeingEdited.debt" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Balance</label>
                      <div class="control">
                        <input :value="accountBeingEdited.balance" class="input" type="text" />
                      </div>
                    </div>
                  </div>
                </div>
                <div class="field is-horizontal">
                  <div class="field-body">
                    <div class="field">
                      <label class="label">Cash Sent (Cash)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.cashSentCashCredit" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Cash Rec'd (Cash)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.cashReceivedCashCredit" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Tech Sent (Cash)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.techSentCashCredit" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Tech Rec'd (Cash)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.techReceivedCashCredit" class="input" type="text" />
                      </div>
                    </div>
                  </div>
                </div>
                <div class="field is-horizontal">
                  <div class="field-body">
                    <div class="field">
                      <label class="label">Cash Sent (Tech)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.cashSentTechCredit" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Cash Rec'd (Tech)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.cashReceivedTechCredit" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Tech Sent (Tech)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.techSentTechCredit" class="input" type="text" />
                      </div>
                    </div>
                    <div class="field">
                      <label class="label">Tech Rec'd (Tech)</label>
                      <div class="control">
                        <input :value="accountBeingEdited.techReceivedTechCredit" class="input" type="text" />
                      </div>
                    </div>
                  </div>
                </div>
              </fieldset>
            </div>
          </fieldset>
        </div>
      </section>
      <footer class="modal-card-foot">
        <button
          v-if="isInUpdateState"
          class="button is-success" 
          :class="{ 'is-loading': buttonIsLoading }"
          @click="update()"
        >
          Save
        </button>
        <button
          v-else-if="foundProspectAccount"
          :class="{ 'is-loading': buttonIsLoading }"
          class="button is-success"
          @click="create()"
        >
          Create
        </button>
        <button
          v-else 
          class="button is-success" 
          :class="{ 'is-loading': buttonIsLoading }"
          @click="findProspectAccount()"
        >
          Search
        </button>
        <button class="button" @click="$emit('close')">Cancel</button>
      </footer>
    </div>
  </div>
</template>

<script>
import { mapActions } from 'vuex';
import { ERROR_MESSAGES } from '~/infrastructure/constants';
import { accountRoles } from '~/infrastructure/dataLists';

export default {
  props: {
    show: Boolean,
    account: {
      type: Object,
      default: () => ({})
    },
  },
  data: () => ({
    buttonIsLoading: false,
    accountBeingEdited: Object,
    roles: accountRoles,
    validationErrors: []
  }),
  computed: {
    nationId: {
      get() {
        return this.accountBeingEdited.nationId;
      },
      set(val) {
        if (val !== "" && !isNaN(parseInt(val))) {
          this.accountBeingEdited.nationId = parseInt(val);
        } else {
          this.$log.debug(`Received nationId val ${val}. Setting to empty string`);
          this.accountBeingEdited.nationId = "";
        }
      }
    },
    isInUpdateState() {
      return this.accountBeingEdited !== undefined && this.accountBeingEdited.id !== undefined;
    },
    foundProspectAccount() {
      return this.accountBeingEdited !== undefined && this.accountBeingEdited.rulerName !== undefined;
    }
  },
  watch: {
    account(val) {
      this.$log.debug(val);
      this.accountBeingEdited = { ...val };
    }
  },
  methods: {
    ...mapActions({
      updateAccount: "admin/accounts/updateAccount",
      createAccount: "admin/accounts/createAccount"
    }),
    async findProspectAccount() {
      this.buttonIsLoading = true;
      try {
        const response = await this.$axios.get(`/accounts/prospect?nationId=${this.accountBeingEdited.nationId}`);
        this.$log.debug(response);
        const prospectAccount = response.data;

        this.accountBeingEdited = { 
          ...this.accountBeingEdited,
          nationName: prospectAccount.nationName,
          rulerName: prospectAccount.rulerName,
          allianceName: prospectAccount.allianceName
        };
      } catch (e) {
        this.$log.error(e);
        if (e.response) {
          this.$toast.error(e.response.data);
        } else {
          this.$toast.error("VEP encountered an error while searching for the above nation ID. Please try again, or contact @lilweirdward if the error persists.");
        }
      }

      this.buttonIsLoading = false;
    },
    async update() {
      this.buttonIsLoading = true;
      this.$log.debug(this.accountBeingEdited);
      try {
        await this.updateAccount(this.accountBeingEdited);
      } catch (e) {
        this.$log.error(e);
        this.$toast.error(ERROR_MESSAGES.default);
      }
      
      this.buttonIsLoading = false;
      this.$emit("close");
      this.$toast.success("Account updated successfully!")
    },
    async create() {
      if (!this.accountIsValid()) {
        this.$log.error("Account form is invalid and will not be submitted to the API");
        return;
      }

      this.buttonIsLoading = true;
      this.$log.debug(this.accountBeingEdited);
      try {
        await this.createAccount(this.accountBeingEdited);
      } catch (e) {
        this.$log.error(e);
        this.$toast.error(ERROR_MESSAGES.default);
      }

      this.buttonIsLoading = false;
      this.$emit("close");
      this.$toast.success("Account created successfully!");
    },
    randomIntFromInterval(min, max) {
      return Math.floor(Math.random() * (max - min + 1) + min)
    },
    generateSecretCode() {
      const newCode = String(this.randomIntFromInterval(1, 99999)).padStart(5, "0");
      this.$set(this.accountBeingEdited, "uniqueCode", newCode);
    },
    accountIsValid() {
      this.validateProperty("role");
      this.validateProperty("uniqueCode", val => val === undefined || val === "");
      this.validateProperty("hasForeignMinistry");
      this.validateProperty("hasFederalAidCommission");
      this.validateProperty("hasDisasterReliefAgency");

      return this.validationErrors.length === 0;
    },
    validateProperty(prop, propIsInvalidFunc = val => val === undefined) {
      const validationIndex = this.validationErrors.findIndex(e => e.prop === prop);
      const propIsInvalid = propIsInvalidFunc(this.accountBeingEdited[prop]);
      const propAlreadyHasError = validationIndex > -1;
      if (propIsInvalid && !propAlreadyHasError) {
        this.validationErrors.push({ prop });
      } else if (!propIsInvalid && propAlreadyHasError) {
        this.validationErrors.splice(validationIndex, 1);
      }
    },
    propertyHasValidationError(prop) {
      return this.validationErrors.some(e => e.prop === prop);
    }
  }
}
</script>