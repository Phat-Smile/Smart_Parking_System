import { expect, test } from "@playwright/test";

for (const width of [390, 1280]) {
    test(`Guest Login navigates to /login at ${width}px`, async ({ page }) => {
        await page.setViewportSize({ width, height: 844 });
        const errors: string[] = [];
        page.on("pageerror", (error) => errors.push(error.message));
        await page.goto("/guest");
        if (width === 390) {
            await page
                .getByRole("button", { name: "Toggle navigation menu" })
                .click();
        }
        await page.getByRole("link", { name: "Log In" }).click();
        await expect(page).toHaveURL(/\/login$/);
        await expect(
            page.getByRole("heading", { name: /Sign in to your account/i }),
        ).toBeVisible();
        expect(errors).toEqual([]);
    });
}

test("Back to Home returns to the Guest landing page", async ({ page }) => {
    await page.goto("/login");
    const backLink = page.getByRole("link", { name: "Back to Home" });
    await expect(backLink).toHaveAttribute("href", "/");
    await backLink.click();
    await expect(page).toHaveURL(/\/guest$/);
    await expect(
        page.getByRole("heading", { name: /Find parking before you arrive/i }),
    ).toBeVisible();
});

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
        await expect(
            page.getByRole("button", { name: "Switch to light mode" }),
        ).toBeVisible();
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
    const loginNavigations: string[] = [];
    page.on("framenavigated", (frame) => {
        if (frame === page.mainFrame()) loginNavigations.push(frame.url());
    });
    await page.goto("/login");
    await expect(page.locator("form")).toHaveAttribute("method", "post");
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
    await page.locator("#login-password").press("Enter");
    await expect(page.getByRole("status")).toContainText(
        "Sign-in is not available yet",
    );
    await expect(page).toHaveURL(/\/login$/);
    expect(new URL(page.url()).search).toBe("");
    expect(loginNavigations.every((url) => new URL(url).search === "")).toBe(
        true,
    );
});

for (const osTheme of ["light", "dark"] as const) {
    for (const selectedTheme of ["light", "dark"] as const) {
        test(`Login renders ${selectedTheme} when OS prefers ${osTheme}`, async ({
            page,
        }) => {
            await page.emulateMedia({ colorScheme: osTheme });
            await page.addInitScript((theme) => {
                localStorage.setItem("smart-parking-theme", theme);
            }, selectedTheme);
            await page.goto("/login");
            await expect(page.locator("html")).toHaveAttribute(
                "data-theme",
                selectedTheme,
            );
            await expect(page.locator("html")).toHaveClass(
                selectedTheme === "dark" ? /dark/ : /^(?!.*dark).*$/,
            );
            await expect(page.locator("body > div.min-h-screen")).toHaveCSS(
                "background-color",
                selectedTheme === "dark"
                    ? "rgb(21, 17, 35)"
                    : "rgb(248, 249, 252)",
            );
        });
    }
}

test("Login header keeps the home link and theme control", async ({ page }) => {
    await page.goto("/login");
    await expect(
        page.getByRole("button", { name: "Switch to light mode" }),
    ).toBeVisible();
    await expect(page.getByRole("link", { name: "How It Works" })).toHaveCount(
        0,
    );
    await expect(page.getByRole("link", { name: "Support" })).toHaveCount(0);
    await page.keyboard.press("Tab");
    await expect(
        page.getByRole("link", { name: "Smart Parking System" }),
    ).toBeFocused();
    await expect(
        page.getByRole("link", { name: "Smart Parking System" }),
    ).toHaveCSS("outline-style", "solid");
});

test("Login theme toggle changes the rendered theme", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "light" });
    await page.addInitScript(() =>
        localStorage.setItem("smart-parking-theme", "light"),
    );
    await page.goto("/login");
    await page.getByRole("button", { name: "Switch to dark mode" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
    await expect(page.locator("body > div.min-h-screen")).toHaveCSS(
        "background-color",
        "rgb(21, 17, 35)",
    );
});
