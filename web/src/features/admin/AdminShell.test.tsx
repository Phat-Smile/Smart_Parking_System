import React from "react";
import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AdminShell } from "./AdminShell";
import { clearAuthSession, setAuthSession } from "@/features/auth/authSession";
import type { LoginResponse } from "@/features/auth/loginParkWiseAccount";

const { mockReplace } = vi.hoisted(() => ({
    mockReplace: vi.fn(),
}));

vi.mock("next/navigation", () => ({
    usePathname: () => "/admin/dashboard",
    useRouter: () => ({
        push: vi.fn(),
        replace: mockReplace,
    }),
}));

vi.mock("next/image", () => ({
    default: ({
        priority: _priority,
        ...props
    }: React.ImgHTMLAttributes<HTMLImageElement> & { priority?: boolean }) => (
        // eslint-disable-next-line @next/next/no-img-element
        <img {...props} alt={props.alt ?? ""} />
    ),
}));

vi.mock("@/shared/i18n/LanguageContext", () => ({
    useLanguage: () => ({
        language: "vi",
        t: (key: string) => key,
    }),
    LanguageSwitcher: () => <div data-testid="language-switcher" />,
}));

vi.mock("./AdminNotifications", () => ({
    AdminNotifications: () => <div data-testid="admin-notifications" />,
}));

vi.mock("./adminLotsData", () => ({
    useAdminLots: () => [],
    countByStatus: () => ({ Pending: 0 }),
}));

vi.mock("./reviewParkingLot", () => ({
    getAdminParkingLots: vi.fn().mockResolvedValue([]),
}));

function signInAdmin(
    role: LoginResponse["account"]["role"] = "SystemAdmin",
) {
    setAuthSession({
        accessToken: "test-admin-token",
        refreshToken: "test-admin-refresh",
        tokenType: "Bearer",
        expiresIn: 60,
        account: {
            id: "admin-1",
            email: "admin@parkwise.local",
            phoneNumber: null,
            status: "Active",
            role,
        },
    });
}

describe("AdminShell Route Guard", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        clearAuthSession();
    });

    afterEach(() => {
        clearAuthSession();
    });

    it("redirects unauthenticated guest to /login and blocks rendering children", () => {
        render(
            <AdminShell>
                <div>Admin Secret Content</div>
            </AdminShell>,
        );

        expect(
            screen.queryByText("Admin Secret Content"),
        ).not.toBeInTheDocument();
        expect(screen.getByText("Checking admin access…")).toBeInTheDocument();
        expect(mockReplace).toHaveBeenCalledWith("/login");
    });

    it.each(["Customer", "Staff", "ParkingLotOwner"] as const)(
        "blocks access and redirects %s to /403",
        (role) => {
            signInAdmin(role);
            render(
                <AdminShell>
                    <div>Admin Secret Content</div>
                </AdminShell>,
            );

            expect(
                screen.queryByText("Admin Secret Content"),
            ).not.toBeInTheDocument();
            expect(
                screen.getByText("Checking admin access…"),
            ).toBeInTheDocument();
            expect(mockReplace).toHaveBeenCalledWith("/403");
        },
    );

    it("allows access and renders children when authenticated as SystemAdmin", () => {
        signInAdmin("SystemAdmin");
        render(
            <AdminShell>
                <div>Admin Secret Content</div>
            </AdminShell>,
        );

        expect(screen.getByText("Admin Secret Content")).toBeInTheDocument();
        expect(mockReplace).not.toHaveBeenCalled();
    });
});
