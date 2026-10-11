import { defineVitestConfig } from '@nuxt/test-utils/config'

export default defineVitestConfig({
  test: {
    // Boots a Nuxt app per test file, so components get Nuxt UI, Pinia and auto-imports
    environment: 'nuxt',
    setupFiles: ['./tests/setup.ts'],
    environmentOptions: {
      nuxt: {
        domEnvironment: 'happy-dom',
      },
    },
    coverage: {
      // v8 can't map coverage back to pages through Nuxt's page transform, so pages showed as 0 lines
      provider: 'istanbul',
      reporter: ['text', 'html', 'json-summary'],
      include: [
        'components/**/*.vue',
        'composables/**/*.ts',
        'infrastructure/**/*.ts',
        'layouts/**/*.vue',
        'middleware/**/*.ts',
        'pages/**/*.vue',
        'plugins/**/*.ts',
        'stores/**/*.ts',
      ],
    },
  },
})
