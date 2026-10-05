import { expect, test } from "@playwright/test";

test("home shell loads", async ({ page }) => {
    await page.goto("/");
    await expect(
        page.getByRole("heading", { name: "Web foundation" }),
    ).toBeVisible();
});

test("unknown route uses not-found UI", async ({ page }) => {
    const response = await page.goto("/unknown-foundation-route");
    expect(response?.status()).toBe(404);
    await expect(
        page.getByRole("heading", { name: "Page not found" }),
    ).toBeVisible();
});
