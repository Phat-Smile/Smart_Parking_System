import { expect, test } from "@playwright/test";

test("Registration route renders and validates before sending data", async ({
    page,
}) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    let calls = 0;
    await page.route("**/api/identity/accounts", (route) => {
        calls += 1;
        return route.fulfill({
            status: 201,
            contentType: "application/json",
            body: JSON.stringify({
                accountId: "id-1",
                email: "jane@example.com",
                status: "PendingEmailVerification",
            }),
        });
    });
    await page.goto("/register");
    await expect(
        page
            .getByRole("heading", { name: "Create account", exact: true })
            .last(),
    ).toBeVisible();
    await page
        .getByRole("button", { name: "Create account", exact: true })
        .click();
    await expect(page.getByText("Full name is required")).toBeVisible();
    expect(calls).toBe(0);
    expect(new URL(page.url()).pathname).toBe("/register");
    expect(errors).toEqual([]);
});

test("Valid form uses Gateway POST and never places credentials in the URL", async ({
    page,
}) => {
    let payload: Record<string, unknown> | undefined;
    await page.route("**/api/identity/accounts", async (route) => {
        expect(route.request().method()).toBe("POST");
        payload = route.request().postDataJSON();
        await route.fulfill({
            status: 201,
            contentType: "application/json",
            body: JSON.stringify({
                accountId: "id-1",
                email: "jane@example.com",
                status: "PendingEmailVerification",
            }),
        });
    });
    await page.goto("/register");
    await page.getByLabel("Full Name").fill("Jane Park");
    await page.getByLabel("Email Address").fill("jane@example.com");
    await page.getByLabel("Password", { exact: true }).fill("ValidPass1!");
    await page.getByLabel("Confirm Password").fill("ValidPass1!");
    await page.getByRole("checkbox").check();
    await page
        .getByRole("button", { name: "Create account", exact: true })
        .click();
    await expect(page.getByText(/Email verification is pending/)).toBeVisible();
    expect(payload).toEqual({
        email: "jane@example.com",
        password: "ValidPass1!",
        phoneNumber: null,
    });
    expect(new URL(page.url()).pathname).toBe("/register");
    expect(new URL(page.url()).search).toBe("");
});

test("Gateway conflict displays a safe registration error", async ({
    page,
}) => {
    await page.route("**/api/identity/accounts", (route) =>
        route.fulfill({
            status: 409,
            contentType: "application/problem+json",
            headers: {
                "X-Correlation-ID": "trace-123",
                "Access-Control-Expose-Headers": "X-Correlation-ID",
            },
            body: JSON.stringify({
                title: "Conflict",
                detail: "Internal database detail",
            }),
        }),
    );
    await page.goto("/register");
    await page.getByLabel("Full Name").fill("Jane Park");
    await page.getByLabel("Email Address").fill("jane@example.com");
    await page.getByLabel("Password", { exact: true }).fill("ValidPass1!");
    await page.getByLabel("Confirm Password").fill("ValidPass1!");
    await page.getByRole("checkbox").check();
    await page
        .getByRole("button", { name: "Create account", exact: true })
        .click();
    await expect(
        page.getByText(
            "This email address is already registered. Support ID: trace-123",
        ),
    ).toBeVisible();
    await expect(page.getByText("Internal database detail")).toHaveCount(0);
    expect(new URL(page.url()).search).toBe("");
});

test("Registration controls and responsive layout work", async ({
    page,
}, testInfo) => {
    const browserErrors: string[] = [];
    page.on("pageerror", (error) => browserErrors.push(error.message));
    for (const width of [390, 768, 1570]) {
        await page.setViewportSize({
            width,
            height: width === 390 ? 844 : width === 1570 ? 730 : 1024,
        });
        await page.goto("/register");
        await expect(page.locator("html")).toHaveAttribute(
            "data-theme",
            "dark",
        );
        await expect(
            page.getByRole("heading", { name: "Create your account" }),
        ).toBeVisible();
        expect(
            await page.evaluate(
                () => document.documentElement.scrollWidth <= window.innerWidth,
            ),
        ).toBe(true);
        await expect(
            page.getByRole("link", { name: /Log In/ }),
        ).toHaveAttribute("href", "/login");
        await page.screenshot({
            path: testInfo.outputPath(`registration-${width}.png`),
            fullPage: true,
        });
    }
    await page.getByLabel("Password", { exact: true }).fill("secret");
    await page
        .getByRole("button", { name: "Show password", exact: true })
        .click();
    await expect(page.getByLabel("Password", { exact: true })).toHaveAttribute(
        "type",
        "text",
    );
    await page.getByRole("button", { name: "Switch to light mode" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
    await page.screenshot({
        path: testInfo.outputPath("registration-light-1570.png"),
        fullPage: true,
    });
    await expect(
        page.getByRole("link", { name: /Back to Home/ }),
    ).toHaveAttribute("href", "/");
    expect(browserErrors).toEqual([]);
});

for (const osTheme of ["light", "dark"] as const) {
    for (const selectedTheme of ["light", "dark"] as const) {
        test(`Registration explicit ${selectedTheme} theme wins on ${osTheme} OS`, async ({
            page,
        }) => {
            await page.emulateMedia({ colorScheme: osTheme });
            await page.addInitScript(
                (theme) => localStorage.setItem("smart-parking-theme", theme),
                selectedTheme,
            );
            await page.goto("/register");
            await expect(page.locator("html")).toHaveAttribute(
                "data-theme",
                selectedTheme,
            );
            await expect(page.getByTestId("register-page")).toHaveCSS(
                "background-color",
                selectedTheme === "dark"
                    ? "rgb(8, 6, 17)"
                    : "rgb(248, 249, 252)",
            );
        });
    }
}
