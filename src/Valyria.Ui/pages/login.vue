<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8">
      <!-- Logo and Header -->
      <div class="text-center">
        <NuxtLink to="/">
          <img src="~/assets/img/logo.png" class="h-16 mx-auto" alt="VEP Logo" />
        </NuxtLink>
        <h2 class="mt-6 text-3xl font-extrabold text-gray-900 dark:text-white">
          VEP Login Portal
        </h2>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Sign in to access your account
        </p>
      </div>

      <!-- Login Form -->
      <UCard class="mt-8">
        <UForm :schema="schema" :state="state" class="space-y-6" @submit="onSubmit">
          <!-- Username field (for password managers) -->
          <!-- This field will accept either Nation ID or Ruler Name -->
          <UFormField label="Nation ID or Ruler Name" name="username" required>
            <UInput
              v-model="state.username"
              type="text"
              name="username"
              autocomplete="username"
              placeholder="Enter your Nation ID or Ruler Name"
              autofocus
              class="w-full"
            />
          </UFormField>

          <!-- Unique Code (password equivalent) -->
          <UFormField label="Unique Code" name="uniqueCode" required>
            <UInput
              v-model="state.uniqueCode"
              type="password"
              name="password"
              autocomplete="current-password"
              placeholder="Enter your unique code"
              class="w-full"
            />
          </UFormField>

          <!-- Helper text -->
          <div class="text-sm text-gray-500 dark:text-gray-400">
            <p>You can use either your <strong>Nation ID</strong> (numeric) or your <strong>Ruler Name</strong> to log in.</p>
          </div>

          <!-- Submit Button -->
          <UButton type="submit" color="primary" block size="lg" :loading="isLoading">
            Log In
          </UButton>
        </UForm>
      </UCard>

      <!-- Back to Home Link -->
      <div class="text-center">
        <NuxtLink to="/" class="text-sm text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white">
          Back to Home
        </NuxtLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { z } from 'zod'

definePageMeta({
  layout: 'full-screen'
})

const authStore = useAuthStore()
const toast = useToast()
const route = useRoute()
const router = useRouter()
const isLoading = ref(false)

// Define the form state with a single username field
const state = reactive({
  username: '',
  uniqueCode: ''
})

// Define the Zod schema
const schema = z.object({
  username: z.string().min(1, 'Nation ID or Ruler Name is required'),
  uniqueCode: z.string().min(1, 'Unique Code is required')
})

onMounted(() => {
  // Check if already authenticated and redirect
  if (authStore.isAuthenticated) {
    router.push('/')
  } else if (route.query.auth === 'failed') {
    toast.add({
      title: 'Authentication Required',
      description:
        "You can't access that page until you're logged in. Please log into your account, or contact VEP management to create one for you.",
      color: 'error',
    })
  } else if (route.query.auth === 'logout') {
    toast.add({
      title: 'Logout Successful',
      description: "You've successfully signed out of your VEP account.",
      color: 'success'
    })
  }
})

async function onSubmit() {
  isLoading.value = true

  try {
    // Determine if the username is a Nation ID (numeric) or Ruler Name
    const isNumeric = /^\d+$/.test(state.username.trim())

    await authStore.login({
      nationId: isNumeric ? state.username.trim() : undefined,
      rulerName: !isNumeric ? state.username.trim() : undefined,
      uniqueCode: state.uniqueCode,
    })

    toast.add({
      title: 'Login Successful',
      description: 'Welcome back!',
      color: 'success'
    })

    // Redirect to home or intended page
    const redirectTo = router.currentRoute.value.query.redirect as string || '/'
    router.push(redirectTo)
  } catch (error) {
    console.error('Login failed:', error)
    toast.add({
      title: 'Login Failed',
      description: 'Invalid credentials. Please try again.',
      color: 'error'
    })
  } finally {
    isLoading.value = false
  }
}
</script>
