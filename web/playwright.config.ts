import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
    testDir: "./e2e",
    use: { baseURL: "http://127.0.0.1:3000" },
    projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"], channel: "chrome" } }],
    webServer: {
        command: "npm run dev -- --hostname 127.0.0.1",
        url: "http://127.0.0.1:3000",
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
    },
});
