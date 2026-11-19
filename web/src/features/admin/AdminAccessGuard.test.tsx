import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AdminAccessGuard } from "./AdminAccessGuard";
import { clearAuthSession, setAuthSession } from "@/features/auth/authSession";
import type { LoginResponse } from "@/features/auth/loginParkWiseAccount";

const { replace } = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({
    useRouter: () => ({ replace }),
    usePathname: () => "/admin/dashboard",
}));

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

describe("AdminAccessGuard", () => {
    beforeEach(() => {
        clearAuthSession();
        replace.mockClear();
    });

    afterEach(() => {
        clearAuthSession();
    });

    it("blocks guests and redirects to /login with returnUrl", () => {
        render(
            <AdminAccessGuard>
                <div>Protected Admin Content</div>
            </AdminAccessGuard>,
        );

        expect(
            screen.queryByText("Protected Admin Content"),
        ).not.toBeInTheDocument();
        expect(
            screen.getByText("Đang chuyển hướng đến trang đăng nhập..."),
        ).toBeInTheDocument();
        expect(replace).toHaveBeenCalledWith(
            "/login?returnUrl=%2Fadmin%2Fdashboard",
        );
    });

    it.each(["Customer", "Staff", "ParkingLotOwner"] as const)(
        "blocks %s and redirects to /403",
        (role) => {
            signIn(role);
            render(
                <AdminAccessGuard>
                    <div>Protected Admin Content</div>
                </AdminAccessGuard>,
            );

            expect(
                screen.queryByText("Protected Admin Content"),
            ).not.toBeInTheDocument();
            expect(replace).toHaveBeenCalledWith("/403");
        },
    );

    it("renders children and does not redirect when user is SystemAdmin", () => {
        signIn("SystemAdmin");
        render(
            <AdminAccessGuard>
                <div>Protected Admin Content</div>
            </AdminAccessGuard>,
        );

        expect(screen.getByText("Protected Admin Content")).toBeInTheDocument();
        expect(replace).not.toHaveBeenCalled();
    });
});

