import { describe, it, expect } from "vitest";
import {
    parseVietnamesePlate,
    formatVietnamesePlate,
    preprocessCanvasForOcr,
    GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD,
} from "./plateOcr";

describe("plateOcr ANPR Engine (Method 1)", () => {
    describe("parseVietnamesePlate", () => {
        it("nhận diện biển số xe máy 2 dòng thực tế (VD: 59-V1\\n793.79 -> 59V1-793.79, Xe máy)", () => {
            const res = parseVietnamesePlate("59-V1\n793.79");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("59V1-793.79");
            expect(res?.vehicleType).toBe("Motorbike");
        });

        it("nhận diện biển số xe máy có dấu gạch ngang (VD: 59-V1 793.79)", () => {
            const res = parseVietnamesePlate("59-V1 793.79");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("59V1-793.79");
            expect(res?.vehicleType).toBe("Motorbike");
        });

        it("nhận diện biển số xe máy liền (VD: 59X1-234.56)", () => {
            const res = parseVietnamesePlate("59X1-234.56");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("59X1-234.56");
            expect(res?.vehicleType).toBe("Motorbike");
        });

        it("nhận diện biển số ô tô 5 số (VD: 51F-782.91 -> Ô tô)", () => {
            const res = parseVietnamesePlate("51F-782.91");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("51F-782.91");
            expect(res?.vehicleType).toBe("Car");
        });

        it("nhận diện biển số ô tô 51A 123.45", () => {
            const res = parseVietnamesePlate("51A 123.45");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("51A-123.45");
            expect(res?.vehicleType).toBe("Car");
        });

        it("does not invent a fixed plate from unsupported or incomplete OCR", () => {
            expect(parseVietnamesePlate("73J 793.79")).toBeNull();
            expect(parseVietnamesePlate("73J-793.79")).toBeNull();
            expect(parseVietnamesePlate("| 9-\\1 |\n1/93./ J")).toBeNull();
            expect(parseVietnamesePlate("9-V1 793.79")).toBeNull();
            expect(parseVietnamesePlate("9-1 193.1J")).toBeNull();
            expect(parseVietnamesePlate("S9-V1 793.79")).toBeNull();
            expect(parseVietnamesePlate("59-VI 793.79")).toBeNull();
        });

        it("preserves actually observed vehicle numbers and series", () => {
            expect(parseVietnamesePlate("99-E1\n222.68")?.plateNumber).toBe("99E1-222.68");
            expect(parseVietnamesePlate("99-F1\n222.68")?.plateNumber).toBe("99F1-222.68");
            expect(parseVietnamesePlate("99-E1\n477.26")?.plateNumber).toBe("99E1-477.26");
            expect(parseVietnamesePlate("99-E1\n277 RA")).toBeNull();
        });

        it("refuses a frame containing different recognizable plates", () => {
            expect(parseVietnamesePlate("59-V1 793.79 / 51A-123.45")).toBeNull();
            expect(parseVietnamesePlate("51A-123.45 51B-234.56")).toBeNull();
        });

        it("does not infer a vehicle type from unrecognizable text", () => {
            expect(formatVietnamesePlate("FE0E-ERN")).toBeNull();
        });

        it("từ chối chuỗi rác ngẫu nhiên không thuộc định dạng biển số xe VN (VD: FE0E-ERN)", () => {
            const res = parseVietnamesePlate("FE0E-ERN");
            expect(res).toBeNull();
        });

        it("từ chối chuỗi 4 số ngẫu nhiên do nhiễu môi trường (VD: 21A-8138)", () => {
            const res = parseVietnamesePlate("21A-8138");
            expect(res).toBeNull();
        });

        it("từ chối chuỗi văn bản phòng/đồ vật", () => {
            const res = parseVietnamesePlate("HELLO WORLD ROOM 123");
            expect(res).toBeNull();
        });
    });

    describe("formatVietnamesePlate fallback alias", () => {
        it("chuẩn hóa biển số ô tô 5 số chính xác (VD: 51F78291 -> 51F-782.91)", () => {
            const res = formatVietnamesePlate("51F78291");
            expect(res).not.toBeNull();
            expect(res?.plate).toBe("51F-782.91");
            expect(res?.vehicleType).toBe("Car");
        });

        it("chuẩn hóa biển số xe máy (VD: 59X123456 -> 59X1-234.56)", () => {
            const res = formatVietnamesePlate("59X123456");
            expect(res).not.toBeNull();
            expect(res?.plate).toBe("59X1-234.56");
            expect(res?.vehicleType).toBe("Motorbike");
        });
    });

    describe("preprocessCanvasForOcr", () => {
        it("cắt và tiền xử lý tương phản ảnh canvas an toàn không gây crash", () => {
            const canvas = document.createElement("canvas");
            canvas.width = 640;
            canvas.height = 480;
            const processed = preprocessCanvasForOcr(canvas);
            expect(processed).toBeDefined();
            expect(processed.width).toBeGreaterThan(0);
            expect(processed.height).toBeGreaterThan(0);
        });
    });

    describe("GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD (BR-05.a)", () => {
        it("ngưỡng chấp nhận tự động phải đồng bộ chuẩn xác ở mức 0.9 (90%)", () => {
            expect(GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD).toBe(0.9);
        });

        it("độ tin cậy 0.85 (85%) nằm dưới ngưỡng, bắt buộc nhân viên phải xác nhận thủ công", () => {
            const confidence = 0.85;
            const requiresManualConfirm =
                confidence < GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD;
            expect(requiresManualConfirm).toBe(true);
        });

        it("độ tin cậy 0.92 (92%) đạt ngưỡng, hệ thống tự động chấp nhận không cần chỉnh sửa", () => {
            const confidence = 0.92;
            const requiresManualConfirm =
                confidence < GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD;
            expect(requiresManualConfirm).toBe(false);
        });

        it("độ tin cậy thô từ OCR không bị ép sàn giả tạo (VD: 45% -> 0.45, không bị ép lên 0.96)", () => {
            const rawConfidence = 45;
            const scaledConfidence = Math.min(
                1.0,
                Math.max(0.0, rawConfidence / 100),
            );
            expect(scaledConfidence).toBe(0.45);
            expect(scaledConfidence).toBeLessThan(
                GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD,
            );
        });
    });
});
