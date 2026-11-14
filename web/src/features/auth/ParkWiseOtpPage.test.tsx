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

    it("does not call /api/dev-otp and displays local mailpit hint in development", async () => {
        vi.stubEnv("NODE_ENV", "development");
        const fetchSpy = vi.fn();
        global.fetch = fetchSpy;

        try {
            render(
                <ParkWiseOtpPage
                    fontClassName="test-font"
                    initialEmail="owner@example.com"
                    role="owner"
                />,
            );

            // Verify /api/dev-otp is never requested
            expect(fetchSpy).not.toHaveBeenCalled();

            // Verify Mailpit notice presence
            expect(
                screen.getByText(/Kiểm tra mã xác thực tại Mailpit/i),
            ).toBeInTheDocument();
        } finally {
            vi.unstubAllEnvs();
        }
    });

    it("allows user to manually enter 6-digit OTP code", async () => {
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

        // Type "654321" into the 6 inputs
        const codeDigits = ["6", "5", "4", "3", "2", "1"];
        codeDigits.forEach((digit, idx) => {
            fireEvent.change(inputs[idx], { target: { value: digit } });
        });

        await waitFor(() => {
            expect(verifyMock).toHaveBeenCalledWith(
                "owner@example.com",
                "654321",
            );
        });
    });

    it("handles resend OTP action correctly", async () => {
        resendMock.mockResolvedValueOnce({
            email: "owner@example.com",
            expiresInSeconds: 300,
            cooldownSeconds: 60,
            message: "OTP sent",
            otpSent: true,
        });

        render(
            <ParkWiseOtpPage
                fontClassName="test-font"
                initialEmail="owner@example.com"
                role="owner"
            />,
        );

        const resendBtn = screen.getByRole("button", { name: /Gửi lại mã/i });
        fireEvent.click(resendBtn);

        await waitFor(() => {
            expect(resendMock).toHaveBeenCalledWith("owner@example.com");
        });
    });
});
