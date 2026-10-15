import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseResetPasswordPage } from "./ParkWiseResetPasswordPage";
import { resetPassword } from "./changePassword";

vi.mock("next/navigation", () => ({ useRouter: () => ({ replace: vi.fn() }) }));
vi.mock("./ParkWiseAuthLayout", () => ({
    ParkWiseAuthLayout: ({ children }: { children: ReactNode }) => (
        <div>{children}</div>
    ),
}));
vi.mock("./ParkWiseRecoveryAside", () => ({
    ParkWiseRecoveryAside: () => null,
}));
vi.mock("./changePassword", () => ({ resetPassword: vi.fn() }));

const resetMock = vi.mocked(resetPassword);

describe("ParkWise Reset Password", () => {
    beforeEach(() => resetMock.mockReset());

    it("requires email, six ASCII digits, and a policy-compliant password", async () => {
        render(
            <ParkWiseResetPasswordPage fontClassName="test-font" email="" />,
        );
        fireEvent.click(
            screen.getByRole("button", { name: "Change Password" }),
        );
        expect(
            await screen.findByText("Email is required"),
        ).toBeInTheDocument();
        expect(
            screen.getByText("Enter the six-digit code from your email"),
        ).toBeInTheDocument();
        expect(resetMock).not.toHaveBeenCalled();
    });

    it("sends email, OTP and new password and shows success", async () => {
        resetMock.mockResolvedValue({ message: "Done" });
        render(
            <ParkWiseResetPasswordPage
                fontClassName="test-font"
                email="driver@example.com"
            />,
        );
        fireEvent.change(screen.getByLabelText("Six-digit code"), {
            target: { value: "012345" },
        });
        fireEvent.change(screen.getByLabelText("New Password"), {
            target: { value: "Valid123!" },
        });
        fireEvent.change(screen.getByLabelText("Confirm New Password"), {
            target: { value: "Valid123!" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Change Password" }),
        );
        expect(
            await screen.findByRole("heading", {
                name: "Password changed successfully!",
            }),
        ).toBeInTheDocument();
        expect(resetMock).toHaveBeenCalledWith(
            "driver@example.com",
            "012345",
            "Valid123!",
        );
    });
});
