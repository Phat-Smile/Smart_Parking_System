import { fireEvent, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseResetPasswordPage } from "./ParkWiseResetPasswordPage";
import { changePassword } from "./changePassword";

vi.mock("next/navigation", () => ({ useRouter: () => ({ replace: vi.fn() }) }));
vi.mock("./ParkWiseAuthLayout", () => ({
    ParkWiseAuthLayout: ({ children }: { children: ReactNode }) => (
        <div>{children}</div>
    ),
}));
vi.mock("./ParkWiseRecoveryAside", () => ({
    ParkWiseRecoveryAside: () => null,
}));
vi.mock("./changePassword", async (importOriginal) => {
    const original = await importOriginal<typeof import("./changePassword")>();
    return { ...original, changePassword: vi.fn() };
});

const changeMock = vi.mocked(changePassword);

describe("ParkWise Reset Password", () => {
    beforeEach(() => changeMock.mockReset());

    it("requires a link token", () => {
        render(
            <ParkWiseResetPasswordPage fontClassName="test-font" token="" />,
        );
        expect(
            screen.getByRole("heading", { name: "Reset link expired" }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: "Request New Link" }),
        ).toHaveAttribute("href", "/forgot-password");
    });

    it("requires matching policy-compliant passwords", async () => {
        render(
            <ParkWiseResetPasswordPage
                fontClassName="test-font"
                token="server-token"
            />,
        );
        fireEvent.click(
            screen.getByRole("button", { name: "Change Password" }),
        );
        expect(
            await screen.findByText(
                "Password must contain at least 8 characters",
            ),
        ).toBeInTheDocument();
        expect(changeMock).not.toHaveBeenCalled();

        fireEvent.change(screen.getByLabelText("New Password"), {
            target: { value: "Valid123!" },
        });
        fireEvent.change(screen.getByLabelText("Confirm New Password"), {
            target: { value: "Other123!" },
        });
        fireEvent.click(
            screen.getByRole("button", { name: "Change Password" }),
        );
        expect(
            await screen.findByText("The passwords do not match"),
        ).toBeInTheDocument();
        expect(changeMock).not.toHaveBeenCalled();
    });

    it("shows the success state only after a successful response", async () => {
        changeMock.mockResolvedValue(undefined);
        render(
            <ParkWiseResetPasswordPage
                fontClassName="test-font"
                token="server-token"
            />,
        );
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
        expect(changeMock).toHaveBeenCalledWith("server-token", "Valid123!");
        expect(
            screen.getByRole("link", { name: "Go to Login" }),
        ).toHaveAttribute("href", "/login");
    });
});
