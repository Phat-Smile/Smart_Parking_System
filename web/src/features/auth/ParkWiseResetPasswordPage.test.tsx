import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseResetPasswordPage } from "./ParkWiseResetPasswordPage";
import { resetPassword } from "./changePassword";
import { rememberPendingPasswordReset } from "./pendingPasswordReset";
import { ApiError } from "@/shared/errors/api-error";

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
    beforeEach(() => {
        resetMock.mockReset();
        window.sessionStorage.clear();
    });

    it("requires an OTP from the previous step", async () => {
        render(<ParkWiseResetPasswordPage fontClassName="test-font" />);
        expect(
            await screen.findByRole("heading", { name: "Request a new code" }),
        ).toBeInTheDocument();
        expect(resetMock).not.toHaveBeenCalled();
    });

    it("sends email, OTP and new password and shows success", async () => {
        resetMock.mockResolvedValue({ message: "Done" });
        rememberPendingPasswordReset(
            "driver@example.com",
            "012345",
            Date.now() + 300000,
        );
        render(<ParkWiseResetPasswordPage fontClassName="test-font" />);
        await screen.findByRole("heading", { name: "Create a new password" });
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

    it("returns to requesting a code after an incorrect OTP", async () => {
        resetMock.mockRejectedValue(
            new ApiError(
                400,
                { errorCode: "identity.invalid_otp_code" } as never,
                undefined,
            ),
        );
        rememberPendingPasswordReset(
            "driver@example.com",
            "111111",
            Date.now() + 300000,
        );
        render(<ParkWiseResetPasswordPage fontClassName="test-font" />);
        await screen.findByRole("heading", { name: "Create a new password" });
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
            await screen.findByText(
                "Incorrect code. Request a new code and try again.",
            ),
        ).toBeInTheDocument();
        expect(
            window.sessionStorage.getItem("parkwise.pending-password-reset"),
        ).toBeNull();
    });

    it("explains when the new password matches the current password", async () => {
        resetMock.mockRejectedValue(
            new ApiError(
                400,
                { errorCode: "identity.password_same_as_current" } as never,
                undefined,
            ),
        );
        rememberPendingPasswordReset(
            "driver@example.com",
            "012345",
            Date.now() + 300000,
        );
        render(<ParkWiseResetPasswordPage fontClassName="test-font" />);
        await screen.findByRole("heading", { name: "Create a new password" });
        expect(
            screen.queryByRole("link", { name: "Request a new code" }),
        ).not.toBeInTheDocument();
        fireEvent.change(screen.getByLabelText("New Password"), {
            target: { value: "Valid123!" },
        });
        fireEvent.change(screen.getByLabelText("Confirm New Password"), {
            target: { value: "Valid123!" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Change Password" }),
        );

        expect(await screen.findByRole("alert")).toHaveTextContent(
            "Choose a password different from your current one.",
        );
    });
});
