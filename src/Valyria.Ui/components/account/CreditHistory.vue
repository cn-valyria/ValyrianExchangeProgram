<template>
  <Accordion>
    <AccordionPanel>
      <AccordionHeader>
        Credit History&nbsp;
        <span class="has-text-weight-normal">
          +{{ creditHistory.filter(txn => txn.balanceChangeType === "Credit").length }} out,
          -{{ creditHistory.filter(txn => txn.balanceChangeType === "Debt").length }} in
        </span>
      </AccordionHeader>
      <AccordionContent>
        <div class="content">
          <p>
            Your entire history of credit changes, as recorded in the VEP. This history includes both aid-based transactions, as well as manually-
            configured credits or debts that may have been added to your account by an admin. Please contact VEP Management if you have any
            concerns about your credit history.
          </p>
          <DataTable :value="creditHistory">
            <Column field="otherNation" header="Other Nation">
              <template #body="slotProps">
                {{ slotProps.data.otherNation !== null ? `${slotProps.data.otherNation.rulerName} of ${slotProps.data.otherNation.nationName}` : "VEP Management" }}
              </template>
            </Column>
            <Column field="type" header="Type">
              <template #body="slotProps">
                {{ slotProps.data.type }}
              </template>
            </Column>
            <Column field="status" header="Status">
              <template #body="slotProps">
                {{ slotProps.data.otherNation !== null ? toAidStatusDescription(slotProps.data.status) : "Active" }}
              </template>
            </Column>
            <Column field="money" header="Money">
              <template #body="slotProps">
                {{ slotProps.data.money !== null ? slotProps.data.money : "N/A" }}
              </template>
            </Column>
            <Column field="technology" header="Tech">
              <template #body="slotProps">
                {{ slotProps.data.technology !== null ? slotProps.data.technology : "N/A" }}
              </template>
            </Column>
            <Column field="balanceChangeType" header="Credit Change">
              <template #body="slotProps">
                <span :class="{
                  'has-text-success': slotProps.data.balanceChangeType === 'Credit',
                  'has-text-error': slotProps.data.balanceChangeType === 'Debt'
                }">
                  {{ toCreditChange(slotProps.data) }}
                </span>
              </template>
            </Column>
            <Column field="sentOn" header="Sent On">
              <template #body="slotProps">
                {{ slotProps.data.sentOn !== null ? slotProps.data.sentOn.split('T')[0] : "N/A" }}
              </template>
            </Column>
          </DataTable>
        </div>
      </AccordionContent>
    </AccordionPanel>
  </Accordion>
</template>

<script setup lang="ts">
import { ACCOUNT_ROLE_CODES } from '~/infrastructure/constants'

interface Nation {
  nationName: string
  rulerName: string
}

interface CreditHistoryItem {
  otherNation: Nation | null
  type: string
  status: number
  money: number | null
  technology: number | null
  balanceChangeType: string
  sentOn: string | null
  rate: number
}

interface Props {
  accountRole: string
  creditHistory: CreditHistoryItem[]
}

const props = defineProps<Props>()

const toAidStatusDescription = (statusId: number): string => {
  switch (statusId) {
    case 1: return "Pending"
    case 2: return "Approved"
    case 3: return "Cancelled"
    case 4: return "Expired"
    default: return "Unknown"
  }
}

const toCreditChange = (txn: CreditHistoryItem): string => {
  const positivityIndicator = txn.balanceChangeType === "Credit" ? "+" : "-"
  let cashOrTech: string
  let receivedOrSent: string

  if (txn.money !== null) {
    cashOrTech = txn.money > 0 ? "$9m" : "100 tech"
    receivedOrSent = txn.type === "Incoming" ? "received" : "sent"
  } else {
    const cash = `$${txn.rate}m`
    const tech = `${txn.rate / 9 * 100} tech`
    switch (props.accountRole) {
      case ACCOUNT_ROLE_CODES.buyer:
        cashOrTech = txn.balanceChangeType === "Credit" ? tech : cash
        break
      case ACCOUNT_ROLE_CODES.seller:
      case ACCOUNT_ROLE_CODES.probationarySeller:
        cashOrTech = txn.balanceChangeType === "Credit" ? cash : tech
        break
      default:
        // If we can't figure out why they were given a manual txn, then just call it units
        return `${txn.rate} units`
    }

    receivedOrSent = txn.balanceChangeType === "Credit" ? "added" : "deducted"
  }

  return `${positivityIndicator}${cashOrTech} ${receivedOrSent}`
}
</script>
