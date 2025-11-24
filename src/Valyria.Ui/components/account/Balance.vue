<template>
  <div class="flex flex-col items-center gap-2 min-w-fit">
    <!-- Balance Amount & Badge Row -->
    <div class="flex items-center gap-3">
      <div class="text-center">
        <div class="text-3xl font-bold" :class="categoryClass">
          {{ positivityIndicator }}{{ amount }}
        </div>
        <div class="text-sm font-medium text-gray-600 dark:text-gray-400">
          {{ balanceType }}
        </div>
          <div class="text-sm font-medium text-gray-600 dark:text-gray-400">
            Balance
          </div>
      </div>
      <UBadge :color="badgeColor" size="md" variant="subtle" class="shrink-0">
        {{ balanceCategory }}
      </UBadge>
    </div>

    <!-- Description Row -->
    <div class="text-xs text-gray-600 dark:text-gray-400 text-center max-w-xs">
      {{ description }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { ACCOUNT_ROLE_CODES } from "@/infrastructure/constants"

interface Props {
  credit: number | undefined
  debt: number | undefined
  role: string | undefined
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

  if (props.role === ACCOUNT_ROLE_CODES.onHold) {
    return "Hold"
  } else if (balance.value === 0) {
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

const badgeColor = computed(() => {
  switch (balanceCategory.value) {
    case "Credit": return "success"
    case "Debt": return "error"
    case "Even": return "success"
    default: return "neutral"
  }
})

const positivityIndicator = computed(() => {
  if (balance.value !== 1 && !["Even", "Hold"].includes(balanceCategory.value)) {
    return balanceCategory.value === "Credit" ? "+" : "-"
  } else {
    return ""
  }
})

const amount = computed(() => {
  if (props.role === ACCOUNT_ROLE_CODES.onHold) {
    return "--"
  } else if (balance.value === 1) {
    return "\u221E"
  } else if (balance.value === 0) {
    return "0"
  } else {
    return `${balanceType.value === "Cash" ? "$" : ""}${balance.value}${balanceType.value === "Cash" ? "M" : ""}`
  }
})

const description = computed(() => {
  if (balanceCategory.value === "Hold") {
    return "Your account is on hold and cannot participate in VEP. If you feel this is an error, please contact VEP management for support."
  }
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
      default:
        return "It shouldn't be possible for you to have an even balance. Please contact VEP management for support."
    }
  }

  return ""
})
</script>
