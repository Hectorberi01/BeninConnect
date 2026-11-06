/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'
import { defineConfig, loadEnv } from 'vite'

// En développement, /api et /uploads sont relayés vers l'API (pas de CORS à gérer).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.API_PROXY_TARGET || 'http://localhost:5080'
  const proxy = { target, changeOrigin: true, headers: { 'ngrok-skip-browser-warning': '1' } }
  return {
    plugins: [react(), tailwindcss()],
    resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
    server: { port: 5173, proxy: { '/api': proxy, '/uploads': proxy } },
    preview: { proxy: { '/api': proxy, '/uploads': proxy } },
    test: { environment: 'node' },
  }
})
