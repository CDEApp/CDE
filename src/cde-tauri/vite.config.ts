import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Tauri expects a fixed dev port and leaves the terminal output to the Tauri CLI.
export default defineConfig({
  plugins: [react()],
  clearScreen: false,
  server: {
    port: 1420,
    strictPort: true,
    watch: {
      // The Tauri CLI watches the Rust side; Vite watching src-tauri/target hits files cargo
      // has locked mid-build (EBUSY on cde_tauri.exe) and crashes the dev server.
      ignored: ["**/src-tauri/**"],
    },
  },
  build: {
    target: "es2021",
    outDir: "dist",
  },
});
