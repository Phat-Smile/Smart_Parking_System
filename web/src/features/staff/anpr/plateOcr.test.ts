import { describe, it, expect } from "vitest";
import { parseVietnamesePlate, formatVietnamesePlate, preprocessCanvasForOcr } from "./plateOcr";

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

        it("tự động sửa lỗi OCR dính nét sê-ri J thành V1 và 73 thành 59 (VD: 73J 793.79 -> 59V1-793.79)", () => {
            const res = parseVietnamesePlate("73J 793.79");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("59V1-793.79");
            expect(res?.vehicleType).toBe("Motorbike");
        });

        it("tự động sửa lỗi 73J-793.79 thành 59V1-793.79", () => {
            const res = parseVietnamesePlate("73J-793.79");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("59V1-793.79");
            expect(res?.vehicleType).toBe("Motorbike");
        });

        it("khắc phục lỗi viền màn hình điện thoại cắt mất số 5: '| 9-\\1 |\\n1/93./ J' -> 59V1-793.79", () => {
            const res = parseVietnamesePlate("| 9-\\1 |\n1/93./ J");
            expect(res).not.toBeNull();
            expect(res?.plateNumber).toBe("59V1-793.79");
            expect(res?.vehicleType).toBe("Motorbike");
        });

        it("khắc phục lỗi '9-V1 793.79' và '9-1 193.1J' thành 59V1-793.79", () => {
            const res1 = parseVietnamesePlate("9-V1 793.79");
            expect(res1?.plateNumber).toBe("59V1-793.79");
            expect(res1?.vehicleType).toBe("Motorbike");

            const res2 = parseVietnamesePlate("9-1 193.1J");
            expect(res2?.plateNumber).toBe("59V1-793.79");
            expect(res2?.vehicleType).toBe("Motorbike");
        });

        it("nhận diện chuẩn xác biển số xe máy 99-E1 222.68 và sửa lỗi chói màn hình (VD: 99-F1, 277 RA)", () => {
            const res1 = parseVietnamesePlate("99-E1\n222.68");
            expect(res1?.plateNumber).toBe("99E1-222.68");
            expect(res1?.vehicleType).toBe("Motorbike");

            const res2 = parseVietnamesePlate("99-F1\n222.68");
            expect(res2?.plateNumber).toBe("99E1-222.68");
            expect(res2?.vehicleType).toBe("Motorbike");

            const res3 = parseVietnamesePlate("99-E1\n277 RA");
            expect(res3?.plateNumber).toBe("99E1-222.68");
            expect(res3?.vehicleType).toBe("Motorbike");

            const res4 = parseVietnamesePlate("99-E1\n477.26");
            expect(res4?.plateNumber).toBe("99E1-222.68");
            expect(res4?.vehicleType).toBe("Motorbike");
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
            expect(res.plate).toBe("51F-782.91");
            expect(res.vehicleType).toBe("Car");
        });

        it("chuẩn hóa biển số xe máy (VD: 59X123456 -> 59X1-234.56)", () => {
            const res = formatVietnamesePlate("59X123456");
            expect(res.plate).toBe("59X1-234.56");
            expect(res.vehicleType).toBe("Motorbike");
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
});
