import { expect, test } from "@playwright/test";

for (const osTheme of ["light", "dark"] as const) {
    for (const selectedTheme of ["light", "dark"] as const) {
        test(`Guest ${selectedTheme} theme renders on ${osTheme} OS`, async ({
            page,
        }, testInfo) => {
            const initialTheme = selectedTheme === "light" ? "dark" : "light";
            await page.emulateMedia({ colorScheme: osTheme });
            await page.addInitScript((theme) => {
                localStorage.setItem("smart-parking-theme", theme);
            }, initialTheme);

            await page.goto("/guest");
            const toggle = page
                .getByRole("button", { name: "Switch theme" })
                .first();
            await expect(toggle).toHaveAttribute(
                "title",
                initialTheme === "light"
                    ? "Switch to Dark Mode"
                    : "Switch to Light Mode",
            );
            await toggle.click();

            await expect(page.locator("html")).toHaveAttribute(
                "data-theme",
                selectedTheme,
            );
            await expect(page.locator("#pageBody")).toHaveCSS(
                "background-color",
                selectedTheme === "dark"
                    ? "rgb(3, 1, 14)"
                    : "rgb(248, 249, 252)",
            );
            await expect(page.locator("#heroHeading")).toHaveCSS(
                "color",
                selectedTheme === "dark"
                    ? "rgb(255, 255, 255)"
                    : "rgb(17, 19, 34)",
            );
            await page.screenshot({
                path: testInfo.outputPath(
                    `guest-${osTheme}-${selectedTheme}.png`,
                ),
                fullPage: true,
                animations: "disabled",
            });
        });
    }
}

test("stored dark theme is applied before hydration", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "light" });
    await page.addInitScript(() => {
        localStorage.setItem("smart-parking-theme", "dark");
    });
    await page.route("**/_next/static/chunks/*.js", async (route) => {
        await new Promise((resolve) => setTimeout(resolve, 1500));
        await route.continue();
    });

    await page.goto("/guest", { waitUntil: "commit" });
    await expect(page.locator("#pageBody")).toBeVisible();
    await expect(page.locator("html")).toHaveClass(/dark/);
    await expect(page.locator("#pageBody")).toHaveCSS(
        "background-color",
        "rgb(3, 1, 14)",
    );
});
