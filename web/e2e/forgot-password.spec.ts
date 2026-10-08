import { expect, test } from "@playwright/test";

test("Forgot Password validates email and does not put it in the URL", async ({
    page,
}) => {
    await page.goto("/forgot-password");
    await expect(
        page.getByRole("heading", { name: "Forgot your password?", level: 2 }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Send Reset Link" }).click();
    await expect(page.getByText("Email is required")).toBeVisible();
    await page
        .getByRole("textbox", { name: "Email" })
        .fill("driver@example.com");
    await page.getByRole("button", { name: "Send Reset Link" }).click();
    await expect(
        page.getByText(
            "Password reset is not available yet. Please try again later.",
        ),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/forgot-password$/);
    expect(page.url()).not.toContain("driver@example.com");
});

test("Forgot Password stays usable at mobile width", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/forgot-password");
    await expect(
        page.getByRole("link", { name: "Back to Login" }),
    ).toBeVisible();
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
        ),
    ).toBe(false);
});
