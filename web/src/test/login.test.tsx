import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { LoginPage } from "@/features/auth/LoginPage";
import { LoginForm } from "@/features/auth/LoginForm";

beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove("dark");
    document.documentElement.removeAttribute("data-theme");
});

describe("Login UI before Identity integration", () => {
    it("renders the login page and Stitch content", () => {
        render(<LoginPage />);
        expect(
            screen.getByRole("heading", { name: /Sign in to your account/i }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("navigation", { name: "Main navigation" }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("button", { name: /Continue with Google/i }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: /Forgot password/i }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: /Sign up/i }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("img", { name: /parking structure at dusk/i }),
        ).toBeInTheDocument();
    });

    it("requires a valid email and a password", async () => {
        const user = userEvent.setup();
        render(<LoginForm />);
        await user.click(screen.getByRole("button", { name: "Log In" }));
        expect(
            await screen.findByText("Email is required"),
        ).toBeInTheDocument();
        expect(screen.getByText("Password is required")).toBeInTheDocument();

        await user.type(screen.getByLabelText("Email Address"), "invalid");
        await user.click(screen.getByRole("button", { name: "Log In" }));
        expect(
            await screen.findByText("Enter a valid email address"),
        ).toBeInTheDocument();
    });

    it("toggles password visibility without changing its value", async () => {
        const user = userEvent.setup();
        render(<LoginForm />);
        const password = screen.getByLabelText("Password") as HTMLInputElement;
        await user.type(password, "secret password");
        expect(password).toHaveAttribute("type", "password");
        await user.click(screen.getByRole("button", { name: "Show password" }));
        expect(password).toHaveAttribute("type", "text");
        expect(password).toHaveValue("secret password");
        await user.click(screen.getByRole("button", { name: "Hide password" }));
        expect(password).toHaveAttribute("type", "password");
    });

    it("validates locally and does not request or simulate a session", async () => {
        const user = userEvent.setup();
        const fetchSpy = vi.spyOn(globalThis, "fetch");
        render(<LoginForm />);
        await user.type(
            screen.getByLabelText("Email Address"),
            " user@example.com ",
        );
        await user.type(
            screen.getByLabelText("Password"),
            "unchanged password",
        );
        await user.click(screen.getByRole("button", { name: "Log In" }));
        expect(await screen.findByRole("status")).toHaveTextContent(
            "Sign-in is not available yet",
        );
        expect(fetchSpy).not.toHaveBeenCalled();
        expect(localStorage.length).toBe(0);
    });

    it("keeps Google, password reset, and registration integration pending", async () => {
        const user = userEvent.setup();
        render(<LoginForm />);
        await user.click(
            screen.getByRole("button", { name: /Continue with Google/i }),
        );
        expect(screen.getByRole("status")).toHaveTextContent(
            "Google sign-in is not available yet",
        );
        await user.click(
            screen.getByRole("link", { name: /Forgot password/i }),
        );
        expect(screen.getByRole("status")).toHaveTextContent(
            "Password reset is not available yet",
        );
        await user.click(screen.getByRole("link", { name: /Sign up/i }));
        expect(screen.getByRole("status")).toHaveTextContent(
            "Account registration UI is not available yet",
        );
    });

    it("uses the shared explicit theme selection", async () => {
        localStorage.setItem("smart-parking-theme", "light");
        render(<LoginPage />);
        fireEvent.click(
            screen.getByRole("button", { name: "Switch to dark mode" }),
        );
        await waitFor(() => {
            expect(document.documentElement).toHaveClass("dark");
            expect(document.documentElement.dataset.theme).toBe("dark");
        });
    });
});
