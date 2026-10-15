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
        page.getByRole("heading", { name: "We couldn't find this page." }),
    ).toBeVisible();
});

test("footer links open the public legal pages", async ({ page }) => {
    await page.goto("/");
    await page
        .getByRole("contentinfo")
        .getByRole("link", { name: "Privacy Policy" })
        .first()
        .click();
    await expect(page).toHaveURL(/\/privacy-policy$/);
    await expect(
        page.getByRole("heading", { name: "Privacy Policy", level: 1 }),
    ).toBeVisible();

    await page
        .getByRole("contentinfo")
        .getByRole("link", { name: "Terms & Conditions" })
        .first()
        .click();
    await expect(page).toHaveURL(/\/terms-and-conditions$/);
    await expect(
        page.getByRole("heading", { name: "Terms & Conditions", level: 1 }),
    ).toBeVisible();
});
