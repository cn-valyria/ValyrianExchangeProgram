// ESLint 9 flat config, replacing .eslintrc.cjs (which ESLint 9 no longer reads)
import { createConfigForNuxt } from '@nuxt/eslint-config/flat'
import prettier from 'eslint-config-prettier'

export default createConfigForNuxt()
  .prepend({
    ignores: ['.nuxt/**', '.output/**', 'dist/**', 'coverage/**', 'static/**'],
  })
  // Leaves formatting to Prettier
  .append(prettier)
  .append({
    rules: {
      'vue/multi-word-component-names': 'off',
      'vue/no-v-html': 'off',
    },
  })
