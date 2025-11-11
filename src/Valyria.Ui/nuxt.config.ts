// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-11-10',

  // Disable SSR for SPA mode
  ssr: false,

  devtools: { enabled: true },

  modules: [
    '@pinia/nuxt',
    '@pinia-plugin-persistedstate/nuxt',
    '@primevue/nuxt-module',
    '@vueuse/nuxt',
    '@nuxt/icon',
  ],

  // PrimeVue configuration
  primevue: {
    options: {
      ripple: true,
      inputVariant: 'filled',
    },
    components: {
      include: [
        'Button',
        'InputText',
        'Password',
        'DataTable',
        'Column',
        'Dialog',
        'Toast',
        'ConfirmDialog',
        'Dropdown',
        'Calendar',
        'InputNumber',
        'Textarea',
        'Checkbox',
        'Tag',
        'Card',
        'Chart',
        'Divider',
        'Menu',
        'ProgressBar',
        'Toolbar',
      ],
    },
  },

  // Global CSS
  css: [
    '@/assets/css/main.scss',
    'primeicons/primeicons.css',
  ],

  // App configuration
  app: {
    head: {
      title: 'Valyrian Exchange Program',
      htmlAttrs: {
        lang: 'en',
      },
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        { name: 'description', content: 'Valyrian Exchange Program' },
        { name: 'format-detection', content: 'telephone=no' },
      ],
      link: [{ rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' }],
      script: [
        {
          src: 'https://unpkg.com/ionicons@5.5.2/dist/ionicons/ionicons.js',
          body: true,
          tagPosition: 'bodyClose'
        }
      ],
    },
  },

  // Runtime config for environment variables
  runtimeConfig: {
    public: {
      apiUrl: process.env.NUXT_PUBLIC_API_URL || 'http://localhost:5000',
    },
  },

  // Vue configuration
  vue: {
    compilerOptions: {
      isCustomElement: (tag) => tag.startsWith('ion-'),
    },
  },

  // Vite configuration
  vite: {
    css: {
      preprocessorOptions: {
        scss: {
          api: 'modern-compiler',
        },
      },
    },
  },

  // TypeScript configuration
  typescript: {
    strict: false,
    typeCheck: false,
  },
})
