import { expect, test } from "@playwright/test";

for (const osTheme of ["light", "dark"] as const) {
    for (const selectedTheme of ["light", "dark"] as const) {
        test(`stored ${selectedTheme} theme wins on ${osTheme} OS`, async ({
            page,
        }) => {
            await page.emulateMedia({ colorScheme: osTheme });
            await page.addInitScript(
                (theme) => localStorage.setItem("smart-parking-theme", theme),
                selectedTheme,
            );
            await page.goto("/");
            await expect(page.locator("html")).toHaveAttribute(
                "data-theme",
                selectedTheme,
            );
            await expect(page.locator("html")).toHaveClass(
                selectedTheme === "dark" ? /dark/ : /^(?!.*dark).*$/,
            );
            await expect(
                page.getByRole("heading", {
                    name: /Find the Right Parking Spot/i,
                }),
            ).toBeVisible();
        });
    }
}

test("stored theme is applied before landing hydration", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "light" });
    await page.addInitScript(() =>
        localStorage.setItem("smart-parking-theme", "dark"),
    );
    await page.route("**/_next/static/chunks/*.js", async (route) => {
        await new Promise((resolve) => setTimeout(resolve, 1500));
        await route.continue();
    });
    await page.goto("/", { waitUntil: "commit" });
    await expect(page.locator("html")).toHaveClass(/dark/);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
});

test("mobile navigation remains usable without horizontal overflow", async ({
    page,
}) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto("/");
    await page.getByRole("button", { name: "Open menu" }).click();
    await expect(
        page.getByRole("navigation", { name: "Mobile navigation" }),
    ).toBeVisible();
    await page.getByRole("link", { name: "How It Works" }).first().click();
    await expect(page).toHaveURL(/#how-it-works$/);
    const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth > window.innerWidth,
    );
    expect(overflow).toBe(false);
});
