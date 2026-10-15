import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseForgotPasswordPage } from "./ParkWiseForgotPasswordPage";
import { requestPasswordReset } from "./requestPasswordReset";

const pushMock = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: pushMock }) }));

vi.mock("./ParkWiseAuthLayout", () => ({
    ParkWiseAuthLayout: ({ children }: { children: ReactNode }) => (
        <div>{children}</div>
    ),
}));
vi.mock("./ParkWiseRecoveryAside", () => ({
    ParkWiseRecoveryAside: () => null,
}));
vi.mock("./requestPasswordReset", () => ({ requestPasswordReset: vi.fn() }));

const requestMock = vi.mocked(requestPasswordReset);

describe("ParkWise Forgot Password", () => {
    beforeEach(() => {
        requestMock.mockReset();
        pushMock.mockReset();
        window.sessionStorage.clear();
    });

    it("requires a valid email without sending a request", async () => {
        render(<ParkWiseForgotPasswordPage fontClassName="test-font" />);
        fireEvent.click(
            screen.getByRole("button", { name: "Send Reset Code" }),
        );
        expect(
            await screen.findByText("Email is required"),
        ).toBeInTheDocument();
        expect(requestMock).not.toHaveBeenCalled();

        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "invalid" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Send Reset Code" }),
        );
        expect(
            await screen.findByText("Enter a valid email address"),
        ).toBeInTheDocument();
        expect(requestMock).not.toHaveBeenCalled();
    });

    it("shows Check your email only after the gateway request succeeds", async () => {
        requestMock.mockResolvedValue({
            email: "driver@example.com",
            expiresInSeconds: 300,
            cooldownSeconds: 0,
            message: "Accepted",
        });
        render(<ParkWiseForgotPasswordPage fontClassName="test-font" />);
        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "driver@example.com" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Send Reset Code" }),
        );
        expect(
            await screen.findByRole("heading", { name: "Check your email" }),
        ).toBeInTheDocument();
        expect(requestMock).toHaveBeenCalledWith("driver@example.com");
        expect(
            screen.getByRole("group", { name: "Six-digit code" }),
        ).toBeInTheDocument();
        for (const [index, digit] of [..."012345"].entries()) {
            fireEvent.change(
                screen.getByRole("textbox", {
                    name: `Digit ${index + 1} of 6`,
                }),
                {
                    target: { value: digit },
                },
            );
        }
        fireEvent.click(screen.getByRole("button", { name: "Continue" }));
        expect(pushMock).toHaveBeenCalledWith("/reset-password");
        fireEvent.click(screen.getByRole("button", { name: "Resend code" }));
        await waitFor(() => expect(requestMock).toHaveBeenCalledTimes(2));
        expect(
            await screen.findByText(
                "If an account exists, a new code is on its way.",
            ),
        ).toBeInTheDocument();
    });
});
