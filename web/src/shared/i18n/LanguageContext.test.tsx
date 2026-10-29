import { fireEvent, render, screen } from "@testing-library/react";
import React from "react";
import { describe, expect, it } from "vitest";
import {
    LanguageProvider,
    LanguageSwitcher,
    useLanguage,
} from "./LanguageContext";

function TestConsumer() {
    const { language, t, setLanguage } = useLanguage();
    return (
        <div>
            <span data-testid="current-lang">{language}</span>
            <span data-testid="translated-title">{t("parkingLotsTitle")}</span>
            <button type="button" onClick={() => setLanguage("en")}>
                Set EN
            </button>
            <button type="button" onClick={() => setLanguage("vi")}>
                Set VI
            </button>
            <LanguageSwitcher variant="sidebar" />
        </div>
    );
}

describe("LanguageContext & i18n synchronization", () => {
    it("provides default language and translates correctly", () => {
        render(
            <LanguageProvider>
                <TestConsumer />
            </LanguageProvider>,
        );

        expect(screen.getByTestId("current-lang").textContent).toBe("vi");
        expect(screen.getByTestId("translated-title").textContent).toBe(
            "Quản trị bãi đỗ xe",
        );
    });

    it("switches language and synchronizes translated text", () => {
        render(
            <LanguageProvider>
                <TestConsumer />
            </LanguageProvider>,
        );

        const setEnBtn = screen.getByRole("button", { name: "Set EN" });
        fireEvent.click(setEnBtn);

        expect(screen.getByTestId("current-lang").textContent).toBe("en");
        expect(screen.getByTestId("translated-title").textContent).toBe(
            "Parking Lots Management",
        );

        const setViBtn = screen.getByRole("button", { name: "Set VI" });
        fireEvent.click(setViBtn);

        expect(screen.getByTestId("current-lang").textContent).toBe("vi");
        expect(screen.getByTestId("translated-title").textContent).toBe(
            "Quản trị bãi đỗ xe",
        );
    });
});
