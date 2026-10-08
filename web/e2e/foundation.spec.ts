import { expect, test } from "@playwright/test";

test("home renders the ParkWise landing page", async ({ page }) => {
    await page.goto("/");
    await expect(page).toHaveURL(/\/$/);
    await expect(
        page.getByRole("heading", {
            name: /Find the Right Parking Spot, Smarter/i,
        }),
    ).toBeVisible();
});

test("legacy Guest route redirects to the ParkWise landing", async ({
    page,
}) => {
    await page.goto("/guest");
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("unknown route uses not-found UI", async ({ page }) => {
    const response = await page.goto("/unknown-foundation-route");
    expect(response?.status()).toBe(404);
    await expect(
        page.getByRole("heading", { name: "Page not found" }),
    ).toBeVisible();
});
