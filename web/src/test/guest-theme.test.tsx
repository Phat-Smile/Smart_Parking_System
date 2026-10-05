import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@/shared/ui/theme/ThemeContext";
import { ThemeToggle } from "@/shared/ui/ThemeToggle";

beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove("dark");
    document.documentElement.removeAttribute("data-theme");
});

describe("explicit Guest theme selection", () => {
    it.each([
        { osDark: false, initial: "light", selected: "dark" },
        { osDark: true, initial: "dark", selected: "light" },
    ] as const)(
        "uses $selected even when OS dark preference is $osDark",
        async ({ osDark, initial, selected }) => {
            vi.stubGlobal(
                "matchMedia",
                vi.fn().mockReturnValue({ matches: osDark }),
            );
            localStorage.setItem("smart-parking-theme", initial);

            render(
                <ThemeProvider>
                    <ThemeToggle />
                </ThemeProvider>,
            );

            const toggle = screen.getByRole("button", { name: "Switch theme" });
            await waitFor(() =>
                expect(document.documentElement.dataset.theme).toBe(initial),
            );
            fireEvent.click(toggle);

            await waitFor(() => {
                expect(
                    document.documentElement.classList.contains("dark"),
                ).toBe(selected === "dark");
                expect(document.documentElement.dataset.theme).toBe(selected);
                expect(localStorage.getItem("smart-parking-theme")).toBe(
                    selected,
                );
            });
        },
    );
});
