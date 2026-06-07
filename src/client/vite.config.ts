import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// We're pinned to Vite 7 (Rollup-based bundler) rather than Vite 8 because
// the Rolldown CJS-to-ESM transform in Vite 8 mishandles modules with
// top-level `const X = require("...")` declarations - it emits broken
// output of the form `var x = r(e => { var t = t(), n = n(), ... })`
// where each local var and its initialiser get renamed to the same
// identifier, producing `var t = t()` which throws "t is not a function"
// at module load.
//
// Both recharts 3.x and mermaid 11.x depend on es-toolkit which uses
// this pattern throughout es-toolkit/compat/**. We tried `optimizeDeps`,
// `commonjsOptions.transformMixedEsModules`, and switching the minifier
// to terser - none of those touch the production CJS transform, only
// pre-bundling for dev. The clean fix is to use the Rollup-based bundler
// (Vite 7) until upstream Rolldown lands a fix.
export default defineConfig({
  plugins: [react()],
  build: {
    sourcemap: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5173',
      '/hubs': {
        target: 'http://localhost:5173',
        ws: true
      }
    }
  }
})
