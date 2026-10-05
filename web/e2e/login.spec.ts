import { expect, test } from "@playwright/test";

for (const width of [390, 768, 1280]) {
    test(`Login is usable without overflow at ${width}px`, async ({ page }) => {
        await page.setViewportSize({
            width,
            height: width === 390 ? 844 : 1117,
        });
        const errors: string[] = [];
        page.on("pageerror", (error) => errors.push(error.message));
        await page.goto("/login");
        await expect(
            page.getByRole("heading", { name: /Sign in to your account/i }),
        ).toBeVisible();
        await expect(page.getByLabel("Email Address")).toBeVisible();
        await expect(page.locator("#login-password")).toBeVisible();
        if (width === 390) {
            await page
                .getByRole("button", { name: "Toggle navigation menu" })
                .click();
            await expect(
                page.getByRole("navigation", { name: "Mobile navigation" }),
            ).toBeVisible();
        }
        expect(
            await page.evaluate(
                () => document.documentElement.scrollWidth <= innerWidth,
            ),
        ).toBe(true);
        expect(errors).toEqual([]);
    });
}

test("Login validates by keyboard and does not fake success", async ({
    page,
}) => {
    await page.goto("/login");
    await page.getByLabel("Email Address").focus();
    await page.keyboard.press("Tab");
    await expect(page.locator("#login-password")).toBeFocused();
    await page.getByRole("button", { name: "Log In" }).click();
    await expect(page.getByText("Email is required")).toBeVisible();
    await expect(page.getByText("Password is required")).toBeVisible();
    await page.getByLabel("Email Address").fill("person@example.com");
    await page.locator("#login-password").fill("example password");
    await page.getByRole("button", { name: "Show password" }).click();
    await expect(page.locator("#login-password")).toHaveAttribute(
        "type",
        "text",
    );
    await page.getByRole("button", { name: "Log In" }).click();
    await expect(page.getByRole("status")).toContainText(
        "Sign-in is not available yet",
    );
    await expect(page).toHaveURL(/\/login$/);
});

test("Login follows explicit theme selection", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "light" });
    await page.addInitScript(() =>
        localStorage.setItem("smart-parking-theme", "light"),
    );
    await page.goto("/login");
    await expect(page.locator("html")).not.toHaveClass(/dark/);
    await page.getByRole("button", { name: "Switch to dark mode" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
    await expect(page.locator("body > div.min-h-screen")).toHaveCSS(
        "background-color",
        "rgb(3, 1, 14)",
    );
});
