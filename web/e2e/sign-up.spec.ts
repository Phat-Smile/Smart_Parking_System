import { expect, test } from "@playwright/test";

test("Landing Sign Up opens the form and Log in returns to Login", async ({
    page,
}) => {
    await page.goto("/");
    await page
        .getByRole("link", { name: "Sign Up", exact: true })
        .last()
        .click();
    await expect(page).toHaveURL(/\/sign-up$/);
    await expect(
        page.getByRole("heading", { name: "Create your account" }),
    ).toBeVisible();
    await page
        .getByRole("link", { name: "Log in", exact: true })
        .last()
        .click();
    await expect(page).toHaveURL(/\/login$/);
});

test("Sign Up validates fields and posts the documented registration contract", async ({
    page,
}) => {
    let registrationBody: Record<string, unknown> | undefined;
    await page.route("**/api/identity/accounts", async (route) => {
        registrationBody = route.request().postDataJSON();
        await route.fulfill({
            status: 201,
            contentType: "application/json",
            body: JSON.stringify({
                accountId: "7b92df78-c09a-4122-87f5-9610f4439c27",
                email: "jane@example.com",
                fullName: "Jane Doe",
                status: "PendingEmailVerification",
            }),
        });
    });
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    await page.goto("/sign-up");
    await page.getByRole("button", { name: "Create Account" }).click();
    await expect(page.getByText("Email is required")).toBeVisible();

    await page.locator("#parkwise-signup-first-name").fill("Jane");
    await page.locator("#parkwise-signup-last-name").fill("Doe");
    await page.locator("#parkwise-signup-email").fill("jane@example.com");
    await page.locator("#parkwise-signup-phone").fill("0912345678");
    await page.locator("#parkwise-signup-password").fill("ValidPassword123!");
    await page
        .locator("#parkwise-signup-confirm-password")
        .fill("Mismatch123!");
    await page.getByRole("button", { name: "Create Account" }).click();
    await expect(page.getByText("The passwords do not match")).toBeVisible();

    await page
        .locator("#parkwise-signup-confirm-password")
        .fill("ValidPassword123!");
    await page.getByRole("button", { name: "Create Account" }).click();
    await expect(page).toHaveURL(/\/verify-otp$/);
    expect(registrationBody).toEqual({
        fullName: "Jane Doe",
        email: "jane@example.com",
        password: "ValidPassword123!",
        phoneNumber: "0912345678",
    });
    expect(
        await page.evaluate(() =>
            sessionStorage.getItem("parkwise.pending-verification-email"),
        ),
    ).toBe("jane@example.com");
    expect(page.url()).not.toContain("ValidPassword123");
    expect(pageErrors).toEqual([]);
});

test("Sign Up keeps the form on a registered-email conflict", async ({
    page,
}) => {
    await page.route("**/api/identity/accounts", (route) =>
        route.fulfill({
            status: 409,
            contentType: "application/problem+json",
            body: JSON.stringify({
                title: "Conflict",
                status: 409,
                errorCode: "identity.email_already_registered",
            }),
        }),
    );
    await page.goto("/sign-up");
    await page.locator("#parkwise-signup-first-name").fill("Jane");
    await page.locator("#parkwise-signup-last-name").fill("Doe");
    await page.locator("#parkwise-signup-email").fill("jane@example.com");
    await page.locator("#parkwise-signup-phone").fill("0912345678");
    await page.locator("#parkwise-signup-password").fill("ValidPassword123!");
    await page
        .locator("#parkwise-signup-confirm-password")
        .fill("ValidPassword123!");
    await page.getByRole("button", { name: "Create Account" }).click();
    await expect(
        page.getByText("This email address is already registered."),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/sign-up$/);
});

test("Sign Up accepts email and password without optional profile fields", async ({
    page,
}) => {
    let body: Record<string, unknown> | undefined;
    await page.route("**/api/identity/accounts", async (route) => {
        body = route.request().postDataJSON();
        await route.fulfill({
            status: 201,
            contentType: "application/json",
            body: JSON.stringify({
                accountId: "id-2",
                email: "minimal@example.com",
                fullName: null,
                status: "PendingEmailVerification",
                message: "Code sent",
            }),
        });
    });
    await page.goto("/sign-up");
    await page.locator("#parkwise-signup-email").fill("minimal@example.com");
    await page.locator("#parkwise-signup-password").fill("ValidPassword123!");
    await page
        .locator("#parkwise-signup-confirm-password")
        .fill("ValidPassword123!");
    await page.getByRole("button", { name: "Create Account" }).click();
    await expect(page).toHaveURL(/\/verify-otp$/);
    expect(body).toEqual({
        fullName: null,
        email: "minimal@example.com",
        password: "ValidPassword123!",
        phoneNumber: null,
    });
});

test("Sign Up password visibility and mobile layout remain usable", async ({
    page,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/sign-up");
    const password = page.locator("#parkwise-signup-password");
    await expect(password).toHaveAttribute("type", "password");
    await page
        .getByRole("button", { name: "Show password", exact: true })
        .click();
    await expect(password).toHaveAttribute("type", "text");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > window.innerWidth,
        ),
    ).toBe(false);
});
