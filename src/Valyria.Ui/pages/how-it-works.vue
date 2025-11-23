<template>
  <div class="min-h-screen bg-white dark:bg-gray-900">
    <!-- Hero Section -->
    <section class="relative overflow-hidden bg-gradient-to-br from-primary-600 via-primary-700 to-purple-700">
      <div class="absolute inset-0 bg-grid-white/10"></div>
      <div class="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-20 sm:py-24">
        <div class="text-center space-y-6">
          <h1 class="text-4xl sm:text-5xl lg:text-6xl font-bold text-white tracking-tight">
            How VEP Works
          </h1>
          <p class="text-lg sm:text-xl text-primary-100 max-w-3xl mx-auto">
            Sending and receiving aid made simple, efficient, and dare we say—a little bit fun
          </p>
        </div>
      </div>
    </section>

    <!-- Main Content -->
    <div class="max-w-5xl mx-auto px-4 sm:px-6 lg:px-8 py-16 space-y-16">

      <!-- Getting Started Section -->
      <section class="space-y-8">
        <div class="text-center space-y-4">
          <h2 class="text-3xl sm:text-4xl font-bold text-gray-900 dark:text-white">
            Getting Started
          </h2>
          <p class="text-lg text-gray-600 dark:text-gray-300 max-w-3xl mx-auto">
            Follow these simple steps to join the Valyrian Exchange Program and start managing your aid efficiently
          </p>
        </div>

        <!-- Step 1: Login -->
        <UCard>
          <div class="space-y-6">
            <div class="flex items-start gap-4">
              <div class="flex-shrink-0">
                <div class="h-12 w-12 rounded-full bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <span class="text-xl font-bold text-primary-600 dark:text-primary-400">1</span>
                </div>
              </div>
              <div class="flex-1 space-y-3">
                <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
                  Log In to VEP
                </h3>
                <p class="text-gray-600 dark:text-gray-300">
                  Click the <strong>Login</strong> button in the top right corner of the page. You'll need your Cybernations
                  Nation ID to authenticate.
                </p>
                <div class="bg-gray-50 dark:bg-gray-800 rounded-lg p-4 space-y-2">
                  <p class="text-sm font-semibold text-gray-700 dark:text-gray-300">How to find your Nation ID:</p>
                  <ol class="text-sm text-gray-600 dark:text-gray-400 list-decimal list-inside space-y-1 ml-2">
                    <li>Click the "View My Nation" link in the Cybernations navigation menu</li>
                    <li>Copy the Nation ID from the end of the URL in your browser</li>
                    <li>Example: <code class="text-xs bg-gray-200 dark:bg-gray-700 px-2 py-1 rounded">https://www.cybernations.net/nation_drill_display.asp?Nation_ID=299962</code></li>
                  </ol>
                </div>

                <!-- Placeholder for screenshot -->
                <div class="bg-gray-100 dark:bg-gray-800 rounded-lg border-2 border-dashed border-gray-300 dark:border-gray-700 p-8 flex items-center justify-center">
                  <div class="text-center">
                    <Icon name="heroicons:photo" class="h-16 w-16 text-gray-400 mx-auto mb-2" />
                    <p class="text-sm text-gray-500 dark:text-gray-400">Screenshot placeholder: Login page</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </UCard>

        <!-- Step 2: Save Authentication -->
        <UCard>
          <div class="space-y-6">
            <div class="flex items-start gap-4">
              <div class="flex-shrink-0">
                <div class="h-12 w-12 rounded-full bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <span class="text-xl font-bold text-primary-600 dark:text-primary-400">2</span>
                </div>
              </div>
              <div class="flex-1 space-y-3">
                <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
                  Save Your Authentication Code
                </h3>
                <p class="text-gray-600 dark:text-gray-300">
                  You will receive a unique authentication code via in-game message. Save this information in a secure location,
                  as the system will occasionally require re-authentication.
                </p>
                <UAlert
                  color="primary"
                  variant="subtle"
                  title="Pro Tip"
                  description="We recommend storing your authentication code in the Cybernations in-game notepad for easy access."
                  icon="i-heroicons-light-bulb"
                />

                <!-- Placeholder for screenshot -->
                <div class="bg-gray-100 dark:bg-gray-800 rounded-lg border-2 border-dashed border-gray-300 dark:border-gray-700 p-8 flex items-center justify-center">
                  <div class="text-center">
                    <Icon name="heroicons:photo" class="h-16 w-16 text-gray-400 mx-auto mb-2" />
                    <p class="text-sm text-gray-500 dark:text-gray-400">Screenshot placeholder: Authentication code message</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </UCard>

        <!-- Step 3: Account Page -->
        <UCard>
          <div class="space-y-6">
            <div class="flex items-start gap-4">
              <div class="flex-shrink-0">
                <div class="h-12 w-12 rounded-full bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <span class="text-xl font-bold text-primary-600 dark:text-primary-400">3</span>
                </div>
              </div>
              <div class="flex-1 space-y-3">
                <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
                  Access Your Account Dashboard
                </h3>
                <p class="text-gray-600 dark:text-gray-300">
                  Once authenticated, you'll have access to your personal account page where you can track your balance in real-time.
                </p>
                <div class="bg-gray-50 dark:bg-gray-800 rounded-lg p-4 space-y-2">
                  <p class="text-sm font-semibold text-gray-700 dark:text-gray-300">Your balance reflects:</p>
                  <ul class="text-sm text-gray-600 dark:text-gray-400 list-disc list-inside space-y-1 ml-2">
                    <li>Total aid you've sent</li>
                    <li>Total aid you've received</li>
                    <li>Current credit or debt status</li>
                    <li>During wartime: special status indicators based on your position in the system</li>
                  </ul>
                </div>

                <!-- Placeholder for screenshot -->
                <div class="bg-gray-100 dark:bg-gray-800 rounded-lg border-2 border-dashed border-gray-300 dark:border-gray-700 p-8 flex items-center justify-center">
                  <div class="text-center">
                    <Icon name="heroicons:photo" class="h-16 w-16 text-gray-400 mx-auto mb-2" />
                    <p class="text-sm text-gray-500 dark:text-gray-400">Screenshot placeholder: Account dashboard</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </UCard>
      </section>

      <UDivider />

      <!-- Sending Aid Section -->
      <section class="space-y-8">
        <div class="text-center space-y-4">
          <h2 class="text-3xl sm:text-4xl font-bold text-gray-900 dark:text-white">
            Sending Aid
          </h2>
          <p class="text-lg text-gray-600 dark:text-gray-300 max-w-3xl mx-auto">
            When you're expected to send aid, VEP makes the process straightforward and organized
          </p>
        </div>

        <div class="grid gap-8 md:grid-cols-2">
          <!-- Aid Target List -->
          <UCard>
            <div class="space-y-4">
              <div class="flex items-center gap-3">
                <div class="h-10 w-10 rounded-lg bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <Icon name="heroicons:list-bullet" class="h-5 w-5 text-primary-600 dark:text-primary-400" />
                </div>
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                  Target List
                </h3>
              </div>
              <p class="text-gray-600 dark:text-gray-300">
                You'll receive a prioritized list of aid target nations with direct links to send aid. The list automatically
                updates daily, ensuring you always have current targets.
              </p>
              <UAlert
                color="warning"
                variant="subtle"
                title="Important"
                description="Always work through the list from top to bottom. This ensures prompt turnover and keeps the system running smoothly for everyone."
                icon="i-heroicons-exclamation-triangle"
              />
            </div>
          </UCard>

          <!-- Wartime Considerations -->
          <UCard>
            <div class="space-y-4">
              <div class="flex items-center gap-3">
                <div class="h-10 w-10 rounded-lg bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                  <Icon name="heroicons:shield-exclamation" class="h-5 w-5 text-primary-600 dark:text-primary-400" />
                </div>
                <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                  Wartime Protocol
                </h3>
              </div>
              <p class="text-gray-600 dark:text-gray-300">
                During times of war, your alliance leadership may restrict aid to certain alliances due to treaty obligations
                or political considerations.
              </p>
              <div class="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-3">
                <p class="text-sm text-yellow-800 dark:text-yellow-200">
                  VEP does not currently filter targets based on wartime restrictions. Please coordinate with your alliance
                  leadership to identify and avoid any inappropriate aid targets during conflicts.
                </p>
              </div>
            </div>
          </UCard>
        </div>

        <!-- Placeholder for aid targets screenshot -->
        <div class="bg-gray-100 dark:bg-gray-800 rounded-lg border-2 border-dashed border-gray-300 dark:border-gray-700 p-12 flex items-center justify-center">
          <div class="text-center">
            <Icon name="heroicons:photo" class="h-20 w-20 text-gray-400 mx-auto mb-3" />
            <p class="text-sm text-gray-500 dark:text-gray-400">Screenshot placeholder: Aid targets list</p>
          </div>
        </div>
      </section>

      <UDivider />

      <!-- Notifications Section -->
      <section class="space-y-6">
        <div class="text-center space-y-4">
          <h2 class="text-3xl sm:text-4xl font-bold text-gray-900 dark:text-white">
            Stay Informed
          </h2>
          <p class="text-lg text-gray-600 dark:text-gray-300 max-w-3xl mx-auto">
            VEP keeps you updated with convenient reminders
          </p>
        </div>

        <UCard>
          <div class="flex items-start gap-4">
            <div class="flex-shrink-0">
              <div class="h-12 w-12 rounded-lg bg-primary-100 dark:bg-primary-900 flex items-center justify-center">
                <Icon name="heroicons:bell" class="h-6 w-6 text-primary-600 dark:text-primary-400" />
              </div>
            </div>
            <div class="flex-1 space-y-3">
              <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
                Automated Reminders
              </h3>
              <p class="text-gray-600 dark:text-gray-300">
                Receive regular notifications via in-game messages and Discord when your aid slots open up. These reminders
                are provided as a convenience to help you stay on top of your aid schedule.
              </p>
              <UAlert
                color="info"
                variant="subtle"
                title="Source of Truth"
                description="While notifications are helpful, the VEP website is always the authoritative source for your account status and aid targets."
                icon="i-heroicons-information-circle"
              />
            </div>
          </div>
        </UCard>
      </section>

      <UDivider />

      <!-- Support Section -->
      <section class="space-y-6">
        <div class="text-center space-y-4">
          <h2 class="text-3xl sm:text-4xl font-bold text-gray-900 dark:text-white">
            Need Help?
          </h2>
        </div>

        <UCard class="bg-gradient-to-br from-primary-50 to-purple-50 dark:from-primary-950 dark:to-purple-950">
          <div class="text-center space-y-4">
            <Icon name="heroicons:chat-bubble-left-right" class="h-16 w-16 text-primary-600 dark:text-primary-400 mx-auto" />
            <h3 class="text-xl font-semibold text-gray-900 dark:text-white">
              Join Our Community
            </h3>
            <p class="text-gray-600 dark:text-gray-300 max-w-2xl mx-auto">
              If you need support or have questions, visit the VEP members channel on the FTW Discord server.
              Our community is here to help you succeed!
            </p>
          </div>
        </UCard>
      </section>

      <!-- Simple Summary -->
      <section class="bg-gradient-to-r from-primary-600 to-purple-600 rounded-2xl p-8 sm:p-12 text-center">
        <h2 class="text-2xl sm:text-3xl font-bold text-white mb-4">
          Send Aid. Track Balance. Succeed.
        </h2>
        <p class="text-lg text-primary-100 max-w-2xl mx-auto">
          It's that easy! Welcome to the Valyrian Exchange Program.
        </p>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({
  auth: true,
})
</script>
