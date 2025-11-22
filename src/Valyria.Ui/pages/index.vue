<template>
  <div class="min-h-screen bg-white dark:bg-gray-900">
    <!-- Hero Section -->
    <section class="relative overflow-hidden bg-gradient-to-br from-primary-600 via-primary-700 to-purple-700">
      <div class="absolute inset-0 bg-grid-white/10"></div>
      <div class="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-24 sm:py-32">
        <div class="text-center space-y-8">
          <h1 class="text-5xl sm:text-6xl lg:text-7xl font-bold text-white tracking-tight">
            Valyrian Exchange Program
          </h1>
          <p class="text-xl sm:text-2xl text-primary-100 max-w-2xl mx-auto">
            Fully managed CN tech dealing
          </p>
        </div>
      </div>
    </section>

    <!-- Dragons Illustration -->
    <div class="relative -mt-16 flex justify-center px-4">
      <img src="~/assets/img/dragons-illustration.png" alt="Dragons" class="w-80 sm:w-96 drop-shadow-2xl" />
    </div>

    <!-- Main Content -->
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-16 space-y-24">
      <!-- Balance Management Section -->
      <section class="space-y-8">
        <h2 class="text-3xl sm:text-4xl font-bold text-center text-gray-900 dark:text-white">
          An account that always pays its debts
        </h2>

        <div class="grid grid-cols-1 lg:grid-cols-5 gap-8 items-center">
          <div class="lg:col-span-3">
            <UCard class="overflow-hidden">
              <img src="~/assets/img/account-example.png" alt="Account Example" class="w-full rounded-lg" />
            </UCard>
          </div>

          <div class="lg:col-span-2 space-y-4">
            <h3 class="text-2xl font-semibold text-gray-900 dark:text-white">
              Manage your own balance instead of individual deals
            </h3>
            <div class="prose dark:prose-invert text-gray-600 dark:text-gray-300 space-y-4">
              <p>
                If you've played CN before, you're probably familiar with the concept of a "tech deal": a buyer sends
                cash to a seller in exchange for technology. They can be great for new players and ancient nations
                alike, but they typically require a lot of research, maintenance, and tracking in order to maintain
                healthy deals long-term.
              </p>
              <p>
                The Valyrian Exchange Program is a new paradigm in tech dealing. Instead of managing individual deals,
                you are given a simple ledger - known as your balance - that tracks how much aid you've sent, how much
                you're owed, and how much you're due to send. VEP enrolls well over one hundred nations, with an even
                split of buyers and sellers, so you're guaranteed a consistent source of incoming aid and outgoing
                targets. Never worry about 1x1 deals again; get signed up in VEP today!
              </p>
            </div>
          </div>
        </div>
      </section>

      <!-- Features Section -->
      <section class="space-y-8">
        <h2 class="text-3xl sm:text-4xl font-bold text-center text-gray-900 dark:text-white">
          Features that you can't get in other programs
        </h2>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-8 lg:gap-12">
          <!-- Feature 1 -->
          <UCard>
            <div class="flex gap-4">
              <div class="flex-shrink-0">
                <div class="h-12 w-12 rounded-lg bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <Icon name="heroicons:sparkles" class="h-6 w-6 text-primary-600 dark:text-primary-400" />
                </div>
              </div>
              <div class="space-y-2">
                <h5 class="text-lg font-semibold text-gray-900 dark:text-white">Lovely Website</h5>
                <p class="text-gray-600 dark:text-gray-300">
                  Let's be honest: CN isn't known for being a very attractive app. It doesn't have to be that
                  way for a wonderful tool such as VEP, though! And VEP isn't just pretty; it is thoughtfully-
                  crafted to be simple and easy to understand.
                </p>
              </div>
            </div>
          </UCard>

          <!-- Feature 2 -->
          <UCard>
            <div class="flex gap-4">
              <div class="flex-shrink-0">
                <div class="h-12 w-12 rounded-lg bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <Icon name="heroicons:bolt" class="h-6 w-6 text-primary-600 dark:text-primary-400" />
                </div>
              </div>
              <div class="space-y-2">
                <h5 class="text-lg font-semibold text-gray-900 dark:text-white">Constant Aid</h5>
                <p class="text-gray-600 dark:text-gray-300">
                  In VEP, it's assumed that if you have an open aid slot, then you'd like to either receive aid or
                  send more out. We want to make sure that you're using your aid slots to their max capacity!
                </p>
              </div>
            </div>
          </UCard>

          <!-- Feature 3 -->
          <UCard>
            <div class="flex gap-4">
              <div class="flex-shrink-0">
                <div class="h-12 w-12 rounded-lg bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <Icon name="heroicons:banknotes" class="h-6 w-6 text-primary-600 dark:text-primary-400" />
                </div>
              </div>
              <div class="space-y-2">
                <h5 class="text-lg font-semibold text-gray-900 dark:text-white">Fair Rates</h5>
                <p class="text-gray-600 dark:text-gray-300">
                  9/100: $9 million in cash for 100 technology levels. That's been VEP's rate since it was founded,
                  and it's the fixed rate for VEP's future.
                </p>
              </div>
            </div>
          </UCard>

          <!-- Feature 4 -->
          <UCard>
            <div class="flex gap-4">
              <div class="flex-shrink-0">
                <div class="h-12 w-12 rounded-lg bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <Icon name="heroicons:cog-6-tooth" class="h-6 w-6 text-primary-600 dark:text-primary-400" />
                </div>
              </div>
              <div class="space-y-2">
                <h5 class="text-lg font-semibold text-gray-900 dark:text-white">Automated Transactions</h5>
                <p class="text-gray-600 dark:text-gray-300">
                  All aid is imported and tallied against your balance every morning, in CN time, so you'll never
                  need to track aid screens again.
                </p>
              </div>
            </div>
          </UCard>
        </div>

        <p class="text-center text-xl text-gray-700 dark:text-gray-300 font-medium mt-12">
          ...and so much more!
        </p>
      </section>
    </div>

    <!-- CTA Section -->
    <section class="bg-gradient-to-r from-primary-600 to-purple-600">
      <div class="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-16 text-center space-y-6">
        <h2 class="text-3xl sm:text-4xl font-bold text-white">
          Start managing your account now!
        </h2>
        <p class="text-xl text-primary-100 max-w-2xl mx-auto">
          If you already have an account, dig into our additional resources to learn more about the nuts and bolts of
          how VEP works
        </p>
        <UButton size="xl" color="neutral" variant="solid" label="How VEP Works" class="mt-4" />
      </div>
    </section>

    <!-- Footer -->
    <footer class="bg-gray-50 dark:bg-gray-950 border-t border-gray-200 dark:border-gray-800">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <p class="text-center text-gray-600 dark:text-gray-400">
          Built with <span class="text-red-500">❤️</span> by lilweirdward and dev0win. Founded by Lord Draculea.
        </p>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  auth: false,
})

// Check for auth failure on mount
onMounted(() => {
})
</script>
