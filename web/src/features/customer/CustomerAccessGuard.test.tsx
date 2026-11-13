import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerAccessGuard } from "./CustomerAccessGuard";
import { ParkWiseNavbar } from "@/features/parkwise/ParkWiseNavbar";
import { clearAuthSession, setAuthSession } from "@/features/auth/authSession";
import type { LoginResponse } from "@/features/auth/loginParkWiseAccount";

const { replace } = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace }) }));
vi.mock("next/image", () => ({ default: () => null }));

function signIn(role: LoginResponse["account"]["role"]) {
    setAuthSession({
        accessToken: "test-token",
        refreshToken: "test-refresh-token",
        tokenType: "Bearer",
        expiresIn: 60,
        account: {
            id: "test-id",
            email: "test@example.com",
            phoneNumber: null,
            status: "Active",
            role,
        },
    });
}

describe("Customer navigation and access", () => {
    beforeEach(() => {
        clearAuthSession();
        replace.mockClear();
    });
    afterEach(() => clearAuthSession());

    it("hides Customer navigation and redirects guests to login", () => {
        render(
            <>
                <ParkWiseNavbar />
                <CustomerAccessGuard>
                    <div>Customer content</div>
                </CustomerAccessGuard>
            </>,
        );
        expect(
            screen.queryByRole("link", { name: "Customer" }),
        ).not.toBeInTheDocument();
        expect(screen.getByRole("link", { name: "Login" })).toBeInTheDocument();
        expect(screen.queryByText("Customer content")).not.toBeInTheDocument();
        expect(replace).toHaveBeenCalledWith("/login");
    });

    it.each(["Staff", "ParkingLotOwner", "SystemAdmin"] as const)(
        "blocks Customer UI for %s",
        (role) => {
            signIn(role);
            render(
                <>
                    <ParkWiseNavbar />
                    <CustomerAccessGuard>
                        <div>Customer content</div>
                    </CustomerAccessGuard>
                </>,
            );
            expect(
                screen.queryByRole("link", { name: "Customer" }),
            ).not.toBeInTheDocument();
            expect(
                screen.queryByText("Customer content"),
            ).not.toBeInTheDocument();
            expect(replace).toHaveBeenCalledWith("/403");
        },
    );

    it("shows Customer navigation and content for a customer session", () => {
        signIn("Customer");
        render(
            <>
                <ParkWiseNavbar />
                <CustomerAccessGuard>
                    <div>Customer content</div>
                </CustomerAccessGuard>
            </>,
        );
        expect(screen.getByRole("link", { name: "Customer" })).toHaveAttribute(
            "href",
            "/customer/dashboard",
        );
        expect(
            screen.queryByRole("link", { name: "Login" }),
        ).not.toBeInTheDocument();
        expect(screen.getByText("Customer content")).toBeInTheDocument();
        expect(replace).not.toHaveBeenCalled();
    });
});
