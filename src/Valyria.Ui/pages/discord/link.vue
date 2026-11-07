<template>
  <div
    class="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900 py-12 px-4 sm:px-6 lg:px-8"
  >
    <div class="max-w-md w-full space-y-8">
      <div class="text-center">
        <NuxtLink to="/">
          <img
            src="~/assets/img/logo.png"
            class="h-16 mx-auto"
            alt="VEP Logo"
          />
        </NuxtLink>
        <h2 class="mt-6 text-3xl font-extrabold text-gray-900 dark:text-white">
          Link your Discord account
        </h2>
      </div>

      <UCard>
        <!-- No token in the link -->
        <div v-if="!token" class="space-y-4 text-center">
          <UIcon
            name="i-heroicons-exclamation-triangle"
            class="w-10 h-10 text-error-500 mx-auto"
          />
          <p class="text-gray-700 dark:text-gray-300">
            This link is missing its code. Run <strong>/register</strong> in
            Discord to get a new one.
          </p>
        </div>

        <!-- Signed out: log in, then come back here -->
        <div v-else-if="!isAuthenticated" class="space-y-4 text-center">
          <p class="text-gray-700 dark:text-gray-300">
            Log into your VEP account to link your Discord account to it.
          </p>
          <UButton
            :to="{ path: '/login', query: { redirect: route.fullPath } }"
            color="primary"
            block
            size="lg"
          >
            Log In
          </UButton>
        </div>

        <div v-else-if="state === 'loading'" class="flex justify-center py-6">
          <UIcon
            name="i-heroicons-arrow-path"
            class="w-10 h-10 text-primary-500 animate-spin"
          />
        </div>

        <!-- Expired, tampered with, or not issued by VEP -->
        <div v-else-if="state === 'invalid'" class="space-y-4 text-center">
          <UIcon
            name="i-heroicons-exclamation-triangle"
            class="w-10 h-10 text-error-500 mx-auto"
          />
          <p class="text-gray-700 dark:text-gray-300">
            This link is invalid or has expired. Run
            <strong>/register</strong> in Discord to get a new one.
          </p>
          <UButton :to="accountPath" color="neutral" variant="outline" block>
            Go to my account
          </UButton>
        </div>

        <div v-else-if="state === 'error'" class="space-y-4 text-center">
          <UIcon
            name="i-heroicons-exclamation-triangle"
            class="w-10 h-10 text-error-500 mx-auto"
          />
          <p class="text-gray-700 dark:text-gray-300">
            Something went wrong loading this link. Please refresh the page to
            try again.
          </p>
        </div>

        <!-- Approve -->
        <div v-else-if="state === 'ready' && link" class="space-y-5">
          <p class="text-gray-700 dark:text-gray-300">
            Link the Discord account
            <strong data-testid="discord-username">{{
              link.discordUsername
            }}</strong>
            to your VEP account
            <strong v-if="account?.rulerName">
              {{ account.rulerName }} of {{ account.nationName }}</strong
            >?
          </p>

          <ul
            class="list-disc pl-5 space-y-1 text-sm text-gray-600 dark:text-gray-400"
          >
            <li>
              VEP will ping this Discord account about your deals, unless you
              turn Discord notifications off in your VEP settings.
            </li>
            <li>
              Whoever uses this Discord account can get your unique code with
              the bot's <strong>/uniquecode</strong> command.
            </li>
          </ul>

          <UAlert
            v-if="replacesDiscordAccount"
            color="warning"
            variant="subtle"
            icon="i-heroicons-exclamation-triangle"
            title="This replaces your linked Discord account"
            :description="`Your account is linked to ${account?.discord || 'another Discord account'} now.`"
          />

          <UAlert
            color="error"
            variant="subtle"
            icon="i-heroicons-shield-exclamation"
            description="Only approve this if you ran /register in Discord yourself, and this is your Discord account. If someone sent you this link, don't approve it."
          />

          <div class="flex gap-3">
            <UButton
              :to="accountPath"
              color="neutral"
              variant="outline"
              block
              class="flex-1"
            >
              Cancel
            </UButton>
            <UButton
              color="primary"
              block
              class="flex-1"
              :loading="isLinking"
              @click="approve()"
            >
              Authorize
            </UButton>
          </div>
        </div>

        <!-- Done -->
        <div
          v-else-if="state === 'linked' && link"
          class="space-y-4 text-center"
        >
          <UIcon
            name="i-heroicons-check-circle"
            class="w-10 h-10 text-success-500 mx-auto"
          />
          <p class="text-gray-700 dark:text-gray-300">
            Your Discord account <strong>{{ link.discordUsername }}</strong> is
            linked to your VEP account. You can close this window now.
          </p>
          <UButton :to="accountPath" color="primary" block>
            Go to my account
          </UButton>
        </div>
      </UCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import {
  useAccountStore,
  type Account,
  type DiscordLinkDetails,
} from '~/stores/account'
import { useAuthStore } from '~/stores/auth'

definePageMeta({
  layout: 'full-screen',
})

type LinkState = 'loading' | 'invalid' | 'error' | 'ready' | 'linked'

const route = useRoute()
const toast = useToast()
const accountStore = useAccountStore()
const authStore = useAuthStore()
const { isAuthenticated, loggedInUser } = storeToRefs(authStore)

const token = computed<string>(() =>
  typeof route.query.token === 'string' ? route.query.token : '',
)
const accountPath = computed<string>(() =>
  loggedInUser.value ? `/account/${loggedInUser.value.accountId}` : '/',
)

const state = ref<LinkState>('loading')
const link = ref<DiscordLinkDetails | null>(null)
const account = ref<Account | null>(null)
const isLinking = ref<boolean>(false)

// Discord IDs can come back from the API as a BigInt, so compare them as strings
const replacesDiscordAccount = computed<boolean>(
  () =>
    !!account.value?.discordUniqueId &&
    !!link.value &&
    String(account.value.discordUniqueId) !== link.value.discordId,
)

const isInvalidLink = (error: unknown) =>
  (error as { statusCode?: number } | null)?.statusCode === 400

onMounted(async () => {
  if (!token.value || !isAuthenticated.value || !loggedInUser.value) return

  try {
    const [details] = await Promise.all([
      accountStore.previewDiscordLink(token.value),
      accountStore
        .loadAccount(loggedInUser.value.accountId)
        .then(() => (account.value = accountStore.account))
        // The account is only shown for context, so the page still works without it
        .catch(() => (account.value = null)),
    ])
    link.value = details
    state.value = 'ready'
  } catch (error) {
    state.value = isInvalidLink(error) ? 'invalid' : 'error'
  }
})

async function approve() {
  isLinking.value = true
  try {
    link.value = await accountStore.linkDiscordAccount(token.value)
    state.value = 'linked'
  } catch (error) {
    if (isInvalidLink(error)) {
      state.value = 'invalid'
      return
    }
    toast.add({
      color: 'error',
      title: 'Error',
      description: ERROR_MESSAGES.default,
      duration: 5000,
    })
  } finally {
    isLinking.value = false
  }
}
</script>
