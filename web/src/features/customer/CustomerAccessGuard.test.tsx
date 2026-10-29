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

    it("shows Customer navigation and content to guests during UI preview", () => {
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
        expect(screen.getByRole("link", { name: "Login" })).toBeInTheDocument();
        expect(screen.getByText("Customer content")).toBeInTheDocument();
        expect(replace).not.toHaveBeenCalled();
    });

    it.each(["Staff", "ParkingLotOwner", "SystemAdmin"] as const)(
        "shows Customer UI to %s during preview",
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
                screen.getByRole("link", { name: "Customer" }),
            ).toHaveAttribute("href", "/customer/dashboard");
            expect(screen.getByText("Customer content")).toBeInTheDocument();
            expect(replace).not.toHaveBeenCalled();
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
