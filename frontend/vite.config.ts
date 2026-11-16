import { defineConfig } from "vite";
import { fileURLToPath, URL } from "node:url";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      "/api": { target: process.env.VITE_API_URL || "http://localhost:5080", changeOrigin: true },
      "/health": { target: process.env.VITE_API_URL || "http://localhost:5080", changeOrigin: true }
    }
  }
});

