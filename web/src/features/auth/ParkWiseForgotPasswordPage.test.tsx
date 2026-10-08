import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseForgotPasswordPage } from "./ParkWiseForgotPasswordPage";
import { requestPasswordReset } from "./requestPasswordReset";

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
    beforeEach(() => requestMock.mockReset());

    it("requires a valid email without sending a request", async () => {
        render(<ParkWiseForgotPasswordPage fontClassName="test-font" />);
        fireEvent.click(
            screen.getByRole("button", { name: "Send Reset Link" }),
        );
        expect(
            await screen.findByText("Email is required"),
        ).toBeInTheDocument();
        expect(requestMock).not.toHaveBeenCalled();

        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "invalid" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Send Reset Link" }),
        );
        expect(
            await screen.findByText("Enter a valid email address"),
        ).toBeInTheDocument();
        expect(requestMock).not.toHaveBeenCalled();
    });

    it("shows Check your email only after the gateway request succeeds", async () => {
        requestMock.mockResolvedValue({ message: "Accepted" });
        render(<ParkWiseForgotPasswordPage fontClassName="test-font" />);
        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "driver@example.com" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Send Reset Link" }),
        );
        expect(
            await screen.findByRole("heading", { name: "Check your email" }),
        ).toBeInTheDocument();
        expect(requestMock).toHaveBeenCalledWith("driver@example.com");
        fireEvent.click(screen.getByRole("button", { name: "Resend link" }));
        await waitFor(() => expect(requestMock).toHaveBeenCalledTimes(2));
        expect(
            await screen.findByText(
                "If an account exists, a new link is on its way.",
            ),
        ).toBeInTheDocument();
    });
});
