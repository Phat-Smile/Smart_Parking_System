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
});

describe("OwnerRegisterPage", () => {
    it("renders owner registration fields, terms checkbox and links", () => {
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
        expect(
            screen.getByRole("button", { name: /Tiếp tục — Xác thực Email/i }),
        ).toBeInTheDocument();
    });

    it("blocks submission when fields are empty", async () => {
        const user = userEvent.setup();
        render(<OwnerRegisterPage />);

        await user.click(
            screen.getByRole("button", { name: /Tiếp tục — Xác thực Email/i }),
        );

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

        await user.click(
            screen.getByRole("button", { name: /Tiếp tục — Xác thực Email/i }),
        );

        await vi.waitFor(() => {
            expect(mockedRegister).toHaveBeenCalledWith({
                fullName: "Nguyễn Văn Chủ Bãi",
                email: "partner@example.com",
                phoneNumber: "0987654321",
                password: "PartnerPass123!",
                accountType: "ParkingLotOwner",
            });
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

        await user.click(
            screen.getByRole("button", { name: /Tiếp tục — Xác thực Email/i }),
        );

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

        await user.click(
            screen.getByRole("button", { name: /Tiếp tục — Xác thực Email/i }),
        );

        expect(
            await screen.findByText(
                /Không thể hoàn tất đăng ký: Số điện thoại hoặc Email này có thể đã được đăng ký trong hệ thống/i,
            ),
        ).toBeVisible();
    });
});
