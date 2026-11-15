import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseSignUpPage } from "./ParkWiseSignUpPage";
import * as registerApi from "./registerParkWiseAccount";
import * as sessionModule from "./authSession";
import * as pendingVerificationModule from "./pendingVerification";

const mockPush = vi.fn();
const mockReplace = vi.fn();

vi.mock("next/navigation", () => ({
    useRouter: () => ({
        push: mockPush,
        replace: mockReplace,
    }),
}));

describe("ParkWiseSignUpPage - FORM-1 simplified registration", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it("does not render first name, last name, or phone number fields", () => {
        render(<ParkWiseSignUpPage fontClassName="font-sans" />);

        expect(screen.queryByLabelText(/First Name/i)).not.toBeInTheDocument();
        expect(screen.queryByLabelText(/Last Name/i)).not.toBeInTheDocument();
        expect(screen.queryByLabelText(/Phone Number/i)).not.toBeInTheDocument();

        // Only Email, Password, Confirm Password
        expect(screen.getByPlaceholderText(/Enter your email/i)).toBeInTheDocument();
        expect(screen.getByPlaceholderText(/^Password$/i)).toBeInTheDocument();
        expect(screen.getByPlaceholderText(/Confirm password/i)).toBeInTheDocument();
    });

    it("extracts email prefix as fallback display name when registering", async () => {
        const user = userEvent.setup();
        const registerSpy = vi
            .spyOn(registerApi, "registerParkWiseAccount")
            .mockResolvedValue({
                accountId: "acc-123",
                email: "hung.le@gmail.com",
                fullName: "hung.le",
                status: "PendingEmailVerification",
                message: "Registered successfully",
            });

        const rememberSpy = vi.spyOn(
            sessionModule,
            "rememberRegisteredProfileName",
        );
        vi.spyOn(
            pendingVerificationModule,
            "rememberPendingVerification",
        ).mockReturnValue(true);

        render(<ParkWiseSignUpPage fontClassName="font-sans" />);

        await user.type(screen.getByPlaceholderText(/Enter your email/i), "hung.le@gmail.com");
        await user.type(screen.getByPlaceholderText(/^Password$/i), "Passw0rd!Secure");
        await user.type(
            screen.getByPlaceholderText(/Confirm password/i),
            "Passw0rd!Secure",
        );

        await user.click(
            screen.getByRole("button", { name: /Create Account/i }),
        );

        await waitFor(() => {
            expect(registerSpy).toHaveBeenCalledWith({
                email: "hung.le@gmail.com",
                fullName: "hung.le",
                password: "Passw0rd!Secure",
                phoneNumber: null,
            });
            expect(rememberSpy).toHaveBeenCalledWith(
                "hung.le@gmail.com",
                "hung.le",
            );
            expect(mockPush).toHaveBeenCalledWith("/verify-otp");
        });
    });
});
