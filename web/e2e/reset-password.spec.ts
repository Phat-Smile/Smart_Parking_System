import { expect, test } from "@playwright/test";

test("Reset Password validates OTP and posts the documented body", async ({
    page,
}) => {
    let payload: Record<string, unknown> | undefined;
    await page.route(
        "**/api/identity/accounts/reset-password",
        async (route) => {
            payload = route.request().postDataJSON();
            await route.fulfill({
                status: 200,
                contentType: "application/json",
                body: JSON.stringify({ message: "Password reset" }),
            });
        },
    );
    await page.goto("/reset-password?email=driver%40example.com");
    await page.getByRole("button", { name: "Change Password" }).click();
    await expect(
        page.getByText("Enter the six-digit code from your email"),
    ).toBeVisible();
    await page.locator("#parkwise-reset-code").fill("012345");
    await page.locator("#parkwise-reset-new-password").fill("Valid123!");
    await page.locator("#parkwise-reset-confirm-password").fill("Valid123!");
    await page.getByRole("button", { name: "Change Password" }).click();
    await expect(
        page.getByRole("heading", { name: "Password changed successfully!" }),
    ).toBeVisible();
    expect(payload).toEqual({
        email: "driver@example.com",
        otpCode: "012345",
        newPassword: "Valid123!",
    });
    expect(page.url()).not.toContain("Valid123!");
    expect(page.url()).not.toContain("012345");
});

test("Reset Password fits mobile width", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/reset-password?email=driver%40example.com");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
        ),
    ).toBe(false);
});
