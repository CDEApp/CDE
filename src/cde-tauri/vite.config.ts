import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { execFileSync } from "node:child_process";

// Release date shown in the About dialog: SOURCE_DATE_EPOCH (reproducible-builds convention) if set,
// else the committer date of the last git commit, else today. Always UTC, YYYY-MM-DD.
function buildDate(): string {
  const toDate = (epochSeconds: number) => new Date(epochSeconds * 1000).toISOString().slice(0, 10);
  const fromEnv = Number(process.env.SOURCE_DATE_EPOCH);
  if (process.env.SOURCE_DATE_EPOCH && Number.isFinite(fromEnv)) return toDate(fromEnv);
  try {
    const out = execFileSync("git", ["log", "-1", "--format=%ct"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
    const t = Number(out.trim());
    if (out.trim() && Number.isFinite(t)) return toDate(t);
  } catch {
    // git missing or not a checkout: fall through
  }
  return new Date().toISOString().slice(0, 10);
}

// Tauri expects a fixed dev port and leaves the terminal output to the Tauri CLI.
export default defineConfig({
  plugins: [react()],
  define: { __BUILD_DATE__: JSON.stringify(buildDate()) },
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
