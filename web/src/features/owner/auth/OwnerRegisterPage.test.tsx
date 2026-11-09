import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { OwnerRegisterPage } from "./OwnerRegisterPage";
import { registerParkWiseAccount } from "@/features/auth/registerParkWiseAccount";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

vi.mock("@/features/auth/registerParkWiseAccount", () => ({
    registerParkWiseAccount: vi.fn(),
}));

const mockedRegister = vi.mocked(registerParkWiseAccount);

beforeEach(() => {
    mockedRegister.mockReset();
    push.mockReset();
    window.sessionStorage.clear();
    window.localStorage.clear();
});

describe("OwnerRegisterPage", () => {
    it("renders owner registration fields, terms checkbox, language switcher, 2-step indicator and continue button", () => {
        render(<OwnerRegisterPage />);
        expect(
            screen.getByLabelText(/Họ và tên người đại diện/i),
        ).toBeInTheDocument();
        expect(screen.getByLabelText(/Email liên hệ/i)).toBeInTheDocument();
        expect(
            screen.getByLabelText(/Số điện thoại di động/i),
        ).toBeInTheDocument();
        expect(screen.getByLabelText(/^Mật khẩu bảo mật/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/Xác nhận mật khẩu/i)).toBeInTheDocument();
        expect(screen.getByRole("checkbox")).toBeInTheDocument();

        // Nút Tiếp tục (Hình 4)
        expect(
            screen.getByRole("button", { name: /^Tiếp tục$/i }),
        ).toBeInTheDocument();

        // 2 bước tuần tự (Hình 5)
        expect(screen.getByText("Khởi tạo tài khoản")).toBeInTheDocument();
        expect(screen.getByText("Xác thực Email")).toBeInTheDocument();

        // Nút chuyển ngôn ngữ VI / EN (Hình 4)
        expect(screen.getByTitle("Tiếng Việt")).toBeInTheDocument();
        expect(screen.getByTitle("English")).toBeInTheDocument();

        // Độ mạnh mật khẩu (Hình 3)
        expect(screen.getByText(/^Độ mạnh mật khẩu/i)).toBeInTheDocument();
    });

    it("switches language between Vietnamese and English", async () => {
        const user = userEvent.setup();
        render(<OwnerRegisterPage />);

        // Chuyển sang tiếng Anh
        await user.click(screen.getByTitle("English"));

        expect(
            screen.getByRole("button", { name: /^Continue$/i }),
        ).toBeInTheDocument();
        expect(screen.getByText("Create Account")).toBeInTheDocument();
        expect(screen.getByText("Verify Email")).toBeInTheDocument();
        expect(
            screen.getByLabelText(/Representative Full Name/i),
        ).toBeInTheDocument();

        // Chuyển lại tiếng Việt
        await user.click(screen.getByTitle("Tiếng Việt"));

        expect(
            screen.getByRole("button", { name: /^Tiếp tục$/i }),
        ).toBeInTheDocument();
        expect(screen.getByText("Khởi tạo tài khoản")).toBeInTheDocument();
    });

    it("blocks submission when fields are empty", async () => {
        const user = userEvent.setup();
        render(<OwnerRegisterPage />);

        await user.click(screen.getByRole("button", { name: /^Tiếp tục$/i }));

        expect(
            await screen.findByText("Họ và tên phải có ít nhất 2 ký tự"),
        ).toBeVisible();
        expect(screen.getByText("Email không được để trống")).toBeVisible();
        expect(
            screen.getByText("Số điện thoại không được để trống"),
        ).toBeVisible();
        expect(mockedRegister).not.toHaveBeenCalled();
    });

    it("submits with accountType: ParkingLotOwner and redirects to verify-otp", async () => {
        const user = userEvent.setup();
        mockedRegister.mockResolvedValueOnce({
            accountId: "owner-123",
            email: "partner@example.com",
            fullName: "Nguyễn Văn Chủ Bãi",
            status: "PendingEmailVerification",
            message: "Account registered",
        });

        render(<OwnerRegisterPage />);

        await user.type(
            screen.getByLabelText(/Họ và tên người đại diện/i),
            "Nguyễn Văn Chủ Bãi",
        );
        await user.type(
            screen.getByLabelText(/Email liên hệ/i),
            "partner@example.com",
        );
        await user.type(
            screen.getByLabelText(/Số điện thoại di động/i),
            "0987654321",
        );
        await user.type(
            screen.getByLabelText(/^Mật khẩu bảo mật/i),
            "PartnerPass123!",
        );
        await user.type(
            screen.getByLabelText(/Xác nhận mật khẩu/i),
            "PartnerPass123!",
        );
        await user.click(screen.getByRole("checkbox"));

        await user.click(screen.getByRole("button", { name: /^Tiếp tục$/i }));

        await vi.waitFor(() => {
            expect(mockedRegister).toHaveBeenCalledWith({
                fullName: "Nguyễn Văn Chủ Bãi",
                email: "partner@example.com",
                phoneNumber: "0987654321",
                password: "PartnerPass123!",
                accountType: "ParkingLotOwner",
            });
        });

        await vi.waitFor(() => {
            expect(push).toHaveBeenCalledWith(
                expect.stringContaining(
                    "/verify-otp?email=partner%40example.com&role=owner",
                ),
            );
        });
    });

    it("displays friendly message when phone number is already registered", async () => {
        const user = userEvent.setup();
        const { ApiError } = await import("@/shared/errors/api-error");
        mockedRegister.mockRejectedValueOnce(
            new ApiError(
                409,
                {
                    errorCode: "identity.phone_already_registered",
                    title: "Conflict",
                },
                undefined,
            ),
        );

        render(<OwnerRegisterPage />);

        await user.type(
            screen.getByLabelText(/Họ và tên người đại diện/i),
            "Nguyễn Văn Chủ Bãi",
        );
        await user.type(
            screen.getByLabelText(/Email liên hệ/i),
            "partner@example.com",
        );
        await user.type(
            screen.getByLabelText(/Số điện thoại di động/i),
            "0982567849",
        );
        await user.type(
            screen.getByLabelText(/^Mật khẩu bảo mật/i),
            "PartnerPass123!",
        );
        await user.type(
            screen.getByLabelText(/Xác nhận mật khẩu/i),
            "PartnerPass123!",
        );
        await user.click(screen.getByRole("checkbox"));

        await user.click(screen.getByRole("button", { name: /^Tiếp tục$/i }));

        expect(
            await screen.findByText(
                /Số điện thoại này đã được sử dụng bởi một tài khoản khác/i,
            ),
        ).toBeVisible();
    });

    it("displays informative error when server returns 500", async () => {
        const user = userEvent.setup();
        const { ApiError } = await import("@/shared/errors/api-error");
        mockedRegister.mockRejectedValueOnce(
            new ApiError(
                500,
                { title: "An unexpected error occurred." },
                undefined,
            ),
        );

        render(<OwnerRegisterPage />);

        await user.type(
            screen.getByLabelText(/Họ và tên người đại diện/i),
            "Nguyễn Văn Chủ Bãi",
        );
        await user.type(
            screen.getByLabelText(/Email liên hệ/i),
            "partner@example.com",
        );
        await user.type(
            screen.getByLabelText(/Số điện thoại di động/i),
            "0982567849",
        );
        await user.type(
            screen.getByLabelText(/^Mật khẩu bảo mật/i),
            "PartnerPass123!",
        );
        await user.type(
            screen.getByLabelText(/Xác nhận mật khẩu/i),
            "PartnerPass123!",
        );
        await user.click(screen.getByRole("checkbox"));

        await user.click(screen.getByRole("button", { name: /^Tiếp tục$/i }));

        expect(
            await screen.findByText(
                /Không thể hoàn tất đăng ký: Số điện thoại hoặc Email này có thể đã được đăng ký trong hệ thống/i,
            ),
        ).toBeVisible();
    });

    it("renders registration container with proper vertical spacing below navbar", () => {
        render(<OwnerRegisterPage />);
        const main = screen.getByRole("main");
        expect(main).toHaveClass("parkwise-owner-register-main");
        expect(main).toHaveStyle({
            paddingTop: "calc(var(--parkwise-nav-height, 66px) + 32px)",
        });
    });
});
