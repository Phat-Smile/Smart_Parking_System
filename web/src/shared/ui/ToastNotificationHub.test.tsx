import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { renderToString } from "react-dom/server";
import { render, screen, fireEvent } from "@testing-library/react";
import {
    ToastNotificationHub,
    PUSH_PROMPT_SESSION_KEY,
} from "./ToastNotificationHub";

afterEach(() => {
    vi.unstubAllGlobals();
    try {
        window.sessionStorage?.clear();
    } catch {
        // Ignore in environments without window
    }
});

describe("ToastNotificationHub hydration", () => {
    it("renders the same initial HTML on the server and a browser with Push support", () => {
        vi.stubGlobal("window", undefined);
        const serverHtml = renderToString(<ToastNotificationHub />);
        vi.unstubAllGlobals();

        vi.stubGlobal("Notification", { permission: "default" });
        const browserHtml = renderToString(<ToastNotificationHub />);

        expect(browserHtml).toBe(serverHtml);
    });
});

describe("ToastNotificationHub responsive positioning and session persistence", () => {
    beforeEach(() => {
        try {
            window.sessionStorage?.clear();
        } catch {
            // Ignore
        }
        vi.stubGlobal("Notification", {
            permission: "default",
            requestPermission: vi.fn().mockResolvedValue("granted"),
        });
    });

    it("places notification prompt at the top on small screens (< 640px) to keep bottom form controls clear", () => {
        render(<ToastNotificationHub />);

        const prompt = screen.getByTestId("push-notification-prompt");
        expect(prompt).toBeInTheDocument();

        // Must have mobile top positioning classes so it doesn't occlude bottom form buttons
        expect(prompt.className).toContain("top-16");
        expect(prompt.className).toContain("left-2");
        expect(prompt.className).toContain("right-2");
        // On desktop (sm breakpoint), it repositions to bottom-4 right-4
        expect(prompt.className).toContain("sm:top-auto");
        expect(prompt.className).toContain("sm:bottom-4");
        expect(prompt.className).toContain("sm:right-4");
    });

    it("dismisses prompt for the whole session when clicking 'Later' button", () => {
        render(<ToastNotificationHub />);

        const prompt = screen.getByTestId("push-notification-prompt");
        expect(prompt).toBeInTheDocument();

        const laterButton = screen.getByRole("button", {
            name: /Later|Để sau/i,
        });
        fireEvent.click(laterButton);

        // Saved in sessionStorage
        expect(window.sessionStorage.getItem(PUSH_PROMPT_SESSION_KEY)).toBe(
            "true",
        );

        // Removed from UI
        expect(
            screen.queryByTestId("push-notification-prompt"),
        ).not.toBeInTheDocument();
    });

    it("dismisses prompt for the whole session when clicking close [X] button", () => {
        render(<ToastNotificationHub />);

        const closeButton = screen.getByRole("button", {
            name: /Dismiss prompt|Đóng gợi ý thông báo/i,
        });
        fireEvent.click(closeButton);

        expect(window.sessionStorage.getItem(PUSH_PROMPT_SESSION_KEY)).toBe(
            "true",
        );
        expect(
            screen.queryByTestId("push-notification-prompt"),
        ).not.toBeInTheDocument();
    });

    it("does not render prompt if previously dismissed in session storage", () => {
        window.sessionStorage.setItem(PUSH_PROMPT_SESSION_KEY, "true");

        render(<ToastNotificationHub />);

        expect(
            screen.queryByTestId("push-notification-prompt"),
        ).not.toBeInTheDocument();
    });
});
