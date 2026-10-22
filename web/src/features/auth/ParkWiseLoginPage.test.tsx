import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseLoginPage } from "./ParkWiseLoginPage";
import { loginParkWiseAccount } from "./loginParkWiseAccount";
import { googleLoginParkWiseAccount } from "./googleSignIn";
import { ApiError } from "@/shared/errors/api-error";

const { push, replace } = vi.hoisted(() => ({
    push: vi.fn(),
    replace: vi.fn(),
}));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push, replace }) }));

vi.mock("./loginParkWiseAccount", () => ({ loginParkWiseAccount: vi.fn() }));
const loginMock = vi.mocked(loginParkWiseAccount);

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
    beforeEach(() => vi.clearAllMocks());

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
            expect(replace).toHaveBeenCalledWith("/customer/complete-profile"),
        );
        expect(loginMock).toHaveBeenCalledWith(
            "driver@example.com",
            "ValidPassword123!",
        );
        expect(screen.getByLabelText("Password")).toHaveValue("");
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
            expect(replace).toHaveBeenCalledWith("/customer/complete-profile"),
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

    it("shows a clean invalid-credentials message without a support ID", async () => {
        loginMock.mockRejectedValue(
            new ApiError(
                401,
                { errorCode: "identity.invalid_credentials" } as never,
                "internal-correlation-id",
            ),
        );
        render(<ParkWiseLoginPage fontClassName="test-font" />);
        fireEvent.change(screen.getByRole("textbox", { name: "Email" }), {
            target: { value: "driver@example.com" },
        });
        fireEvent.change(screen.getByLabelText("Password"), {
            target: { value: "WrongPassword1!" },
        });
        fireEvent.click(screen.getByRole("button", { name: "Log in" }));

        expect(await screen.findByRole("alert")).toHaveTextContent(
            "Email or password is incorrect.",
        );
        expect(screen.getByRole("alert")).not.toHaveTextContent("Support ID");
    });
});
