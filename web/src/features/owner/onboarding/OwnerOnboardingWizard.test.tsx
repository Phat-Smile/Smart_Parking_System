import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OwnerOnboardingWizard } from "./OwnerOnboardingWizard";
import * as ownerLegalApi from "@/features/owner/api/ownerLegalApi";
import { clearAuthSession, setAuthSession } from "@/features/auth/authSession";

const { push, replace, router } = vi.hoisted(() => {
    const push = vi.fn();
    const replace = vi.fn();
    // Next.js returns the same router object on every render.
    return { push, replace, router: { push, replace } };
});
vi.mock("next/navigation", () => ({ useRouter: () => router }));

describe("OwnerOnboardingWizard", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        window.localStorage.clear();
        // The application belongs to the signed-in account.
        setAuthSession({
            accessToken: "access",
            tokenType: "Bearer",
            expiresIn: 3600,
            refreshToken: "refresh",
            account: {
                id: "acc-123",
                email: "partner@parkwise.vn",
                phoneNumber: null,
                status: "EmailVerified",
                role: "Customer",
            },
        });
        vi.spyOn(ownerLegalApi, "updateOwnerType").mockResolvedValue({
            accountId: "acc-123",
            email: "partner@parkwise.vn",
            status: "None",
            taxCode: null,
            businessName: null,
            rejectionReason: null,
            reviewedAtUtc: null,
            ownerType: "Enterprise",
        });

        vi.spyOn(ownerLegalApi, "getMyOwnerApplication").mockResolvedValue({
            accountId: "acc-123",
            email: "partner@parkwise.vn",
            status: "None",
            taxCode: null,
            businessName: null,
            rejectionReason: null,
            reviewedAtUtc: null,
            ownerType: "Enterprise",
        });

        vi.spyOn(
            ownerLegalApi,
            "getOwnerDocumentRequirements",
        ).mockResolvedValue({
            ownerType: "Enterprise",
            documents: [
                {
                    documentType: "EnterpriseRegistration",
                    name: "Giấy chứng nhận đăng ký doanh nghiệp (GPĐKKD)",
                    required: true,
                    acceptedContentTypes: ["application/pdf"],
                    maxSizeBytes: 10485760,
                },
            ],
        });

        vi.spyOn(ownerLegalApi, "getMyOwnerDocuments").mockResolvedValue([]);
    });

    afterEach(() => {
        clearAuthSession();
    });

    it("sends an owner who is not signed in to the login page first", async () => {
        clearAuthSession();
        render(<OwnerOnboardingWizard />);

        await waitFor(() =>
            expect(replace).toHaveBeenCalledWith(
                "/login?redirect=%2Fowner%2Fonboarding",
            ),
        );
        expect(ownerLegalApi.getMyOwnerApplication).not.toHaveBeenCalled();
    });

    it("saves the owner type before the document step and stops when Identity refuses", async () => {
        const user = userEvent.setup();
        vi.mocked(ownerLegalApi.updateOwnerType).mockRejectedValueOnce(
            new Error("offline"),
        );
        render(<OwnerOnboardingWizard />);

        await screen.findByText("1. Chọn hình thức hoạt động");
        await user.type(
            screen.getByLabelText(/Tên Doanh nghiệp/i),
            "Công ty Cổ phần Bãi xe ParkWise",
        );
        await user.type(screen.getByLabelText(/Mã số thuế/i), "0312345678");
        await user.click(
            screen.getByRole("button", {
                name: /Tiếp tục: Tải giấy tờ chứng thực/i,
            }),
        );

        expect(ownerLegalApi.updateOwnerType).toHaveBeenCalledWith(
            "Enterprise",
        );
        expect(
            await screen.findByText(/Không kết nối được máy chủ/i),
        ).toBeInTheDocument();
        expect(
            screen.getByText("1. Chọn hình thức hoạt động"),
        ).toBeInTheDocument();
    });

    it("renders Step 1 with entity type selector and inputs", async () => {
        render(<OwnerOnboardingWizard />);

        expect(
            await screen.findByText("1. Chọn hình thức hoạt động"),
        ).toBeInTheDocument();
        expect(screen.getByText("Doanh nghiệp / Công ty")).toBeInTheDocument();
        expect(screen.getByText("Hộ kinh doanh")).toBeInTheDocument();
        expect(screen.getByText("Cá nhân")).toBeInTheDocument();
        expect(screen.getByLabelText(/Tên Doanh nghiệp/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/Mã số thuế/i)).toBeInTheDocument();
    });

    it("blocks advancing to Step 2 when business name is empty", async () => {
        const user = userEvent.setup();
        render(<OwnerOnboardingWizard />);

        await screen.findByText("1. Chọn hình thức hoạt động");
        await user.click(
            screen.getByRole("button", {
                name: /Tiếp tục: Tải giấy tờ chứng thực/i,
            }),
        );

        expect(
            await screen.findByText(/Vui lòng nhập tên công ty/i),
        ).toBeInTheDocument();
    });

    it("advances to Step 2 when valid entity and tax code are entered", async () => {
        const user = userEvent.setup();
        render(<OwnerOnboardingWizard />);

        await screen.findByText("1. Chọn hình thức hoạt động");
        await user.type(
            screen.getByLabelText(/Tên Doanh nghiệp/i),
            "Công ty Cổ phần Bãi xe ParkWise",
        );
        await user.type(screen.getByLabelText(/Mã số thuế/i), "0312345678");

        await user.click(
            screen.getByRole("button", {
                name: /Tiếp tục: Tải giấy tờ chứng thực/i,
            }),
        );

        expect(
            await screen.findByText("2. Tải lên giấy tờ chứng minh"),
        ).toBeInTheDocument();
        expect(
            screen.getByText(/Giấy chứng nhận đăng ký doanh nghiệp/i),
        ).toBeInTheDocument();
    });

    it("switches to Individual form and renders personal fields", async () => {
        const user = userEvent.setup();
        render(<OwnerOnboardingWizard />);

        await screen.findByText("1. Chọn hình thức hoạt động");
        await user.click(screen.getByText("Cá nhân"));

        expect(
            await screen.findByText(
                /Thông tin chủ sở hữu & Cơ sở bãi xe cá nhân/i,
            ),
        ).toBeInTheDocument();
        expect(screen.getByLabelText(/Tên bãi đỗ xe/i)).toBeInTheDocument();
        expect(
            screen.getByLabelText(/Họ và tên chủ sở hữu/i),
        ).toBeInTheDocument();
        expect(
            screen.getByLabelText(/Số Căn cước công dân/i),
        ).toBeInTheDocument();
    });

    it("looks up tax code via VietQR API and auto-fills business details", async () => {
        const user = userEvent.setup();
        vi.spyOn(ownerLegalApi, "lookupBusinessTaxCode").mockResolvedValue({
            id: "0100109106",
            name: "TẬP ĐOÀN CÔNG NGHIỆP - VIỄN THÔNG QUÂN ĐỘI",
            internationalName: null,
            shortName: null,
            address: "Số 5, Phố Nguyễn Hữu An, Phường Cầu Giấy, TP Hà Nội",
            status: "NNT đang hoạt động",
        });

        render(<OwnerOnboardingWizard />);
        await screen.findByText("1. Chọn hình thức hoạt động");

        const taxInput = screen.getByLabelText(/Mã số thuế Doanh nghiệp/i);
        await user.type(taxInput, "0100109106");

        const lookupBtn = screen.getByRole("button", {
            name: /Tra cứu Doanh nghiệp/i,
        });
        await user.click(lookupBtn);

        const matches = await screen.findAllByText(
            /TẬP ĐOÀN CÔNG NGHIỆP - VIỄN THÔNG QUÂN ĐỘI/i,
        );
        expect(matches.length).toBeGreaterThan(0);
        expect(
            screen.getByDisplayValue(
                "TẬP ĐOÀN CÔNG NGHIỆP - VIỄN THÔNG QUÂN ĐỘI",
            ),
        ).toBeInTheDocument();
        expect(
            screen.getByDisplayValue(
                "Số 5, Phố Nguyễn Hữu An, Phường Cầu Giấy, TP Hà Nội",
            ),
        ).toBeInTheDocument();
    });

    it("renders dedicated input field for document number in Step 2", async () => {
        const user = userEvent.setup();
        render(<OwnerOnboardingWizard />);

        await screen.findByText("1. Chọn hình thức hoạt động");
        await user.type(
            screen.getByLabelText(/Tên Doanh nghiệp/i),
            "Công ty Cổ phần Bãi xe ParkWise",
        );
        await user.type(screen.getByLabelText(/Mã số thuế/i), "0312345678");
        await user.click(
            screen.getByRole("button", {
                name: /Tiếp tục: Tải giấy tờ chứng thực/i,
            }),
        );

        expect(
            await screen.findByText("2. Tải lên giấy tờ chứng minh"),
        ).toBeInTheDocument();
        const docInput = screen.getByLabelText(/Số hiệu \/ Mã giấy tờ/i);
        expect(docInput).toBeInTheDocument();
        expect(docInput).toHaveValue("0312345678");

        await user.clear(docInput);
        await user.type(docInput, "GPKD-999888");
        expect(docInput).toHaveValue("GPKD-999888");
    });

    it("renders basic verification fields (land plot, area, address) to match with uploaded documents", async () => {
        const user = userEvent.setup();
        vi.spyOn(
            ownerLegalApi,
            "getOwnerDocumentRequirements",
        ).mockResolvedValue({
            ownerType: "Individual",
            documents: ownerLegalApi.FALLBACK_REQUIREMENTS.Individual,
        });

        render(<OwnerOnboardingWizard />);
        await screen.findByText("1. Chọn hình thức hoạt động");

        // Chọn cá nhân
        await user.click(screen.getByText("Cá nhân"));
        await user.type(
            screen.getByLabelText(/Tên bãi đỗ xe/i),
            "Bãi xe An Bình",
        );
        await user.type(
            screen.getByLabelText(/Họ và tên chủ sở hữu/i),
            "Nguyễn Văn An",
        );
        await user.type(
            screen.getByLabelText(/Số Căn cước công dân/i),
            "079094001234",
        );
        await user.type(
            screen.getByLabelText(/Địa chỉ.*bãi đỗ xe/i),
            "123 Đường Số 1, Quận 7, TP.HCM",
        );

        await user.click(
            screen.getByRole("button", {
                name: /Tiếp tục: Tải giấy tờ chứng thực/i,
            }),
        );

        expect(
            await screen.findByText("2. Tải lên giấy tờ chứng minh"),
        ).toBeInTheDocument();

        // Kiểm tra hiển thị khối thông tin đối chiếu
        const verifyBadges = screen.getAllByText(/Thông tin cơ bản đối chiếu/i);
        expect(verifyBadges.length).toBeGreaterThan(0);

        // Kiểm tra các trường cơ bản đối chiếu trên Sổ đỏ/Hợp đồng thuê
        expect(
            screen.getByLabelText(/Thửa đất số & Tờ bản đồ số/i),
        ).toBeInTheDocument();
        expect(
            screen.getByLabelText(/Diện tích mặt bằng/i),
        ).toBeInTheDocument();
        expect(
            screen.getByLabelText(/Địa chỉ thửa đất \/ Mặt bằng/i),
        ).toBeInTheDocument();

        // Điền dữ liệu đối chiếu Sổ đỏ
        const landPlotInput = screen.getByLabelText(
            /Thửa đất số & Tờ bản đồ số/i,
        );
        const landAreaInput = screen.getByLabelText(/Diện tích mặt bằng/i);

        await user.type(landPlotInput, "Thửa 128, Tờ 12");
        await user.type(landAreaInput, "500 m2");

        expect(landPlotInput).toHaveValue("Thửa 128, Tờ 12");
        expect(landAreaInput).toHaveValue("500 m2");

        // Kiểm tra bên dưới có khung tải hình ảnh đối chiếu
        expect(
            screen.getAllByText(/Tải lên hình ảnh hoặc bản scan để đối chiếu/i)
                .length,
        ).toBeGreaterThan(0);
    });

    it("renders pending approval screen with simulation sandbox link and email notice", async () => {
        vi.spyOn(ownerLegalApi, "getMyOwnerApplication").mockResolvedValue({
            accountId: "acc-123",
            email: "partner@parkwise.vn",
            status: "Pending",
            taxCode: "0312345678",
            businessName: "Bãi xe ParkWise Trung Tâm",
            rejectionReason: null,
            reviewedAtUtc: new Date().toISOString(),
            ownerType: "Enterprise",
            contactEmail: "partner@parkwise.vn",
        });

        render(<OwnerOnboardingWizard />);

        expect(
            await screen.findByText(
                /Hồ sơ đã tiếp nhận - Chờ Ban quản trị duyệt/i,
            ),
        ).toBeInTheDocument();
        expect(
            screen.getByText(/Hệ thống đã gửi email xác nhận tiếp nhận hồ sơ/i),
        ).toBeInTheDocument();
        expect(
            screen.getByText(/Vui lòng kiểm tra hộp thư đến/i),
        ).toBeInTheDocument();

        // Verify it links to simulation mode instead of direct dashboard
        expect(
            screen.queryByText(/Truy cập Dashboard Chủ bãi \(Bản xem trước\)/i),
        ).not.toBeInTheDocument();
        const simLink = screen.getByRole("link", {
            name: /Trải nghiệm Dashboard mô phỏng/i,
        });
        expect(simLink).toBeInTheDocument();
        expect(simLink).toHaveAttribute("href", "/owner/simulation");

        expect(
            screen.getByRole("button", { name: /Cập nhật lại hồ sơ/i }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: /Về trang chủ/i }),
        ).toBeInTheDocument();
    });

    it("opens VNeID modal and auto-fills CCCD and representative information", async () => {
        vi.mocked(ownerLegalApi.getMyOwnerApplication).mockResolvedValue({
            accountId: "acc-test",
            email: "owner@demo.local",
            status: "None",
            taxCode: null,
            businessName: null,
            rejectionReason: null,
            reviewedAtUtc: null,
            ownerType: "Individual",
        });

        render(<OwnerOnboardingWizard />);

        const user = userEvent.setup();
        await screen.findByText("1. Chọn hình thức hoạt động");
        await user.click(screen.getByText("Cá nhân"));

        // Check VNeID button exists
        const vneidBtn = screen.getByRole("button", {
            name: /Điền nhanh qua VNeID/i,
        });
        expect(vneidBtn).toBeInTheDocument();

        // Click to open VNeID modal
        fireEvent.click(vneidBtn);
        expect(screen.getByTestId("vneid-auth-modal")).toBeInTheDocument();

        // Confirm sharing
        const confirmBtn = screen.getByRole("button", {
            name: /Xác nhận chia sẻ thông tin/i,
        });
        fireEvent.click(confirmBtn);

        // Verify auto-filled values
        expect(screen.getByTestId("vneid-verified-badge")).toBeInTheDocument();
        const idCardInput = screen.getByPlaceholderText(/079201234567/i) as HTMLInputElement;
        expect(idCardInput.value).toBe("079201234567");
        const fullNameInput = screen.getByPlaceholderText(/Nguyễn Văn An/i) as HTMLInputElement;
        expect(fullNameInput.value).toBe("NGUYỄN VĂN AN");
    });
});
