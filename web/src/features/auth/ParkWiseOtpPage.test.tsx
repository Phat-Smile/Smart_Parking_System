import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ParkWiseOtpPage } from "./ParkWiseOtpPage";
import { verifyParkWiseEmail, resendParkWiseOtp } from "./emailVerificationApi";

vi.mock("next/navigation", () => ({
    useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

vi.mock("./ParkWiseAuthLayout", () => ({
    ParkWiseAuthLayout: ({ children }: { children: ReactNode }) => (
        <div data-testid="auth-layout">{children}</div>
    ),
    ParkWiseAuthAside: ({
        description,
        afterWelcome,
    }: {
        description: string;
        afterWelcome?: ReactNode;
    }) => (
        <aside data-testid="auth-aside">
            <p>{description}</p>
            {afterWelcome}
        </aside>
    ),
}));

vi.mock("./emailVerificationApi", () => ({
    verifyParkWiseEmail: vi.fn(),
    resendParkWiseOtp: vi.fn(),
}));

const verifyMock = vi.mocked(verifyParkWiseEmail);
const resendMock = vi.mocked(resendParkWiseOtp);

describe("ParkWiseOtpPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        global.fetch = vi.fn().mockImplementation(() =>
            Promise.resolve({
                ok: false,
                json: () => Promise.resolve({}),
            }),
        );
    });

    it("renders owner OTP layout with Vietnamese identity for role=owner", () => {
        render(
            <ParkWiseOtpPage
                fontClassName="test-font"
                initialEmail="owner@example.com"
                role="owner"
            />,
        );

        expect(
            screen.getByRole("heading", { name: "Xác thực Email Đối tác" }),
        ).toBeInTheDocument();
        expect(
            screen.getByText(
                /Mã xác thực 6 chữ số đã được gửi đến owner@example.com/,
            ),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: "Quay lại Đăng ký Đối tác" }),
        ).toHaveAttribute("href", "/owner/register");
        expect(
            screen.getByRole("button", { name: "Xác thực & Kích hoạt" }),
        ).toBeDisabled();
        expect(
            screen.getByText(/Mã OTP được gửi trực tiếp từ máy chủ Backend/i),
        ).toBeInTheDocument();
    });

    it("renders standard English layout when role is customer/empty", () => {
        render(
            <ParkWiseOtpPage
                fontClassName="test-font"
                initialEmail="driver@example.com"
            />,
        );

        expect(
            screen.getByRole("heading", { name: "Verify Your Email" }),
        ).toBeInTheDocument();
        expect(
            screen.getByText(
                /Enter the verification code for driver@example.com/,
            ),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", { name: "Back to Sign Up" }),
        ).toHaveAttribute("href", "/sign-up");
        expect(
            screen.getByRole("button", { name: "Verify Code" }),
        ).toBeDisabled();
    });

    it("calls verifyParkWiseEmail when all 6 digits are entered", async () => {
        verifyMock.mockResolvedValueOnce({
            accountId: "acc-123",
            email: "owner@example.com",
            isVerified: true,
            status: "Active",
            message: "Account verified",
        });

        render(
            <ParkWiseOtpPage
                fontClassName="test-font"
                initialEmail="owner@example.com"
                role="owner"
            />,
        );

        const inputs = screen.getAllByRole("textbox");
        expect(inputs).toHaveLength(6);

        // Type digits 123456
        for (let i = 0; i < 6; i++) {
            fireEvent.change(inputs[i], { target: { value: String(i + 1) } });
        }

        await waitFor(() => {
            expect(verifyMock).toHaveBeenCalledWith(
                "owner@example.com",
                "123456",
            );
        });

        expect(
            await screen.findByRole("heading", {
                name: "Xác thực email thành công",
            }),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("link", {
                name: /Tiếp tục: Hoàn tất hồ sơ đối tác/i,
            }),
        ).toHaveAttribute("href", "/owner/onboarding");
    });

    it("displays dev OTP prompt when detected from backend dev helper", async () => {
        global.fetch = vi.fn().mockImplementation((url: string) => {
            if (url.includes("/api/dev-otp")) {
                return Promise.resolve({
                    ok: true,
                    json: () =>
                        Promise.resolve({
                            email: "owner@example.com",
                            otpCode: "654321",
                            found: true,
                        }),
                });
            }
            return Promise.resolve({ ok: false });
        });

        verifyMock.mockResolvedValueOnce({
            accountId: "acc-123",
            email: "owner@example.com",
            isVerified: true,
            status: "Active",
            message: "Account verified",
        });

        render(
            <ParkWiseOtpPage
                fontClassName="test-font"
                initialEmail="owner@example.com"
                role="owner"
            />,
        );

        // Wait for dev OTP prompt to appear
        const otpBadges = await screen.findAllByText("654321");
        expect(otpBadges.length).toBeGreaterThanOrEqual(1);
        expect(
            screen.getByText(/Mã xác thực từ Backend \(Chế độ phát triển\)/),
        ).toBeInTheDocument();

        // Click autofill button
        fireEvent.click(
            screen.getByRole("button", { name: "✓ Tự động dán mã" }),
        );

        await waitFor(() => {
            expect(verifyMock).toHaveBeenCalledWith(
                "owner@example.com",
                "654321",
            );
        });
    });
});
