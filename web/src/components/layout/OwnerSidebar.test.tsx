import { render, screen } from "@testing-library/react";
import { describe, it, expect, beforeEach, vi } from "vitest";
import { OwnerSidebar } from "./OwnerSidebar";
import * as modeModule from "@/features/owner/mode/useOwnerViewMode";

vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
    usePathname: () => "/owner/pricing",
}));

describe("OwnerSidebar mode isolation", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        window.sessionStorage.clear();
    });

    it("points 'Tổng quan' to /owner/dashboard in live mode", () => {
        vi.spyOn(modeModule, "useOwnerViewMode").mockReturnValue(["live", vi.fn()]);

        render(<OwnerSidebar />);

        const overviewLink = screen.getByRole("link", { name: /Tổng quan/i });
        expect(overviewLink).toHaveAttribute("href", "/owner/dashboard");

        const pricingLink = screen.getByRole("link", { name: /Bảng giá & Cọc/i });
        expect(pricingLink).toHaveAttribute("href", "/owner/pricing");
    });

    it("points 'Tổng quan' to /owner/simulation when in sample mode", () => {
        vi.spyOn(modeModule, "useOwnerViewMode").mockReturnValue(["sample", vi.fn()]);

        render(<OwnerSidebar />);

        const overviewLink = screen.getByRole("link", { name: /Tổng quan/i });
        expect(overviewLink).toHaveAttribute("href", "/owner/simulation");

        const pricingLink = screen.getByRole("link", { name: /Bảng giá & Cọc/i });
        expect(pricingLink).toHaveAttribute("href", "/owner/pricing?mode=sample");

        expect(screen.getByText(/🟡 Đang Xem Mẫu/i)).toBeInTheDocument();
        expect(screen.getByRole("button", { name: /Về Dashboard chính →/i })).toBeInTheDocument();
    });
});
