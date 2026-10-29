import { expect, test } from "@playwright/test";

test("Reset Password uses the OTP from the previous step and posts the documented body", async ({
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
    await page.addInitScript(() =>
        sessionStorage.setItem(
            "parkwise.pending-password-reset",
            JSON.stringify({
                email: "driver@example.com",
                otpCode: "012345",
                expiresAt: Date.now() + 300000,
            }),
        ),
    );
    await page.goto("/reset-password");
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
    await page.goto("/reset-password");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
        ),
    ).toBe(false);
});

test("Reset Password explains when the new password is the current password", async ({
    page,
}) => {
    await page.route("**/api/identity/accounts/reset-password", (route) =>
        route.fulfill({
            status: 400,
            contentType: "application/problem+json",
            body: JSON.stringify({
                status: 400,
                errorCode: "identity.password_same_as_current",
            }),
        }),
    );
    await page.addInitScript(() =>
        sessionStorage.setItem(
            "parkwise.pending-password-reset",
            JSON.stringify({
                email: "driver@example.com",
                otpCode: "012345",
                expiresAt: Date.now() + 300000,
            }),
        ),
    );
    await page.goto("/reset-password");
    await expect(
        page.getByRole("link", { name: "Request a new code" }),
    ).toHaveCount(0);
    await page.locator("#parkwise-reset-new-password").fill("Valid123!");
    await page.locator("#parkwise-reset-confirm-password").fill("Valid123!");
    await page.getByRole("button", { name: "Change Password" }).click();
    await expect(
        page.locator(".parkwise-login-error[role='alert']"),
    ).toHaveText("Choose a password different from your current one.");
});
