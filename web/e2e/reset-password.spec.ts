import { expect, test } from "@playwright/test";

test("Reset Password requires a link token and offers a new link", async ({
    page,
}) => {
    await page.goto("/reset-password");
    await expect(
        page.getByRole("heading", { name: "Reset link expired" }),
    ).toBeVisible();
    await page.getByRole("link", { name: "Request New Link" }).click();
    await expect(page).toHaveURL(/\/forgot-password$/);
});

test("Reset Password validates fields and does not leak credentials to the URL", async ({
    page,
}) => {
    await page.goto("/reset-password?token=server-token");
    await expect(
        page.getByRole("heading", { name: "Create a new password" }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Change Password" }).click();
    await expect(
        page.getByText("Password must contain at least 8 characters"),
    ).toBeVisible();
    await page.locator("#parkwise-reset-new-password").fill("Valid123!");
    await page.locator("#parkwise-reset-confirm-password").fill("Valid123!");
    await page.getByRole("button", { name: "Show new password" }).click();
    await expect(page.locator("#parkwise-reset-new-password")).toHaveAttribute(
        "type",
        "text",
    );
    await page.getByRole("button", { name: "Change Password" }).click();
    await expect(
        page.getByText(
            "Password reset is not available yet. Please try again later.",
        ),
    ).toBeVisible();
    expect(page.url()).not.toContain("Valid123!");
});

test("Reset Password fits mobile width", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/reset-password?token=server-token");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
        ),
    ).toBe(false);
});
