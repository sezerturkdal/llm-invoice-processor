import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The app calls the API with relative /api URLs. In development Vite forwards them to the
// .NET API; in the container nginx does the same, so the frontend code is identical in both.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5098',
    },
  },
})
