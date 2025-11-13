<template>
  <UCard class="text-center">
    <div class="space-y-4">
      <p class="text-lg text-gray-600 dark:text-gray-400">Your Current Balance</p>
      <p class="text-5xl font-bold" :class="categoryClass">
        {{ positivityIndicator }}{{ amount }} {{ balanceType }} ({{ balanceCategory }})
      </p>
      <p class="text-sm text-gray-600 dark:text-gray-400">{{ description }}</p>
    </div>
  </UCard>
</template>

<script setup lang="ts">
import { ACCOUNT_ROLE_CODES } from "@/infrastructure/constants"

interface Props {
  credit: number
  debt: number
  role: string
}

const props = defineProps<Props>()

const balance = computed(() => {
  if (props.credit === undefined || props.debt === undefined) {
    return null
  }

  // At least one of credit or debt will always be 0, so the balance amount is always the max of the two
  return Math.max(props.credit, props.debt)
})

const balanceCategory = computed(() => {
  if (balance.value === null) {
    return null
  }

  if (balance.value === 0) {
    return "Even" // If the max is 0, then the account has neither credit nor debt
  } else if (balance.value === props.credit) {
    return "Credit"
  } else {
    return "Debt"
  }
})

const balanceType = computed(() => {
  if (balanceCategory.value === null || props.role === undefined) {
    return null
  }

  switch (props.role) {
    // Buyers always show credit as tech, but debt as cash
    case ACCOUNT_ROLE_CODES.buyer:
      return balanceCategory.value === "Credit" ? "Tech" : "Cash"

    // Sellers always show credit as cash, but debt as tech
    case ACCOUNT_ROLE_CODES.seller:
    case ACCOUNT_ROLE_CODES.probationarySeller:
      return balanceCategory.value === "Credit" ? "Cash" : "Tech"

    // Donors and Collectors are always exchanging cash
    case ACCOUNT_ROLE_CODES.cashDonor:
    case ACCOUNT_ROLE_CODES.temporaryDonor:
    case ACCOUNT_ROLE_CODES.cashCollector:
    case ACCOUNT_ROLE_CODES.temporaryCollector:
      return "Cash"

    // Farms and Receivers are always exchanging tech
    case ACCOUNT_ROLE_CODES.techFarm:
    case ACCOUNT_ROLE_CODES.temporaryFarm:
    case ACCOUNT_ROLE_CODES.techReceiver:
    case ACCOUNT_ROLE_CODES.temporaryReceiver:
      return "Tech"

    default:
      return null
  }
})

const categoryClass = computed(() => {
  switch (balanceCategory.value) {
    case "Credit": return "text-green-600 dark:text-green-400"
    case "Debt": return "text-red-600 dark:text-red-400"
    case "Even": return "text-green-600 dark:text-green-400"
    default: return "text-gray-400 dark:text-gray-500"
  }
})

const positivityIndicator = computed(() => {
  if (balance.value !== 1 && balanceCategory.value !== "Even") {
    return balanceCategory.value === "Credit" ? "+" : "-"
  } else {
    return ""
  }
})

const amount = computed(() => {
  if (balance.value === 1) {
    return "\u221E"
  } else if (balance.value === 0) {
    return "0"
  } else {
    return `${balanceType.value === "Cash" ? "$" : ""}${balance.value}${balanceType.value === "Cash" ? "M" : ""}`
  }
})

const description = computed(() => {
  if (balanceCategory.value === "Credit" && balance.value !== 1) {
    return `Your nation is due ${amount.value} ${balanceType.value} from other VEP accounts.`
  }
  if (balanceCategory.value === "Debt" && balance.value !== 1) {
    return `Your nation owes ${amount.value} ${balanceType.value} to its VEP account.`
  }
  if (balanceCategory.value === "Credit" && balance.value === 1) {
    return `Your nation receives infinite ${balanceType.value} and will always show a Credit balance.`
  }
  if (balanceCategory.value === "Debt" && balance.value === 1) {
    return `Your nation produces infinite ${balanceType.value} and will always show a Debt balance.`
  }
  if (balanceCategory.value === "Even") {
    switch (props.role) {
      case ACCOUNT_ROLE_CODES.buyer:
        return "Your account currently has an even balance! This means that you need to send cash to other VEP accounts."
      case ACCOUNT_ROLE_CODES.seller:
      case ACCOUNT_ROLE_CODES.probationarySeller:
        return "Your account currently has an even balance! This means that you are due cash from other VEP accounts."
    }
  }

  return ""
})
</script>
