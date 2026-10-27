import { expect, test } from "@playwright/test";

test("Guest navigation opens the Login screen and Home returns to the landing", async ({
    page,
}) => {
    await page.goto("/");
    await page.getByRole("link", { name: "Login", exact: true }).last().click();
    await expect(page).toHaveURL(/\/login$/);
    await expect(
        page.getByRole("heading", { name: "Log in to your account" }),
    ).toBeVisible();
    await page
        .getByRole("navigation", { name: "Main navigation" })
        .getByRole("link", { name: "Home" })
        .click();
    await expect(page).toHaveURL(/\/$/);
});

test("Login validates locally and never sends credentials in the URL", async ({
    page,
}) => {
    let loginBody: Record<string, unknown> | undefined;
    await page.route("**/api/identity/accounts/login", async (route) => {
        loginBody = route.request().postDataJSON();
        await route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                accessToken: "test-access-token",
                tokenType: "Bearer",
                expiresIn: 900,
                refreshToken: "test-refresh-token",
                account: {
                    id: "account-id",
                    email: "driver@example.com",
                    phoneNumber: null,
                    status: "EmailVerified",
                    role: "Customer",
                },
            }),
        });
    });
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    await page.goto("/login");
    await page.getByRole("button", { name: "Log in", exact: true }).click();
    await expect(page.getByText("Email is required")).toBeVisible();
    await expect(page.getByText("Password is required")).toBeVisible();

    await page
        .getByRole("textbox", { name: "Email" })
        .fill("driver@example.com");
    await page
        .getByLabel("Password", { exact: true })
        .fill("ValidPassword123!");
    await page.getByRole("button", { name: "Log in", exact: true }).click();
    await expect(page).toHaveURL(/\/customer\/complete-profile$/);
    await expect(page.getByText("driver@example.com").first()).toBeVisible();
    expect(loginBody).toEqual({
        email: "driver@example.com",
        password: "ValidPassword123!",
    });
    expect(page.url()).not.toContain("driver@example.com");
    expect(page.url()).not.toContain("ValidPassword123");
    expect(
        await page.evaluate(() =>
            Object.keys(sessionStorage)
                .map((key) => sessionStorage.getItem(key))
                .join(" "),
        ),
    ).not.toContain("test-refresh-token");
    expect(
        await page.evaluate(() =>
            Object.keys(localStorage)
                .map((key) => localStorage.getItem(key))
                .join(" "),
        ),
    ).not.toContain("test-access-token");
    expect(pageErrors).toEqual([]);
});

test("Login displays Identity credential errors without creating a session", async ({
    page,
}) => {
    await page.route("**/api/identity/accounts/login", (route) =>
        route.fulfill({
            status: 401,
            contentType: "application/problem+json",
            body: JSON.stringify({
                status: 401,
                title: "Unauthorized",
                errorCode: "identity.invalid_credentials",
            }),
        }),
    );
    await page.goto("/login");
    await page
        .getByRole("textbox", { name: "Email" })
        .fill("driver@example.com");
    await page
        .getByLabel("Password", { exact: true })
        .fill("WrongPassword123!");
    await page.getByRole("button", { name: "Log in", exact: true }).click();
    await expect(
        page.getByText("Email or password is incorrect."),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);
});

test("Google control is shown and mobile form fits", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/login");
    // Google's own button when NEXT_PUBLIC_GOOGLE_CLIENT_ID is set, otherwise the ParkWise fallback.
    const google = page
        .getByTestId("google-sign-in")
        .or(page.getByRole("button", { name: "Continue with Google" }));
    await expect(google).toBeVisible();
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > window.innerWidth,
        ),
    ).toBe(false);
});
