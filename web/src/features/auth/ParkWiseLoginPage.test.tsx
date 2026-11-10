import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseLoginPage } from "./ParkWiseLoginPage";
import { completeMfaLogin, loginParkWiseAccount } from "./loginParkWiseAccount";
import { googleLoginParkWiseAccount } from "./googleSignIn";
import { ApiError } from "@/shared/errors/api-error";

const { push, getSearchParam } = vi.hoisted(() => ({
    push: vi.fn(),
    getSearchParam: vi.fn<(param: string) => string | null>(() => null),
}));

vi.mock("next/navigation", () => ({
    useRouter: () => ({ push }),
    useSearchParams: () => ({
        get: (param: string) => getSearchParam(param),
    }),
}));

vi.mock("./loginParkWiseAccount", () => ({
    loginParkWiseAccount: vi.fn(),
    completeMfaLogin: vi.fn(),
}));
const loginMock = vi.mocked(loginParkWiseAccount);
const completeMfaLoginMock = vi.mocked(completeMfaLogin);

vi.mock("./googleSignIn", async (importOriginal) => ({
    ...(await importOriginal<typeof import("./googleSignIn")>()),
    googleLoginParkWiseAccount: vi.fn(),
}));
const googleLoginMock = vi.mocked(googleLoginParkWiseAccount);

// Stands in for Google's popup: clicking hands back an ID token.
vi.mock("./GoogleSignInButton", () => ({
    GoogleSignInButton: ({
        onCredential,
    }: {
        onCredential: (token: string) => void;
    }) => (
        <button type="button" onClick={() => onCredential("google-id-token")}>
            Google
        </button>
    ),
}));

vi.mock("./ParkWiseAuthLayout", () => ({
    ParkWiseAuthLayout: ({ children }: { children: ReactNode }) => (
        <div>{children}</div>
    ),
}));

describe("ParkWise Login form", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        getSearchParam.mockReturnValue(null);
    });

    it("requires email and password before calling Identity", async () => {
        loginMock.mockResolvedValue({
            id: "account-id",
            email: "driver@example.com",
            phoneNumber: null,
            status: "EmailVerified",
            role: "Customer",
        });
        render(<ParkWiseLoginPage fontClassName="test-font" />);
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));
        expect(
            await screen.findByText("Email is required"),
        ).toBeInTheDocument();
        expect(screen.getByText("Password is required")).toBeInTheDocument();
        expect(loginMock).not.toHaveBeenCalled();

        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "driver@example.com" },
        });
        fireEvent.change(screen.getByLabelText("Password"), {
            target: { value: "ValidPassword123!" },
        });
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));
        await waitFor(() =>
            expect(screen.getByRole("status")).toHaveTextContent(
                "Signed in as driver@example.com",
            ),
        );
        expect(loginMock).toHaveBeenCalledWith(
            "driver@example.com",
            "ValidPassword123!",
        );
        expect(screen.getByLabelText("Password")).toHaveValue("");
    });

    it("redirects SystemAdmin account to /admin/dashboard", async () => {
        loginMock.mockResolvedValue({
            id: "admin-id",
            email: "admin@parking.test",
            phoneNumber: null,
            status: "Active",
            role: "SystemAdmin",
        });
        render(<ParkWiseLoginPage fontClassName="test-font" />);
        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "admin@parking.test" },
        });
        fireEvent.change(screen.getByLabelText("Password"), {
            target: { value: "Test@12345" },
        });
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));

        await waitFor(() => {
            expect(push).toHaveBeenCalledWith("/admin/dashboard");
        });
    });

    it("redirects Staff account to /staff/dashboard", async () => {
        loginMock.mockResolvedValue({
            id: "staff-id",
            email: "staff@parking.test",
            phoneNumber: null,
            status: "Active",
            role: "Staff",
        });
        render(<ParkWiseLoginPage fontClassName="test-font" />);
        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "staff@parking.test" },
        });
        fireEvent.change(screen.getByLabelText("Password"), {
            target: { value: "Test@12345" },
        });
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));

        await waitFor(() => {
            expect(push).toHaveBeenCalledWith("/staff/dashboard");
        });
    });

    it("signs in with the Google ID token", async () => {
        googleLoginMock.mockResolvedValue({
            id: "account-id",
            email: "driver@gmail.com",
            phoneNumber: null,
            status: "EmailVerified",
            role: "Customer",
        });
        render(<ParkWiseLoginPage fontClassName="test-font" />);

        fireEvent.click(screen.getByRole("button", { name: "Google" }));

        await waitFor(() =>
            expect(screen.getByRole("status")).toHaveTextContent(
                "Signed in as driver@gmail.com",
            ),
        );
        expect(googleLoginMock).toHaveBeenCalledWith("google-id-token");
    });

    it("shows why Google sign-in failed", async () => {
        googleLoginMock.mockRejectedValue(
            new ApiError(
                401,
                { errorCode: "identity.invalid_google_token" } as never,
                undefined,
            ),
        );
        render(<ParkWiseLoginPage fontClassName="test-font" />);

        fireEvent.click(screen.getByRole("button", { name: "Google" }));

        expect(await screen.findByRole("alert")).toHaveTextContent(
            "Google sign-in could not be verified. Please try again.",
        );
    });

    it("points a Google-only account to Google sign-in", async () => {
        loginMock.mockRejectedValue(
            new ApiError(
                401,
                { errorCode: "identity.password_not_set" } as never,
                undefined,
            ),
        );
        render(<ParkWiseLoginPage fontClassName="test-font" />);
        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "driver@gmail.com" },
        });
        fireEvent.change(screen.getByLabelText("Password"), {
            target: { value: "AnyPassword1!" },
        });
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));

        expect(await screen.findByRole("alert")).toHaveTextContent(
            'This account was created with Google and has no password yet. Use "Continue with Google"',
        );
    });

    describe("Two-Factor Authentication (TOTP MFA)", () => {
        it("switches to MFA view when login returns mfaRequired", async () => {
            loginMock.mockResolvedValue({
                mfaRequired: true,
                mfaToken: "mfa-step2-token",
            });
            render(<ParkWiseLoginPage fontClassName="test-font" />);

            fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
                target: { value: "admin@parkwise.vn" },
            });
            fireEvent.change(screen.getByLabelText("Password"), {
                target: { value: "AdminSecurePass!1" },
            });
            fireEvent.click(screen.getByRole("button", { name: "Log in" }));

            expect(
                await screen.findByRole("heading", {
                    name: "Two-factor authentication",
                }),
            ).toBeInTheDocument();
            expect(
                screen.getByText(
                    "Enter the 6-digit code from your authenticator app.",
                ),
            ).toBeInTheDocument();
            expect(
                screen.getByRole("button", { name: "Verify and log in" }),
            ).toBeDisabled();
            expect(
                screen.getByRole("button", { name: "Back to login" }),
            ).toBeInTheDocument();
        });

        it("completes MFA login and navigates admin to destination", async () => {
            loginMock.mockResolvedValue({
                mfaRequired: true,
                mfaToken: "mfa-step2-token",
            });
            completeMfaLoginMock.mockResolvedValue({
                id: "admin-id",
                email: "admin@parkwise.vn",
                role: "SystemAdmin",
                status: "Active",
                phoneNumber: null,
            });

            render(<ParkWiseLoginPage fontClassName="test-font" />);
            fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
                target: { value: "admin@parkwise.vn" },
            });
            fireEvent.change(screen.getByLabelText("Password"), {
                target: { value: "AdminSecurePass!1" },
            });
            fireEvent.click(screen.getByRole("button", { name: "Log in" }));

            await screen.findByRole("heading", {
                name: "Two-factor authentication",
            });

            // Enter 6 digits
            for (let i = 1; i <= 6; i++) {
                const input = screen.getByLabelText(`Digit ${i} of 6`);
                fireEvent.change(input, { target: { value: String(i) } });
            }

            await waitFor(() => {
                expect(completeMfaLoginMock).toHaveBeenCalledWith(
                    "mfa-step2-token",
                    "123456",
                );
            });

            await waitFor(() => {
                expect(push).toHaveBeenCalledWith("/admin/dashboard");
            });
        });

        it("redirects Admin to /admin/owner-applications when explicit redirect query is provided", async () => {
            getSearchParam.mockImplementation((param) =>
                param === "redirect" ? "/admin/owner-applications" : null,
            );
            loginMock.mockResolvedValue({
                mfaRequired: true,
                mfaToken: "mfa-admin-token",
            });
            completeMfaLoginMock.mockResolvedValue({
                id: "admin-id",
                email: "admin@parkwise.vn",
                role: "SystemAdmin",
                status: "Active",
                phoneNumber: null,
            });

            render(<ParkWiseLoginPage fontClassName="test-font" />);
            fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
                target: { value: "admin@parkwise.vn" },
            });
            fireEvent.change(screen.getByLabelText("Password"), {
                target: { value: "AdminSecurePass!1" },
            });
            fireEvent.click(screen.getByRole("button", { name: "Log in" }));

            await screen.findByRole("heading", {
                name: "Two-factor authentication",
            });

            for (let i = 1; i <= 6; i++) {
                const input = screen.getByLabelText(`Digit ${i} of 6`);
                fireEvent.change(input, { target: { value: String(i) } });
            }

            await waitFor(() => {
                expect(completeMfaLoginMock).toHaveBeenCalledWith(
                    "mfa-admin-token",
                    "123456",
                );
            });

            await waitFor(() => {
                expect(push).toHaveBeenCalledWith("/admin/owner-applications");
            });
        });

        it("displays error message on invalid MFA OTP code", async () => {
            loginMock.mockResolvedValue({
                mfaRequired: true,
                mfaToken: "mfa-step2-token",
            });
            completeMfaLoginMock.mockRejectedValue(
                new ApiError(
                    401,
                    { errorCode: "identity.invalid_credentials" } as never,
                    undefined,
                ),
            );

            render(<ParkWiseLoginPage fontClassName="test-font" />);
            fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
                target: { value: "admin@parkwise.vn" },
            });
            fireEvent.change(screen.getByLabelText("Password"), {
                target: { value: "AdminSecurePass!1" },
            });
            fireEvent.click(screen.getByRole("button", { name: "Log in" }));

            await screen.findByRole("heading", {
                name: "Two-factor authentication",
            });

            for (let i = 1; i <= 6; i++) {
                const input = screen.getByLabelText(`Digit ${i} of 6`);
                fireEvent.change(input, { target: { value: "9" } });
            }

            const alert = await screen.findByRole("alert");
            expect(alert).toHaveTextContent(
                "Invalid verification code. Please check your app and try again.",
            );
            expect(alert).toHaveClass("parkwise-login-error");
        });

        it("allows user to return to login form via Back to login button", async () => {
            loginMock.mockResolvedValue({
                mfaRequired: true,
                mfaToken: "mfa-step2-token",
            });

            render(<ParkWiseLoginPage fontClassName="test-font" />);
            fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
                target: { value: "admin@parkwise.vn" },
            });
            fireEvent.change(screen.getByLabelText("Password"), {
                target: { value: "AdminSecurePass!1" },
            });
            fireEvent.click(screen.getByRole("button", { name: "Log in" }));

            await screen.findByRole("heading", {
                name: "Two-factor authentication",
            });

            fireEvent.click(
                screen.getByRole("button", { name: "Back to login" }),
            );

            expect(
                await screen.findByRole("heading", {
                    name: "Log in to your account",
                }),
            ).toBeInTheDocument();
            expect(
                screen.getByRole("button", { name: "Log in" }),
            ).toBeInTheDocument();
        });
    });
});
