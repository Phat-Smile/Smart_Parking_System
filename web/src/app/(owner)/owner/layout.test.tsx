import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";

const replace = vi.hoisted(() => vi.fn());
const loadOwnerLots = vi.hoisted(() => vi.fn());
const clearAuthSession = vi.hoisted(() => vi.fn());

vi.mock("next/navigation", () => ({
    usePathname: () => "/owner/dashboard",
    useRouter: () => ({ replace }),
}));
vi.mock("next/font/google", () => ({
    Be_Vietnam_Pro: () => ({ className: "font", variable: "font" }),
}));
vi.mock("@/features/owner/lots/ownerLotsSync", () => ({ loadOwnerLots }));
vi.mock("@/features/auth/authSession", () => ({ clearAuthSession }));
vi.mock("../../../components/layout/OwnerSidebar", () => ({
    OwnerSidebar: () => <nav />,
}));

import OwnerRootLayout from "./layout";

beforeEach(() => {
    replace.mockReset();
    loadOwnerLots.mockReset();
    clearAuthSession.mockReset();
});

describe("OwnerRootLayout", () => {
    it("sends the owner to sign in when the lots cannot load because the session expired", async () => {
        loadOwnerLots.mockRejectedValue(new ApiError(401));
        render(<OwnerRootLayout>page</OwnerRootLayout>);
        await waitFor(() => expect(replace).toHaveBeenCalledWith("/login"));
        expect(clearAuthSession).toHaveBeenCalled();
        expect(screen.queryByRole("alert")).toBeNull();
    });

    it("keeps the load error for other failures and stays on the page", async () => {
        loadOwnerLots.mockRejectedValue(new ApiError(500));
        render(<OwnerRootLayout>page</OwnerRootLayout>);
        expect(await screen.findByRole("alert")).toHaveTextContent(
            "Không tải được danh sách bãi",
        );
        expect(replace).not.toHaveBeenCalled();
    });

    it("shows the portal without an error when the lots load", async () => {
        loadOwnerLots.mockResolvedValue([]);
        render(<OwnerRootLayout>page</OwnerRootLayout>);
        await waitFor(() => expect(loadOwnerLots).toHaveBeenCalled());
        expect(screen.getByText("page")).toBeInTheDocument();
        expect(screen.queryByRole("alert")).toBeNull();
    });
});
