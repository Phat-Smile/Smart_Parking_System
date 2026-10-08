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
    await expect(page.getByText(/Log in is not connected/)).toBeVisible();
    await expect(page).toHaveURL(/\/login$/);
    expect(page.url()).not.toContain("driver@example.com");
    expect(page.url()).not.toContain("ValidPassword123");
    expect(pageErrors).toEqual([]);
});

test("Google control reports pending integration and mobile form fits", async ({
    page,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/login");
    await page.getByRole("button", { name: "Continue with Google" }).click();
    await expect(page.getByRole("status")).toContainText("not available yet");
    expect(
        await page.evaluate(
            () => document.documentElement.scrollWidth > window.innerWidth,
        ),
    ).toBe(false);
});
