<template>
  <div
    class="min-h-screen bg-gradient-to-br from-primary-600 via-primary-700 to-purple-700"
  >
    <!-- Hero Section -->
    <section class="relative overflow-hidden">
      <div class="absolute inset-0 bg-grid-white/10"></div>
      <div class="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
        <div class="text-center space-y-4">
          <div class="flex justify-center items-center gap-3">
            <Icon name="heroicons:trophy" class="h-12 w-12 text-yellow-300" />
            <h1
              class="text-5xl sm:text-6xl font-bold text-white tracking-tight"
            >
              Deal Partner Leaderboard
            </h1>
          </div>
          <p class="text-xl text-primary-100 max-w-2xl mx-auto">
            The VEP accounts with the fewest flags per outgoing leg over the
            last {{ leaderboard?.windowDays ?? 90 }} days
          </p>
          <p
            v-if="leaderboard"
            class="text-sm text-primary-200 max-w-2xl mx-auto"
          >
            A flag means aid wasn't sent on the day its slot opened, or went
            somewhere other than your partner. Accounts need
            {{ leaderboard.minimumOutgoingLegs }} outgoing legs since
            {{ leaderboard.windowStart.split('T')[0] }} to be ranked.
          </p>
        </div>
      </div>
    </section>

    <!-- Main Content -->
    <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 pb-16">
      <div v-if="!loading && leaderboard?.mine" class="mb-12">
        <LeaderboardYourStanding
          :leaderboard="leaderboard"
          :standing="leaderboard.mine"
        />
      </div>

      <!-- Top 3 Podium -->
      <div v-if="!loading && podium.length === 3" class="mb-12">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6 items-end">
          <!-- 2nd Place -->
          <div class="md:order-1">
            <LeaderboardPodiumCard :account="podium[1]" medal-color="silver" />
          </div>

          <!-- 1st Place (Taller) -->
          <div class="md:order-2 transform md:scale-110 md:-mt-8">
            <LeaderboardPodiumCard
              :account="podium[0]"
              medal-color="gold"
              :is-champion="true"
            />
          </div>

          <!-- 3rd Place -->
          <div class="md:order-3">
            <LeaderboardPodiumCard :account="podium[2]" medal-color="bronze" />
          </div>
        </div>
      </div>

      <!-- Remaining Rankings (4-10) -->
      <div v-if="!loading && remainingRanks.length > 0" class="space-y-3">
        <h2 class="text-2xl font-bold text-white text-center mb-6">
          Hall of Fame
        </h2>
        <LeaderboardRankCard
          v-for="account in remainingRanks"
          :key="account.accountId"
          :account="account"
        />
      </div>

      <!-- Loading State -->
      <div v-if="loading" class="flex justify-center items-center py-20">
        <div class="text-center space-y-4">
          <Icon
            name="heroicons:arrow-path"
            class="h-12 w-12 text-white animate-spin mx-auto"
          />
          <p class="text-white text-lg">Loading leaderboard...</p>
        </div>
      </div>

      <!-- Empty State -->
      <div v-if="!loading && top.length === 0" class="text-center py-20">
        <UCard>
          <div class="space-y-4">
            <Icon
              name="heroicons:users"
              class="h-12 w-12 text-gray-400 mx-auto"
            />
            <p class="text-gray-900 dark:text-white text-lg font-semibold">
              No rankings available yet
            </p>
            <p class="text-gray-600 dark:text-gray-400">
              No one has sent enough outgoing legs in the last
              {{ leaderboard?.windowDays ?? 90 }} days to be ranked yet.
            </p>
          </div>
        </UCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ERROR_MESSAGES } from '~/infrastructure/constants'
import { useLeaderboardStore } from '~/stores/leaderboard'

const toast = useToast()
const leaderboardStore = useLeaderboardStore()

const loading = ref(true)

const leaderboard = computed(() => leaderboardStore.leaderboard)
const top = computed(() => leaderboard.value?.top ?? [])

// The podium is for the top 3 when there are at least 3 ranked accounts; otherwise everyone gets a rank card
const podium = computed(() =>
  top.value.length >= 3 ? top.value.slice(0, 3) : [],
)
const remainingRanks = computed(() =>
  top.value.length >= 3 ? top.value.slice(3) : top.value,
)

onMounted(async () => {
  try {
    await leaderboardStore.loadLeaderboard()
  } catch {
    toast.add({
      color: 'error',
      title: 'Error',
      description: ERROR_MESSAGES.default,
      duration: 3000,
    })
  } finally {
    loading.value = false
  }
})
</script>
