import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { RegisterForm } from "./RegisterForm";
import { registerAccount } from "./registerApi";

vi.mock("./registerApi", () => ({ registerAccount: vi.fn() }));

const mockedRegister = vi.mocked(registerAccount);

beforeEach(() => mockedRegister.mockReset());

describe("Registration form", () => {
    it("renders accessible fields and Login navigation", () => {
        render(<RegisterForm />);
        expect(screen.getByLabelText("Full Name")).toBeInTheDocument();
        expect(screen.getByLabelText("Email Address")).toBeInTheDocument();
        expect(
            screen.getByLabelText("Password", { exact: true }),
        ).toBeInTheDocument();
        expect(screen.getByLabelText("Confirm Password")).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /Log In/i })).toHaveAttribute(
            "href",
            "/login",
        );
    });

    it("blocks empty and mismatched data without an API call", async () => {
        const user = userEvent.setup();
        render(<RegisterForm />);
        await user.click(
            screen.getByRole("button", { name: /Create account/i }),
        );
        expect(await screen.findByText("Full name is required")).toBeVisible();
        expect(screen.getByText("Email is required")).toBeVisible();
        expect(screen.getByText("Password is required")).toBeVisible();
        expect(mockedRegister).not.toHaveBeenCalled();

        await user.type(screen.getByLabelText("Full Name"), "Jane Park");
        await user.type(screen.getByLabelText("Email Address"), "not-an-email");
        await user.type(
            screen.getByLabelText("Password", { exact: true }),
            "ValidPass1!",
        );
        await user.type(
            screen.getByLabelText("Confirm Password"),
            "OtherPass1!",
        );
        await user.click(screen.getByRole("checkbox"));
        await user.click(
            screen.getByRole("button", { name: /Create account/i }),
        );
        expect(
            await screen.findByText("Enter a valid email address"),
        ).toBeVisible();
        expect(await screen.findByText("Passwords do not match")).toBeVisible();
        expect(mockedRegister).not.toHaveBeenCalled();
    });

    it("submits only the supported backend fields, never credentials in the URL", async () => {
        const user = userEvent.setup();
        mockedRegister.mockResolvedValue({
            accountId: "id-1",
            email: "jane@example.com",
            status: "PendingEmailVerification",
        });
        window.history.replaceState({}, "", "/register");
        render(<RegisterForm />);
        await user.type(screen.getByLabelText("Full Name"), "Jane Park");
        await user.type(
            screen.getByLabelText("Email Address"),
            "jane@example.com",
        );
        await user.type(
            screen.getByLabelText("Password", { exact: true }),
            "ValidPass1!",
        );
        await user.type(
            screen.getByLabelText("Confirm Password"),
            "ValidPass1!",
        );
        await user.click(screen.getByRole("checkbox"));
        await user.click(
            screen.getByRole("button", { name: /Create account/i }),
        );
        await waitFor(() =>
            expect(mockedRegister).toHaveBeenCalledWith({
                email: "jane@example.com",
                password: "ValidPass1!",
                phoneNumber: null,
            }),
        );
        expect(window.location.pathname).toBe("/register");
        expect(window.location.search).toBe("");
        expect(
            await screen.findByText(/Email verification is pending/),
        ).toBeVisible();
    });

    it("toggles password visibility without submitting", () => {
        render(<RegisterForm />);
        const password = screen.getByLabelText("Password", { exact: true });
        expect(password).toHaveAttribute("type", "password");
        fireEvent.click(screen.getByRole("button", { name: "Show password" }));
        expect(password).toHaveAttribute("type", "text");
        expect(mockedRegister).not.toHaveBeenCalled();
    });

    it("shows the Google integration as pending without authenticating", () => {
        render(<RegisterForm />);
        fireEvent.click(
            screen.getByRole("button", { name: /Continue with Google/ }),
        );
        expect(
            screen.getByText("Google sign-up is not connected yet."),
        ).toBeVisible();
        expect(mockedRegister).not.toHaveBeenCalled();
    });
});
