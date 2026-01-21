// vite.config.ts
// Vite build configuration for the React SPA frontend.
// Proxies /api and /hub requests to Agent at localhost:17201 during development.
// Builds output to ../Octoporty.Agent/wwwroot for embedding in Agent.

import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:17201',
        changeOrigin: true,
      },
      '/hub': {
        target: 'http://localhost:17201',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    outDir: '../Octoporty.Agent/wwwroot',
    emptyOutDir: true,
  },
})
