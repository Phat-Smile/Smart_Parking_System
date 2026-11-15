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
        expect(
            screen.queryByLabelText(/Phone Number/i),
        ).not.toBeInTheDocument();

        // Only Email, Password, Confirm Password
        expect(
            screen.getByPlaceholderText(/Enter your email/i),
        ).toBeInTheDocument();
        expect(screen.getByPlaceholderText(/^Mật khẩu$/i)).toBeInTheDocument();
        expect(
            screen.getByPlaceholderText(/Nhập lại mật khẩu/i),
        ).toBeInTheDocument();
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

        await user.type(
            screen.getByPlaceholderText(/Enter your email/i),
            "hung.le@gmail.com",
        );
        await user.type(
            screen.getByPlaceholderText(/^Mật khẩu$/i),
            "Passw0rd!Secure",
        );
        await user.type(
            screen.getByPlaceholderText(/Nhập lại mật khẩu/i),
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

describe("ParkWiseSignUpPage - Q-FORM text password toggle and real-time mismatch feedback", () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it("renders text toggle buttons 'Hiện mật khẩu' with touch target >= 44px", () => {
        render(<ParkWiseSignUpPage fontClassName="font-sans" />);

        const toggleButtons = screen.getAllByRole("button", {
            name: /Hiện mật khẩu/i,
        });
        expect(toggleButtons).toHaveLength(2);

        toggleButtons.forEach((btn) => {
            expect(btn).toHaveAttribute("type", "button");
            expect(btn).toHaveTextContent("Hiện mật khẩu");
            // Check style touch target
            expect(btn).toHaveStyle({ minHeight: "44px" });
        });
    });

    it("toggles password visibility and updates button text to 'Ẩn mật khẩu'", async () => {
        const user = userEvent.setup();
        render(<ParkWiseSignUpPage fontClassName="font-sans" />);

        const passwordInput = screen.getByPlaceholderText(/^Mật khẩu$/i);
        const [passwordToggle] = screen.getAllByRole("button", {
            name: /Hiện mật khẩu/i,
        });

        expect(passwordInput).toHaveAttribute("type", "password");
        expect(passwordToggle).toHaveTextContent("Hiện mật khẩu");

        // Click to show password
        await user.click(passwordToggle);
        expect(passwordInput).toHaveAttribute("type", "text");
        expect(passwordToggle).toHaveTextContent("Ẩn mật khẩu");

        // Click to hide password
        await user.click(passwordToggle);
        expect(passwordInput).toHaveAttribute("type", "password");
        expect(passwordToggle).toHaveTextContent("Hiện mật khẩu");
    });

    it("shows real-time 'không khớp' feedback immediately as user types in confirm password", async () => {
        const user = userEvent.setup();
        render(<ParkWiseSignUpPage fontClassName="font-sans" />);

        const passwordInput = screen.getByPlaceholderText(/^Mật khẩu$/i);
        const confirmPasswordInput =
            screen.getByPlaceholderText(/Nhập lại mật khẩu/i);

        // Before typing confirm password, no mismatch feedback is shown
        expect(screen.queryByText(/không khớp/i)).not.toBeInTheDocument();

        // Type password
        await user.type(passwordInput, "Secret123!");

        // Type mismatch into confirm password
        await user.type(confirmPasswordInput, "SecretMismatch");

        // Real-time mismatch feedback should appear without submitting form
        expect(await screen.findByText(/không khớp/i)).toBeInTheDocument();
        expect(screen.getByText("Mật khẩu không khớp")).toBeInTheDocument();

        // Clear and type exact match
        await user.clear(confirmPasswordInput);
        await user.type(confirmPasswordInput, "Secret123!");

        // Real-time mismatch feedback should disappear
        await waitFor(() => {
            expect(screen.queryByText(/không khớp/i)).not.toBeInTheDocument();
        });
        expect(screen.getByText("Mật khẩu đã khớp")).toBeInTheDocument();
    });
});
