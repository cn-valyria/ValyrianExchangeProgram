import { afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import type { Pinia } from 'pinia'

// Unmount everything a test mounted, so teleported modals don't leak into the next test
enableAutoUnmount(afterEach)

afterEach(async () => {
  await flushPromises()

  // The Nuxt app (and its Pinia) is shared by every test in a file, so put each store back to its initial state
  const pinia = useNuxtApp().$pinia as Pinia & {
    _s: Map<string, { $reset: () => void }>
  }
  pinia._s.forEach((store) => store.$reset())

  useToast().clear()
  localStorage.clear()
  vi.clearAllMocks()
})
