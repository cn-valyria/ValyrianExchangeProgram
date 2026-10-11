export default defineNuxtPlugin(async () => {
  const authStore = useAuthStore()

  // Initialize auth store from session storage
  await authStore.init()
})
