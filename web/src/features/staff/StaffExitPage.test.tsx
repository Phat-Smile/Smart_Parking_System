import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
    translations,
    type SupportedLanguage,
} from "@/shared/i18n/translations";
import { ApiError } from "@/shared/errors/api-error";

const { mockState, apiMocks } = vi.hoisted(() => ({
    mockState: { language: "vi" as SupportedLanguage },
    apiMocks: {
        findSessionByQr: vi.fn(),
        findOpenSessionByPlate: vi.fn(),
        getParkingSessionFee: vi.fn(),
        payParkingSessionCash: vi.fn(),
        completeParkingSession: vi.fn(),
        manualVerifyExit: vi.fn(),
        reissueParkingSessionQr: vi.fn(),
    },
}));

vi.mock("@/shared/i18n/LanguageContext", () => ({
    useLanguage: () => ({
        language: mockState.language,
        t: (key: keyof (typeof translations)["vi"]) =>
            translations[mockState.language][key] ?? key,
    }),
}));

vi.mock("./RealCameraFeed", () => ({
    RealCameraFeed: (props: { onPlateDetected?: (plate: string) => void }) => (
        <div data-testid="real-camera-feed">
            <button
                data-testid="simulate-plate-detect"
                onClick={() => props.onPlateDetected?.("51B-999.88")}
            >
                Simulate Camera Plate
            </button>
        </div>
    ),
}));

vi.mock("./audio", () => ({
    useGateVoice: () => ({
        announceCheckOut: vi.fn(),
        announceWarning: vi.fn(),
    }),
}));

vi.mock("./staffData", () => ({
    useStaffData: () => ({
        assignedLot: { lotId: "lot-1", lotName: "Bãi Test" },
        completeExit: vi.fn(),
        activeSessions: [
            {
                id: "active-1",
                lotId: "lot-1",
                plateNumber: "51B-999.88",
                vehicleType: "Car",
                entryTime: "2026-11-12T01:00:00Z",
                status: "Active",
                qrToken: "qr-active-abc",
                slotCode: null,
                paymentStatus: "Pending",
            },
        ],
        fetchAssignedLot: vi.fn().mockResolvedValue(null),
    }),
}));

vi.mock("./staffApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./staffApi")>();
    return {
        ...actual,
        findSessionByQr: apiMocks.findSessionByQr,
        findOpenSessionByPlate: apiMocks.findOpenSessionByPlate,
        getParkingSessionFee: apiMocks.getParkingSessionFee,
        payParkingSessionCash: apiMocks.payParkingSessionCash,
        completeParkingSession: apiMocks.completeParkingSession,
        manualVerifyExit: apiMocks.manualVerifyExit,
        reissueParkingSessionQr: apiMocks.reissueParkingSessionQr,
    };
});

import { StaffExitPage } from "./StaffExitPage";

const SESSION_ID = "11111111-1111-1111-1111-111111111111";

function unpaidSession() {
    return {
        sessionId: SESSION_ID,
        lotId: "lot-1",
        plateNumber: "51B-999.88",
        vehicleType: "Car",
        customerType: "Registered",
        status: "Active",
        entryTime: "2026-11-12T01:00:00Z",
        qrToken: "qr-abc",
        totalFeeVnd: 15000,
        amountDueVnd: 15000,
    };
}

async function scanQr() {
    fireEvent.change(document.getElementById("staff-qr-input")!, {
        target: { value: "qr-abc" },
    });
    fireEvent.click(screen.getByText(translations.vi.exitVerifyQrBtn));
    await waitFor(() =>
        expect(apiMocks.findSessionByQr).toHaveBeenCalledWith("qr-abc"),
    );
}

function enterGatePlate(plate: string = "51B-999.88") {
    fireEvent.change(document.getElementById("staff-exit-plate")!, {
        target: { value: plate },
    });
}

describe("StaffExitPage payment and exit (WP-GATE-1)", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockState.language = "vi";
        apiMocks.findSessionByQr.mockResolvedValue(unpaidSession());
    });

    it("the exit button stays disabled while the session is unpaid", async () => {
        render(<StaffExitPage />);
        await scanQr();
        enterGatePlate("51B-999.88");

        const exit = await screen.findByText(translations.vi.exitAuthorizeBtn);
        expect(exit.closest("button")).toBeDisabled();
        expect(apiMocks.completeParkingSession).not.toHaveBeenCalled();
    });

    it("cash collection calls Payment with the quoted amount and then allows the exit", async () => {
        apiMocks.payParkingSessionCash.mockResolvedValue({
            transactionId: "t1",
            sessionId: SESSION_ID,
            amountPaid: 15000,
        });
        render(<StaffExitPage />);
        await scanQr();
        enterGatePlate("51B-999.88");

        fireEvent.click(
            await screen.findByRole("button", { name: /Thu tiền mặt/ }),
        );
        fireEvent.click(
            await screen.findByText(translations.vi.exitConfirmCashPayment),
        );

        await waitFor(() =>
            expect(apiMocks.payParkingSessionCash).toHaveBeenCalledWith(
                SESSION_ID,
                15000,
            ),
        );
        await waitFor(() =>
            expect(
                screen
                    .getByText(translations.vi.exitAuthorizeBtn)
                    .closest("button"),
            ).toBeEnabled(),
        );
    });

    it("a refused cash collection does not mark the session paid", async () => {
        apiMocks.payParkingSessionCash.mockRejectedValue(new Error("boom"));
        render(<StaffExitPage />);
        await scanQr();
        enterGatePlate("51B-999.88");

        fireEvent.click(
            await screen.findByRole("button", { name: /Thu tiền mặt/ }),
        );
        fireEvent.click(
            await screen.findByText(translations.vi.exitConfirmCashPayment),
        );

        await waitFor(() =>
            expect(apiMocks.payParkingSessionCash).toHaveBeenCalled(),
        );
        expect(
            screen
                .getByText(translations.vi.exitAuthorizeBtn)
                .closest("button"),
        ).toBeDisabled();
    });

    it("the exit sends the plate read at the gate and the QR customer presents", async () => {
        apiMocks.findSessionByQr.mockResolvedValue({
            ...unpaidSession(),
            status: "Paid",
            amountDueVnd: 0,
        });
        apiMocks.completeParkingSession.mockResolvedValue({
            sessionId: SESSION_ID,
            status: "Completed",
        });
        render(<StaffExitPage />);
        await scanQr();
        enterGatePlate("51B-999.88");

        const exit = await screen.findByText(translations.vi.exitAuthorizeBtn);
        await waitFor(() => expect(exit.closest("button")).toBeEnabled());
        fireEvent.click(exit);

        await waitFor(() =>
            expect(apiMocks.completeParkingSession).toHaveBeenCalledWith(
                SESSION_ID,
                { plateNumber: "51B-999.88", qrToken: "qr-abc" },
            ),
        );
    });

    it("OCR khớp một phiên đang trong bãi: không tự động điền QR của phiên đó", async () => {
        render(<StaffExitPage />);
        const cameraBtn = screen.getByTestId("simulate-plate-detect");
        fireEvent.click(cameraBtn);

        // Biển số được cập nhật vào ô biển số cổng ra
        const plateInput = document.getElementById(
            "staff-exit-plate",
        ) as HTMLInputElement;
        expect(plateInput.value).toBe("51B-999.88");

        // Nhưng ô QR khách đưa không được tự điền
        const qrInput = document.getElementById(
            "staff-qr-input",
        ) as HTMLInputElement;
        expect(qrInput.value).toBe("");
        expect(apiMocks.findSessionByQr).not.toHaveBeenCalled();
    });

    it("Biển số đọc ở cổng khác biển số phiên: không tự cho mở rào", async () => {
        apiMocks.findSessionByQr.mockResolvedValue({
            ...unpaidSession(),
            status: "Paid",
            amountDueVnd: 0,
        });
        render(<StaffExitPage />);
        await scanQr();
        // Nhập biển số khác với biển số phiên (51A-123.45 vs 51B-999.88)
        enterGatePlate("51A-123.45");

        const exitBtn = screen
            .getByText(translations.vi.exitAuthorizeBtn)
            .closest("button");
        expect(exitBtn).toBeDisabled();
        expect(apiMocks.completeParkingSession).not.toHaveBeenCalled();
    });

    it("payment_required từ backend không kích hoạt mở rào hay tự động cứu cánh", async () => {
        apiMocks.findSessionByQr.mockResolvedValue({
            ...unpaidSession(),
            status: "Paid",
            amountDueVnd: 0,
        });
        const paymentRequiredErr = new ApiError(400, {
            errorCode: "parking.session.payment_required",
            title: "Payment required",
            detail: "The parking session must be paid before the vehicle can leave.",
        });
        apiMocks.completeParkingSession.mockRejectedValue(paymentRequiredErr);

        render(<StaffExitPage />);
        await scanQr();
        enterGatePlate("51B-999.88");

        const exit = await screen.findByText(translations.vi.exitAuthorizeBtn);
        fireEvent.click(exit);

        await waitFor(() =>
            expect(apiMocks.completeParkingSession).toHaveBeenCalled(),
        );

        // Không tự động gọi manualVerifyExit làm cứu cánh
        expect(apiMocks.manualVerifyExit).not.toHaveBeenCalled();
        // Rào chắn không mở
        expect(
            screen.queryByText(/Đã mở barrier cho xe xuất bãi/i),
        ).not.toBeInTheDocument();
    });

    it("Xác minh thủ công lỗi: không báo mở rào", async () => {
        apiMocks.findSessionByQr.mockResolvedValue({
            ...unpaidSession(),
            status: "Paid",
            amountDueVnd: 0,
        });
        apiMocks.manualVerifyExit.mockRejectedValue(
            new Error("Lỗi xác minh thủ công"),
        );

        render(<StaffExitPage />);
        await scanQr();
        enterGatePlate("51B-999.88");

        // Tìm nút xác minh thủ công
        const manualBtn = screen.getByRole("button", {
            name: /Xác minh thủ công/i,
        });
        fireEvent.click(manualBtn);

        await waitFor(() =>
            expect(apiMocks.manualVerifyExit).toHaveBeenCalled(),
        );
        expect(
            screen.queryByText(/Đã mở barrier cho xe xuất bãi/i),
        ).not.toBeInTheDocument();
    });

    it("Bấm nút thu tiền mặt 2 lần: chỉ gọi API thanh toán đúng 1 lần (Double click guard)", async () => {
        let resolvePayment: (val: unknown) => void;
        const pendingPromise = new Promise((resolve) => {
            resolvePayment = resolve;
        });
        apiMocks.payParkingSessionCash.mockReturnValue(pendingPromise);

        render(<StaffExitPage />);
        await scanQr();
        enterGatePlate("51B-999.88");

        fireEvent.click(
            await screen.findByRole("button", { name: /Thu tiền mặt/ }),
        );
        const confirmBtn = await screen.findByText(
            translations.vi.exitConfirmCashPayment,
        );

        // Click liên tiếp 2 lần
        fireEvent.click(confirmBtn);
        fireEvent.click(confirmBtn);

        // Chỉ được gọi 1 lần
        expect(apiMocks.payParkingSessionCash).toHaveBeenCalledTimes(1);

        resolvePayment!({
            transactionId: "tx-1",
            sessionId: SESSION_ID,
            amountPaid: 15000,
        });
    });

    it("Tìm theo biển số không tự điền ô QR; nhân viên không thể cho ra nếu khách không xuất trình QR (/complete không được gọi)", async () => {
        apiMocks.findOpenSessionByPlate.mockResolvedValue({
            sessionId: "ses-plate-1",
            lotId: "lot-1",
            plateNumber: "51B-999.88",
            vehicleType: "Car",
            customerType: "Guest",
            status: "Paid",
            entryTime: "2026-11-12T01:00:00Z",
            qrToken: "real-session-qr-token-secret",
        });
        apiMocks.getParkingSessionFee.mockResolvedValue({
            sessionId: "ses-plate-1",
            totalFee: 30000,
            amountDue: 0,
        });

        render(<StaffExitPage />);
        // Nhập biển số tại cổng
        enterGatePlate("51B-999.88");

        // Bấm nút "Tra cứu mất vé"
        const lookupBtn = screen.getByRole("button", {
            name: /Tra cứu mất vé/i,
        });
        fireEvent.click(lookupBtn);

        // Chờ tra cứu thành công và hiển thị phiên
        await waitFor(() =>
            expect(apiMocks.findOpenSessionByPlate).toHaveBeenCalledWith(
                "lot-1",
                "51B-999.88",
            ),
        );

        // Bất biến: Ô QR khách đưa KHÔNG được tự động điền
        const qrInput = document.getElementById(
            "staff-qr-input",
        ) as HTMLInputElement;
        expect(qrInput.value).toBe("");

        // Nút cho ra phải bị vô hiệu hóa vì qrCheckStatus !== "SUCCESS"
        const exitBtn = screen
            .getByText(translations.vi.exitAuthorizeBtn)
            .closest("button");
        expect(exitBtn).toBeDisabled();

        // Không có bất kỳ lệnh gọi /complete nào được thực hiện
        expect(apiMocks.completeParkingSession).not.toHaveBeenCalled();
    });

    it("Bấm chip phiên đang đỗ không tự điền QR và không tự điền biển số cổng ra; không thể hoàn tất ra (/complete không được gọi)", async () => {
        render(<StaffExitPage />);

        // Tìm chip xe đang đỗ
        const chip = screen.getByRole("button", {
            name: /51B-999\.88/i,
        });
        fireEvent.click(chip);

        // Bất biến (BR-11 / Anti-Bypass): Ô QR và ô Biển số cổng ra KHÔNG được tự động điền
        const qrInput = document.getElementById(
            "staff-qr-input",
        ) as HTMLInputElement;
        expect(qrInput.value).toBe("");

        const plateInput = document.getElementById(
            "staff-exit-plate",
        ) as HTMLInputElement;
        expect(plateInput.value).toBe("");

        // Nút cho ra phải bị vô hiệu hóa
        const exitBtn = screen
            .getByText(translations.vi.exitAuthorizeBtn)
            .closest("button");
        expect(exitBtn).toBeDisabled();

        // /complete tuyệt đối không được gọi
        expect(apiMocks.completeParkingSession).not.toHaveBeenCalled();
    });

    it("Bấm nút cấp lại vé QR: gọi API reissue nhưng KHÔNG tự điền mã QR vào ô nhập cổng ra", async () => {
        apiMocks.findOpenSessionByPlate.mockResolvedValue({
            sessionId: "ses-plate-reissue",
            lotId: "lot-1",
            plateNumber: "51B-999.88",
            vehicleType: "Car",
            customerType: "Guest",
            status: "Paid",
            entryTime: "2026-11-12T01:00:00Z",
            qrToken: "qr-old-lost",
        });
        apiMocks.getParkingSessionFee.mockResolvedValue({
            sessionId: "ses-plate-reissue",
            totalFee: 30000,
            amountDue: 0,
        });
        apiMocks.reissueParkingSessionQr.mockResolvedValue({
            sessionId: "ses-plate-reissue",
            qrToken: "qr-newly-reissued-secret",
            issuedAt: "2026-11-14T10:00:00Z",
        });

        render(<StaffExitPage />);
        enterGatePlate("51B-999.88");

        const lookupBtn = screen.getByRole("button", {
            name: /Tra cứu mất vé/i,
        });
        fireEvent.click(lookupBtn);

        await waitFor(() =>
            expect(apiMocks.findOpenSessionByPlate).toHaveBeenCalledWith(
                "lot-1",
                "51B-999.88",
            ),
        );

        // Bấm nút "Cấp lại vé QR" mở modal
        const reissueBtn = await screen.findByRole("button", {
            name: /Cấp lại vé QR/i,
        });
        fireEvent.click(reissueBtn);

        // Xác nhận trong modal V6-UI
        const confirmReissueBtn = await screen.findByRole("button", {
            name: /Xác nhận & Cấp vé/i,
        });
        fireEvent.click(confirmReissueBtn);

        await waitFor(() =>
            expect(apiMocks.reissueParkingSessionQr).toHaveBeenCalledWith(
                "ses-plate-reissue",
                "Khách làm mất vé QR tại cổng ra",
            ),
        );

        // Bất biến (BR-11 / Anti-Bypass): Ô quét QR cổng ra KHÔNG được tự động điền
        const qrInput = document.getElementById(
            "staff-qr-input",
        ) as HTMLInputElement;
        expect(qrInput.value).toBe("");

        // Nút cho ra phải bị vô hiệu hóa vì khách chưa quét vé mới tại cổng
        const exitBtn = screen
            .getByText(translations.vi.exitAuthorizeBtn)
            .closest("button");
        expect(exitBtn).toBeDisabled();

        // /complete tuyệt đối không được gọi
        expect(apiMocks.completeParkingSession).not.toHaveBeenCalled();
    });

    it("V6-UI: reissue modal allows custom mandatory reason and enforces character limit", async () => {
        apiMocks.findOpenSessionByPlate.mockResolvedValue({
            sessionId: "ses-plate-reissue-custom",
            lotId: "lot-1",
            plateNumber: "51F-999.88",
            vehicleType: "Car",
            customerType: "Guest",
            status: "Paid",
            entryTime: "2026-11-14T08:00:00Z",
            qrToken: "qr-old-lost",
        });
        apiMocks.getParkingSessionFee.mockResolvedValue({
            sessionId: "ses-plate-reissue-custom",
            totalFee: 30000,
            amountDue: 0,
        });
        apiMocks.reissueParkingSessionQr.mockResolvedValue({
            sessionId: "ses-plate-reissue-custom",
            qrToken: "qr-custom-reissued-token",
            issuedAt: "2026-11-14T10:00:00Z",
        });

        render(<StaffExitPage />);
        enterGatePlate("51F-999.88");

        const lookupBtn = screen.getByRole("button", {
            name: /Tra cứu mất vé/i,
        });
        fireEvent.click(lookupBtn);

        await waitFor(() =>
            expect(apiMocks.findOpenSessionByPlate).toHaveBeenCalledWith(
                "lot-1",
                "51F-999.88",
            ),
        );

        const reissueBtn = await screen.findByRole("button", { name: /Cấp lại vé QR/i });
        fireEvent.click(reissueBtn);

        const reasonInput = screen.getByLabelText(/Lý do cấp lại vé/i) as HTMLTextAreaElement;
        expect(reasonInput).toBeInTheDocument();

        // Clear reason -> button should be disabled
        fireEvent.change(reasonInput, { target: { value: "   " } });
        const confirmBtn = screen.getByRole("button", { name: /Xác nhận & Cấp vé/i });
        expect(confirmBtn).toBeDisabled();

        // Enter valid reason
        fireEvent.change(reasonInput, { target: { value: "Mã QR của khách bị ướt mưa không quét được" } });
        expect(confirmBtn).not.toBeDisabled();
        fireEvent.click(confirmBtn);

        await waitFor(() =>
            expect(apiMocks.reissueParkingSessionQr).toHaveBeenCalledWith(
                "ses-plate-reissue-custom",
                "Mã QR của khách bị ướt mưa không quét được",
            ),
        );
    });
});
