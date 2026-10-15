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

test("OTP without a registration email cannot claim verification", async ({
    page,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/verify-otp");
    for (const [index, digit] of [..."012345"].entries()) {
        await page
            .getByRole("textbox", { name: `Digit ${index + 1} of 6` })
            .fill(digit);
    }
    await expect(
        page.getByRole("button", { name: "Verify Code" }),
    ).toBeDisabled();
    await expect(page).toHaveURL(/\/verify-otp$/);
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > window.innerWidth,
        ),
    ).toBe(false);
});

test("OTP verify and resend use Identity responses without exposing the code in the URL", async ({
    page,
}) => {
    await page.addInitScript(() =>
        sessionStorage.setItem(
            "parkwise.pending-verification-email",
            "driver@example.com",
        ),
    );
    let verifyBody: Record<string, unknown> | undefined;
    let resendBody: Record<string, unknown> | undefined;
    await page.route("**/api/identity/accounts/resend-otp", async (route) => {
        resendBody = route.request().postDataJSON();
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                email: "driver@example.com",
                expiresInSeconds: 300,
                cooldownSeconds: 60,
                message: "A new code was sent.",
                otpSent: true,
            }),
        });
    });
    await page.route("**/api/identity/accounts/verify-email", async (route) => {
        verifyBody = route.request().postDataJSON();
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                accountId: "7b92df78-c09a-4122-87f5-9610f4439c27",
                email: "driver@example.com",
                status: "EmailVerified",
                isVerified: true,
            }),
        });
    });
    await page.goto("/verify-otp");
    await expect(
        page.getByText("Enter the verification code for driver@example.com."),
    ).toBeVisible();
    await page.getByRole("button", { name: "Resend code" }).click();
    await expect(
        page.getByRole("button", { name: /Resend in/ }),
    ).toBeDisabled();
    expect(resendBody).toEqual({ email: "driver@example.com" });
    for (const [index, digit] of [..."012345"].entries()) {
        await page
            .getByRole("textbox", { name: `Digit ${index + 1} of 6` })
            .fill(digit);
    }
    await expect(
        page.getByRole("heading", { name: "Email verified" }),
    ).toBeVisible();
    expect(verifyBody).toEqual({
        email: "driver@example.com",
        otpCode: "012345",
    });
    expect(
        await page.evaluate(() =>
            sessionStorage.getItem("parkwise.pending-verification-email"),
        ),
    ).toBeNull();
    expect(page.url()).not.toContain("012345");
});

test("An invalid OTP shows the Identity error and stays on verification", async ({
    page,
}) => {
    await page.addInitScript(() =>
        sessionStorage.setItem(
            "parkwise.pending-verification-email",
            "driver@example.com",
        ),
    );
    await page.route("**/api/identity/accounts/verify-email", (route) =>
        route.fulfill({
            status: 400,
            contentType: "application/problem+json",
            body: JSON.stringify({
                status: 400,
                title: "Bad Request",
                errorCode: "identity.invalid_otp_code",
            }),
        }),
    );
    await page.goto("/verify-otp");
    for (const [index, digit] of [..."123456"].entries()) {
        await page
            .getByRole("textbox", { name: `Digit ${index + 1} of 6` })
            .fill(digit);
    }
    await expect(
        page.getByText("Incorrect code. Please check it and try again."),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/verify-otp$/);
});
