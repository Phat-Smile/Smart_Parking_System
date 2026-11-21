import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
    fetchPublicParkingFee,
    FeeLookupError,
    formatDateTimeVi,
    formatDurationVi,
    formatVnd,
    mapCalculationMethodVi,
    mapStatusVi,
} from "./feeLookupApi";
import { FeeLookupView } from "./FeeLookupView";

// Mock next/navigation
let mockQueryQr: string | null = null;
vi.mock("next/navigation", () => ({
    useSearchParams: () => ({
        get: (param: string) => (param === "qr" ? mockQueryQr : null),
    }),
}));

describe("publicFee - feeLookupApi", () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        vi.restoreAllMocks();
        mockQueryQr = null;
    });

    it("parses successful fee response with masked plate and amounts", async () => {
        const mockResponse = {
            sessionId: "11111111-1111-1111-1111-111111111111",
            maskedPlateNumber: "51F-***.91",
            status: "Active",
            entryTimeUtc: "2026-11-22T08:00:00Z",
            calculatedAtUtc: "2026-11-22T10:15:00Z",
            durationMinutes: 135,
            calculationMethod: "Block",
            totalFee: 45000,
            paidAmount: 0,
            amountDue: 45000,
        };

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(mockResponse), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await fetchPublicParkingFee("test-qr-token-123");
        expect(result.sessionId).toBe("11111111-1111-1111-1111-111111111111");
        expect(result.maskedPlateNumber).toBe("51F-***.91");
        expect(result.durationMinutes).toBe(135);
        expect(result.amountDue).toBe(45000);
        expect(result.calculationMethod).toBe("Block");

        const [url] = fetchMock.mock.calls[0];
        expect(String(url)).toContain("/api/payment/public/sessions/fee?qr=test-qr-token-123");
    });

    it("throws FeeLookupError 404 when QR is invalid or not found", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ title: "Not Found" }), {
                status: 404,
                headers: { "Content-Type": "application/problem+json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        await expect(fetchPublicParkingFee("invalid-qr")).rejects.toThrow(FeeLookupError);
        await expect(fetchPublicParkingFee("invalid-qr")).rejects.toMatchObject({
            statusCode: 404,
        });
    });

    it("throws FeeLookupError 429 with 1-minute retry advice when rate limit exceeded", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ title: "Too Many Requests" }), {
                status: 429,
                headers: { "Content-Type": "application/problem+json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        await expect(fetchPublicParkingFee("rate-limit-qr")).rejects.toThrow(FeeLookupError);
        await expect(fetchPublicParkingFee("rate-limit-qr")).rejects.toMatchObject({
            statusCode: 429,
        });
    });

    it("throws FeeLookupError 404 if qrToken is empty or whitespace", async () => {
        await expect(fetchPublicParkingFee("   ")).rejects.toThrow(FeeLookupError);
    });

    describe("formatting helpers", () => {
        it("formats VND correctly", () => {
            expect(formatVnd(45000)).toContain("45.000");
            expect(formatVnd(0)).toContain("0");
        });

        it("formats duration in Vietnamese correctly", () => {
            expect(formatDurationVi(135)).toBe("2 giờ 15 phút");
            expect(formatDurationVi(60)).toBe("1 giờ");
            expect(formatDurationVi(45)).toBe("45 phút");
            expect(formatDurationVi(0)).toBe("0 phút");
        });

        it("maps status labels correctly", () => {
            expect(mapStatusVi("Active").label).toBe("Đang đỗ");
            expect(mapStatusVi("Paid").label).toBe("Đã thanh toán");
            expect(mapStatusVi("Completed").label).toBe("Hoàn thành");
            expect(mapStatusVi("Cancelled").label).toBe("Đã hủy");
        });

        it("maps calculation method correctly", () => {
            expect(mapCalculationMethodVi("Block")).toBe("Theo block thời gian");
            expect(mapCalculationMethodVi("Turn")).toBe("Theo lượt gửi xe");
        });
    });
});

describe("publicFee - FeeLookupView 4 UI States", () => {
    beforeEach(() => {
        vi.restoreAllMocks();
        mockQueryQr = null;
    });

    it("State 1: renders loading state when querying fee", async () => {
        mockQueryQr = "loading-qr-token";
        // Never-resolving fetch to keep loading active
        const fetchMock = vi.fn().mockImplementation(() => new Promise(() => {}));
        vi.stubGlobal("fetch", fetchMock);

        render(<FeeLookupView />);

        expect(screen.getByLabelText("Đang tải dữ liệu")).toBeInTheDocument();
        expect(screen.getByText(/Đang tra cứu dữ liệu phiên đỗ…/i)).toBeInTheDocument();
    });

    it("State 2: renders success state with masked plate, duration, amount due and reload button", async () => {
        mockQueryQr = "valid-qr-token";
        const mockData = {
            sessionId: "ses-100",
            maskedPlateNumber: "51F-***.91",
            status: "Active",
            entryTimeUtc: "2026-11-22T08:00:00Z",
            calculatedAtUtc: "2026-11-22T09:30:00Z",
            durationMinutes: 90,
            calculationMethod: "Block",
            totalFee: 30000,
            paidAmount: 0,
            amountDue: 30000,
        };

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(mockData), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        render(<FeeLookupView />);

        await waitFor(() => {
            expect(screen.getByLabelText("Biển số xe: 51F-***.91")).toBeInTheDocument();
        });

        expect(screen.getByText("51F-***.91")).toBeInTheDocument();
        expect(screen.getByText("1 giờ 30 phút")).toBeInTheDocument();
        expect(screen.getByText("Theo block thời gian")).toBeInTheDocument();
        expect(screen.getByText(/Làm mới/i)).toBeInTheDocument();

        // Invariant: NO payment button and strictly anonymous
        expect(screen.queryByRole("button", { name: /Thanh toán/i })).not.toBeInTheDocument();
        expect(screen.queryByRole("button", { name: /Trả tiền/i })).not.toBeInTheDocument();
        expect(screen.getByText(/Số tiền trên là cước tạm tính đến thời điểm hiện tại/i)).toBeInTheDocument();
    });

    it("State 3: renders 404 error state when QR code is invalid", async () => {
        mockQueryQr = "invalid-qr-token";
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ title: "Not Found" }), {
                status: 404,
                headers: { "Content-Type": "application/problem+json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        render(<FeeLookupView />);

        await waitFor(() => {
            expect(screen.getByRole("alert")).toBeInTheDocument();
        });

        expect(screen.getByText("Không tìm thấy phiên đỗ")).toBeInTheDocument();
        expect(screen.getByText(/Không tìm thấy phiên đỗ hoặc mã QR không hợp lệ/i)).toBeInTheDocument();
    });

    it("State 4: renders 429 error state when rate limit exceeded (advises 1 minute wait)", async () => {
        mockQueryQr = "rate-limited-qr";
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ title: "Too Many Requests" }), {
                status: 429,
                headers: { "Content-Type": "application/problem+json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        render(<FeeLookupView />);

        await waitFor(() => {
            expect(screen.getByRole("alert")).toBeInTheDocument();
        });

        expect(screen.getByText("Vượt giới hạn tra cứu")).toBeInTheDocument();
        expect(screen.getByText(/Xin vui lòng chờ 1 phút và bấm tra cứu lại/i)).toBeInTheDocument();
    });
});
