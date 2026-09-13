import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5266', changeOrigin: true },
      '/health': { target: 'http://localhost:5266', changeOrigin: true },
      '/hubs': { target: 'http://localhost:5266', ws: true, changeOrigin: true },
    },
  },
})
