import { expect, test } from "@playwright/test";

test("Signed-in user changes password and logs out through Identity", async ({
    page,
}) => {
    let changed: Record<string, unknown> | undefined;
    let loggedOut: Record<string, unknown> | undefined;
    await page.route("**/api/identity/accounts/login", (route) =>
        route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({
                accessToken: "access-token",
                refreshToken: "refresh-token",
                tokenType: "Bearer",
                expiresIn: 900,
                account: {
                    id: "account-id",
                    email: "driver@example.com",
                    phoneNumber: null,
                    status: "EmailVerified",
                    role: "Customer",
                },
            }),
        }),
    );
    await page.route("**/api/identity/accounts/change-password", (route) => {
        changed = route.request().postDataJSON();
        return route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({ message: "Password changed" }),
        });
    });
    await page.route("**/api/identity/accounts/logout", (route) => {
        loggedOut = route.request().postDataJSON();
        return route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify({ message: "Logged out" }),
        });
    });

    await page.goto("/login");
    await page
        .getByRole("textbox", { name: "Email" })
        .fill("driver@example.com");
    await page
        .getByLabel("Password", { exact: true })
        .fill("ValidPassword123!");
    await page.getByRole("button", { name: "Log in", exact: true }).click();
    await page.getByRole("link", { name: "Account security" }).click();
    await expect(page).toHaveURL(/\/settings\/security$/);
    await page.getByLabel("Current password").fill("ValidPassword123!");
    await page
        .getByLabel("New password", { exact: true })
        .fill("NewPassword123!");
    await page.getByLabel("Confirm new password").fill("NewPassword123!");
    await page.getByRole("button", { name: "Change password" }).click();
    await expect(
        page.getByText("Password changed successfully."),
    ).toBeVisible();
    expect(changed).toEqual({
        currentPassword: "ValidPassword123!",
        newPassword: "NewPassword123!",
    });
    await page.getByRole("button", { name: "Log out" }).click();
    await expect(page).toHaveURL(/\/login$/);
    expect(loggedOut).toEqual({ refreshToken: "refresh-token" });
});
