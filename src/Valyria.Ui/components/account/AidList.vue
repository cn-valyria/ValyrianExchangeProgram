<template>
  <div>
    <div v-if="slotsFree === 0" class="box py-6">
      <div class="content has-text-centered">
        <h5>You are not expected to interact with VEP today</h5>
        <p>
          Your nation does not have any free foreign aid slots, so you are not expected to send or receive any transactions today. Please check
          again tomorrow for the updated status of your nation.
        </p>
      </div>
    </div>
    <Accordion v-else>
      <AccordionPanel>
        <AccordionHeader>
          <span v-if="owesTech">Tech Targets</span>
          <span v-else-if="owesCash">Cash Targets</span>
          <span v-else-if="expectsCash">Incoming Cash</span>
          <span v-else-if="expectsTech">Incoming Tech</span>
          <span v-else>Potential Transactions</span>
          &nbsp;
          <span class="has-text-weight-normal">({{ aidList.length }} nations)</span>
        </AccordionHeader>
        <AccordionContent>
          <div class="content">
            <p>
              Please
              <span v-if="owesTech">send tech to </span>
              <span v-else-if="owesCash">send cash to </span>
              <span v-else-if="expectsCash">accept cash from </span>
              <span v-else-if="expectsTech">accept tech from </span>
              <span v-else>Potential Transactions</span>
              any of the nations in the following list.
            </p>
            <p v-if="expectsCash || expectsTech">
              IMPORTANT: <strong>Do not use this list to send aid. </strong>
              It should only be used to verify that nations sending you aid are doing so legitimately.
            </p>
            <DataTable :value="aidList">
              <Column field="nation" header="Nation">
                <template #body="slotProps">
                  <a v-if="owesCash || owesTech" :href="getAidNationLink(slotProps.data.nation)" target="_blank">
                    {{ slotProps.data.nation.rulerName }} of {{ slotProps.data.nation.nationName }}
                  </a>
                  <span v-else>
                    {{ slotProps.data.nation.rulerName }} of {{ slotProps.data.nation.nationName }}
                  </span>
                </template>
              </Column>
              <Column field="nation.allianceName" header="Alliance">
                <template #body="slotProps">
                  <div class="columns is-mobile">
                    <div class="column is-narrow">
                      <ChipAllianceAcronym :allianceName="slotProps.data.nation.allianceName"></ChipAllianceAcronym>
                    </div>
                    <div class="column">
                      {{ slotProps.data.nation.allianceName }}
                    </div>
                  </div>
                </template>
              </Column>
              <Column field="slotsFree" header="Slots Free">
                <template #body="slotProps">
                  {{ slotProps.data.slotsFree }}
                </template>
              </Column>
              <Column field="discord" header="Discord Tag">
                <template #body="slotProps">
                  {{ slotProps.data.discord }}
                </template>
              </Column>
            </DataTable>
          </div>
        </AccordionContent>
      </AccordionPanel>
    </Accordion>
  </div>
</template>

<script setup lang="ts">
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'

interface Nation {
  nationId: number
  nationName: string
  rulerName: string
  allianceName: string
}

interface AidListItem {
  nation: Nation
  slotsFree: number
  discord: string
}

interface Account {
  slotsFull: number
  slotsUsed: number
  role: string
  credit: number
  debt: number
  nationId: number
}

interface Props {
  account: Account
  aidList: AidListItem[]
}

const props = defineProps<Props>()

const slotsFree = computed(() => {
  return props.account.slotsFull - props.account.slotsUsed
})

const owesCash = computed(() => {
  const rolesThatSendCash = [ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.cashDonor, ACCOUNT_ROLE_CODES.temporaryDonor]
  return rolesThatSendCash.includes(props.account.role) && props.account.credit === 0
})

const expectsCash = computed(() => {
  if (props.account.role === ACCOUNT_ROLE_CODES.seller || props.account.role === ACCOUNT_ROLE_CODES.probationarySeller) {
    return props.account.debt === 0
  } else if ([ACCOUNT_ROLE_CODES.cashCollector, ACCOUNT_ROLE_CODES.temporaryCollector].includes(props.account.role)) {
    return props.account.credit === 0
  } else {
    return false
  }
})

const owesTech = computed(() => {
  const rolesThatSendTech = [
    ACCOUNT_ROLE_CODES.seller, ACCOUNT_ROLE_CODES.probationarySeller,
    ACCOUNT_ROLE_CODES.techFarm, ACCOUNT_ROLE_CODES.temporaryFarm
  ]

  return rolesThatSendTech.includes(props.account.role) && props.account.debt > 0
})

const expectsTech = computed(() => {
  const rolesThatExpectTech = [
    ACCOUNT_ROLE_CODES.buyer, ACCOUNT_ROLE_CODES.techReceiver, ACCOUNT_ROLE_CODES.temporaryReceiver
  ]

  return rolesThatExpectTech.includes(props.account.role) && props.account.credit > 0
})

const getAidNationLink = (nation: Nation) => {
  return `https://www.cybernations.net/aid_form.asp?Nation_ID=${nation.nationId}&bynation=${props.account.nationId}`
}
</script>
