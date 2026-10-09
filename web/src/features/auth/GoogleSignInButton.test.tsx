import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it, vi } from "vitest";
import { GoogleSignInButton } from "./GoogleSignInButton";

// next/script does not load anything in jsdom: run onReady at once, as when the script is already cached.
vi.mock("next/script", () => ({
    default: ({ onReady }: { onReady?: () => void; children?: ReactNode }) => {
        onReady?.();
        return null;
    },
}));

describe("GoogleSignInButton", () => {
    it("keeps the ParkWise button and reports unavailable without a client ID", () => {
        vi.stubEnv("NEXT_PUBLIC_GOOGLE_CLIENT_ID", "");
        const onUnavailable = vi.fn();
        render(
            <GoogleSignInButton
                text="continue_with"
                onCredential={vi.fn()}
                onUnavailable={onUnavailable}
            />,
        );

        fireEvent.click(
            screen.getByRole("button", { name: /Continue with Google/ }),
        );

        expect(onUnavailable).toHaveBeenCalledOnce();
        expect(screen.queryByTestId("google-sign-in")).not.toBeInTheDocument();
    });

    it("renders Google's button and forwards the ID token", () => {
        vi.stubEnv(
            "NEXT_PUBLIC_GOOGLE_CLIENT_ID",
            "client-id.apps.googleusercontent.com",
        );
        let callback: ((response: { credential: string }) => void) | undefined;
        const initialize = vi.fn(
            (configuration: { callback: typeof callback }) => {
                callback = configuration.callback;
            },
        );
        const renderButton = vi.fn();
        vi.stubGlobal("google", {
            accounts: { id: { initialize, renderButton } },
        });
        const onCredential = vi.fn();

        render(
            <GoogleSignInButton
                text="signup_with"
                onCredential={onCredential}
                onUnavailable={vi.fn()}
            />,
        );
        callback?.({ credential: "google-id-token" });

        expect(initialize).toHaveBeenCalledWith(
            expect.objectContaining({
                client_id: "client-id.apps.googleusercontent.com",
                ux_mode: "popup",
            }),
        );
        expect(renderButton).toHaveBeenCalledWith(
            screen.getByTestId("google-sign-in"),
            expect.objectContaining({ text: "signup_with", width: 200 }),
        );
        expect(onCredential).toHaveBeenCalledWith("google-id-token");
    });
});
