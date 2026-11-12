import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import OwnerSimulationPage from "./page";
import * as ownerLegalApi from "@/features/owner/api/ownerLegalApi";

vi.mock("next/navigation", () => ({
    useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
    usePathname: () => "/owner/simulation",
}));

describe("OwnerSimulationPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        window.sessionStorage.clear();

        vi.spyOn(ownerLegalApi, "getMyOwnerApplication").mockResolvedValue({
            accountId: "acc-123",
            email: "partner@parkwise.vn",
            status: "Pending",
            taxCode: "0100109106",
            businessName: "Bãi xe Thông Minh An Phú",
            rejectionReason: null,
            reviewedAtUtc: new Date().toISOString(),
            ownerType: "Enterprise",
        });
    });

    it("renders simulation banner and explains sandbox mode", async () => {
        render(<OwnerSimulationPage />);

        expect(
            await screen.findByText(/Màn hình mô phỏng/i),
        ).toBeInTheDocument();
        expect(
            screen.getByText(
                /Môi trường trải nghiệm thử nghiệm dành cho Chủ bãi xe/i,
            ),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("button", { name: /Rollback về mặc định/i }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("button", { name: /Vì sao ở chế độ mô phỏng/i }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: /Xem tiến độ duyệt hồ sơ/i }),
        ).toHaveAttribute("href", "/owner/onboarding");
    });

    it("renders tabs and allows switching to configuration simulator", async () => {
        const user = userEvent.setup();
        render(<OwnerSimulationPage />);

        await screen.findByText(/Màn hình mô phỏng/i);

        const configTab = screen.getByRole("button", {
            name: /Thử nghiệm Cấu hình Bãi xe/i,
        });
        await user.click(configTab);

        expect(
            await screen.findByText(/Cấu hình thông tin bãi đỗ xe mô phỏng/i),
        ).toBeInTheDocument();
        expect(
            screen.getByDisplayValue(/Bãi xe Thông Minh An Phú/i),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("button", { name: /Lưu thử nghiệm cấu hình/i }),
        ).toBeInTheDocument();
    });

    it("allows modifying and saving configuration with sandbox notice, then rolling back", async () => {
        const user = userEvent.setup();
        render(<OwnerSimulationPage />);

        await screen.findByText(/Màn hình mô phỏng/i);

        const configTab = screen.getByRole("button", {
            name: /Thử nghiệm Cấu hình Bãi xe/i,
        });
        await user.click(configTab);

        const nameInput = screen.getByDisplayValue(/Bãi xe Thông Minh An Phú/i);
        await user.clear(nameInput);
        await user.type(nameInput, "Bãi xe Đổi Mới VIP");

        const saveBtn = screen.getByRole("button", {
            name: /Lưu thử nghiệm cấu hình/i,
        });
        await user.click(saveBtn);

        expect(
            await screen.findByText(
                /Đã lưu cấu hình vào phiên mô phỏng tạm thời/i,
            ),
        ).toBeInTheDocument();

        // Rollback resets to initial template
        const rollbackBtn = screen.getByRole("button", {
            name: /Rollback về mặc định/i,
        });
        await user.click(rollbackBtn);

        expect(
            await screen.findByText(/Đã rollback toàn bộ cấu hình/i),
        ).toBeInTheDocument();
        expect(
            screen.getByDisplayValue(/Bãi xe Thông Minh An Phú/i),
        ).toBeInTheDocument();
    });

    it("opens explainer modal when clicking info button", async () => {
        const user = userEvent.setup();
        render(<OwnerSimulationPage />);

        await screen.findByText(/Màn hình mô phỏng/i);

        const infoBtn = screen.getByRole("button", {
            name: /Vì sao ở chế độ mô phỏng/i,
        });
        await user.click(infoBtn);

        expect(
            await screen.findByText(/Cơ chế hoạt động của Màn hình mô phỏng/i),
        ).toBeInTheDocument();
        expect(
            screen.getByText(
                /1. Vì sao chưa thay đổi được vào hệ thống chính thức/i,
            ),
        ).toBeInTheDocument();
        expect(
            screen.getByText(
                /2. Tính cô lập & Không bị ảnh hưởng bởi người khác/i,
            ),
        ).toBeInTheDocument();

        const closeBtn = screen.getByRole("button", {
            name: /Đã hiểu & Đóng/i,
        });
        await user.click(closeBtn);

        await waitFor(() => {
            expect(
                screen.queryByText(/Cơ chế hoạt động của Màn hình mô phỏng/i),
            ).not.toBeInTheDocument();
        });
    });
});
