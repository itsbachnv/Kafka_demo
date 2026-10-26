import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Khi dev: /api được proxy sang user-service (mặc định http://localhost:3000),
// nên trình duyệt chỉ gọi cùng origin và không cần cấu hình CORS.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': process.env.VITE_API_TARGET || 'http://localhost:3000',
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/setupTests.js',
  },
});
