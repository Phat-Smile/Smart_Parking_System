import { expect, test } from "@playwright/test";

test("Email verification screen shows its pending-session state", async ({
    page,
}) => {
    await page.goto("/verify-otp");
    await expect(
        page.getByRole("heading", { name: "Verify Your Email" }),
    ).toBeVisible();
    await expect(
        page.getByText("No active verification request was found."),
    ).toBeVisible();
    await expect(
        page.getByRole("button", { name: "Verify Code" }),
    ).toBeDisabled();
    await page.getByRole("link", { name: "Back to Sign Up" }).click();
    await expect(page).toHaveURL(/\/sign-up$/);
});

test("Complete OTP input does not fake account verification", async ({
    page,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/verify-otp");
    for (const [index, digit] of [..."012345"].entries()) {
        await page
            .getByRole("textbox", { name: `Digit ${index + 1} of 6` })
            .fill(digit);
    }
    await page.getByRole("button", { name: "Verify Code" }).click();
    await expect(
        page
            .getByRole("alert")
            .filter({ hasText: "No active verification request" }),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/verify-otp$/);
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > window.innerWidth,
        ),
    ).toBe(false);
});
