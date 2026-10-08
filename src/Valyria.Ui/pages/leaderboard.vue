<template>
  <div class="min-h-screen bg-gradient-to-br from-primary-600 via-primary-700 to-purple-700">
    <!-- Hero Section -->
    <section class="relative overflow-hidden">
      <div class="absolute inset-0 bg-grid-white/10"></div>
      <div class="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
        <div class="text-center space-y-4">
          <div class="flex justify-center items-center gap-3">
            <Icon name="heroicons:trophy" class="h-12 w-12 text-yellow-300" />
            <h1 class="text-5xl sm:text-6xl font-bold text-white tracking-tight">
              Activity Score Leaderboard
            </h1>
          </div>
          <p class="text-xl text-primary-100 max-w-2xl mx-auto">
            The top 10 VEP accounts ranked by fastest debt clearance
          </p>
        </div>
      </div>
    </section>

    <!-- Main Content -->
    <div class="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 pb-16">
      <!-- Top 3 Podium -->
      <div v-if="!loading && topScorers.length >= 3" class="mb-12">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6 items-end">
          <!-- 2nd Place -->
          <div class="md:order-1">
            <LeaderboardPodiumCard
              :account="topScorers[1]"
              rank="2"
              medal-color="silver"
            />
          </div>

          <!-- 1st Place (Taller) -->
          <div class="md:order-2 transform md:scale-110 md:-mt-8">
            <LeaderboardPodiumCard
              :account="topScorers[0]"
              rank="1"
              medal-color="gold"
              :is-champion="true"
            />
          </div>

          <!-- 3rd Place -->
          <div class="md:order-3">
            <LeaderboardPodiumCard
              :account="topScorers[2]"
              rank="3"
              medal-color="bronze"
            />
          </div>
        </div>
      </div>

      <!-- Remaining Rankings (4-10) -->
      <div v-if="!loading && topScorers.length > 3" class="space-y-3">
        <h2 class="text-2xl font-bold text-white text-center mb-6">
          Hall of Fame
        </h2>
        <LeaderboardRankCard
          v-for="account in remainingRanks"
          :key="account.id"
          :account="account"
        />
      </div>

      <!-- Loading State -->
      <div v-if="loading" class="flex justify-center items-center py-20">
        <div class="text-center space-y-4">
          <Icon name="heroicons:arrow-path" class="h-12 w-12 text-white animate-spin mx-auto" />
          <p class="text-white text-lg">Loading leaderboard...</p>
        </div>
      </div>

      <!-- Empty State -->
      <div v-if="!loading && topScorers.length === 0" class="text-center py-20">
        <UCard>
          <div class="space-y-4">
            <Icon name="heroicons:users" class="h-12 w-12 text-gray-400 mx-auto" />
            <p class="text-gray-900 dark:text-white text-lg font-semibold">
              No rankings available yet
            </p>
            <p class="text-gray-600 dark:text-gray-400">
              Check back soon to see the top performers!
            </p>
          </div>
        </UCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { TopScorerAccount } from '~/stores/shared/topScorerAccount'

// Hidden until there's a deal-based ranking to replace the VEP 2.5 Activity Score
definePageMeta({
  middleware: () => navigateTo('/')
})

const config = useRuntimeConfig()
const toast = useToast()

const authStore = useAuthStore()

const loading = ref(true)
const topScorers = ref<TopScorerAccount[]>([])

const remainingRanks = computed(() => topScorers.value.slice(3))

const fetchTopScorers = async () => {
  loading.value = true

  const { data, error } = await useFetch(
    `${config.public.apiUrl}/api/accounts/topScorers`,
    {
      headers: {
        Authorization: `Bearer ${authStore.accessToken}`,
      },
    }
  )

  if (error.value) {
    console.error('Failed to fetch top scorers:', error)
    toast.add({
      color: 'error',
      title: 'Error',
      description: "VEP encountered an error while setting up the leaderboard. Please try again, or contact @lilweirdward if the error persists.",
      duration: 3000
    })
  }

  if (data.value) {
    topScorers.value = data.value as TopScorerAccount[]
  }
  
  loading.value = false
}

onMounted(() => {
  fetchTopScorers()
})
</script>
