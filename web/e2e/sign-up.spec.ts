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

test("Sign Up validates fields and does not fake account creation", async ({
    page,
}) => {
    const pageErrors: string[] = [];
    page.on("pageerror", (error) => pageErrors.push(error.message));
    await page.goto("/sign-up");
    await page.getByRole("button", { name: "Create Account" }).click();
    await expect(page.getByText("First name is required")).toBeVisible();
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
    await expect(page.getByText(/Sign up is not connected/)).toBeVisible();
    await expect(page).toHaveURL(/\/sign-up$/);
    expect(page.url()).not.toContain("ValidPassword123");
    expect(pageErrors).toEqual([]);
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
