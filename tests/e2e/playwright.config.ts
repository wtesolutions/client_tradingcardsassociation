import { defineConfig } from "@playwright/test";

const baseURL = process.env.BASE_URL || "http://localhost:5290";

export default defineConfig({
  testDir: ".",
  timeout: 30_000,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: 0,
  use: {
    baseURL,
    trace: "on-first-retry"
  },
  reporter: [["list"]]
});
