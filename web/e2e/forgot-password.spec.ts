import { expect, test } from "@playwright/test";

test("Forgot Password requests an OTP and keeps the reset flow usable", async ({
    page,
}) => {
    await page.goto("/forgot-password");
    await expect(
        page.getByRole("heading", { name: "Forgot your password?", level: 2 }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Send Reset Code" }).click();
    await expect(page.getByText("Email is required")).toBeVisible();
    await page.route("**/api/identity/accounts/forgot-password", (route) =>
        route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                email: "driver@example.com",
                expiresInSeconds: 300,
                cooldownSeconds: 60,
                message: "If the account exists, a code was sent.",
            }),
        }),
    );
    await page
        .getByRole("textbox", { name: "Email" })
        .fill("driver@example.com");
    await page.getByRole("button", { name: "Send Reset Code" }).click();
    await expect(
        page.getByRole("heading", { name: "Check your email" }),
    ).toBeVisible();
    await expect(
        page.getByRole("group", { name: "Six-digit code" }),
    ).toBeVisible();
    for (const [index, digit] of [..."012345"].entries()) {
        await page
            .getByRole("textbox", { name: `Digit ${index + 1} of 6` })
            .fill(digit);
    }
    await page.getByRole("button", { name: "Continue" }).click();
    await expect(page).toHaveURL(/\/reset-password$/);
    await expect(
        page.getByRole("heading", { name: "Create a new password" }),
    ).toBeVisible();
    expect(page.url()).not.toContain("driver@example.com");
});

test("Forgot Password stays usable at mobile width", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.route("**/api/identity/accounts/forgot-password", (route) =>
        route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                email: "driver@example.com",
                expiresInSeconds: 300,
                cooldownSeconds: 60,
                message: "Accepted",
            }),
        }),
    );
    await page.goto("/forgot-password");
    await expect(
        page.getByRole("link", { name: "Back to Login" }),
    ).toBeVisible();
    await page
        .getByRole("textbox", { name: "Email" })
        .fill("driver@example.com");
    await page.getByRole("button", { name: "Send Reset Code" }).click();
    await expect(
        page.getByRole("group", { name: "Six-digit code" }),
    ).toBeVisible();
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
        ),
    ).toBe(false);
});
