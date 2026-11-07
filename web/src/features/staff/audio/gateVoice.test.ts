import { describe, expect, it, beforeEach } from "vitest";
import {
    formatPlateForSpeech,
    formatSlotForSpeech,
    buildCheckInSpeech,
    buildCheckOutSpeech,
    buildWarningSpeech,
} from "./phoneticHelper";
import { gateVoiceService } from "./GateVoiceService";

describe("phoneticHelper - Chuẩn hóa giọng đọc tiếng Việt", () => {
    it("chuẩn hóa biển số xe dạng có dấu chấm 51F-123.45 chính xác", () => {
        const speech = formatPlateForSpeech("51F-123.45");
        expect(speech).toContain("5 1 Ép");
        expect(speech).toContain("một hai ba, bốn năm");
    });

    it("chuẩn hóa biển số xe 30A-99999 chính xác", () => {
        const speech = formatPlateForSpeech("30A-99999");
        expect(speech).toContain("3 0 A");
        expect(speech).toContain("9 9 9, 9 9");
    });

    it("chuẩn hóa biển số xe chữ ghép 29LD-001.23", () => {
        const speech = formatPlateForSpeech("29LD-001.23");
        expect(speech).toContain("2 9 Lờ Đê");
        expect(speech).toContain("không không một, hai ba");
    });

    it("chuẩn hóa ô đỗ xe định dạng A-01 và B-12", () => {
        expect(formatSlotForSpeech("A-01")).toBe("ô A không một");
        expect(formatSlotForSpeech("B-12")).toBe("ô Bê 12");
        expect(formatSlotForSpeech("CAR-A")).toBe("khu vực ô tô A");
        expect(formatSlotForSpeech("MOTO-B")).toBe("khu vực xe máy B");
        expect(formatSlotForSpeech("")).toBe("khu vực đã được hướng dẫn");
    });

    it("tạo câu thoại Check-in chuẩn format nghiệp vụ", () => {
        const speech = buildCheckInSpeech({
            plateNumber: "51F-123.45",
            slotCode: "A-01",
            lotName: "Bãi A",
        });

        expect(speech).toContain("Kính chào quý khách!");
        expect(speech).toContain("5 1 Ép");
        expect(speech).toContain("ô A không một");
        expect(speech).toContain("Chúc quý khách một ngày tốt lành!");
    });

    it("tạo câu thoại Check-out chuẩn format nghiệp vụ", () => {
        const speech = buildCheckOutSpeech({
            plateNumber: "51F-123.45",
            lotName: "Bãi A",
        });

        expect(speech).toContain("Cảm ơn quý khách!");
        expect(speech).toContain("5 1 Ép");
        expect(speech).toContain("thanh toán thành công");
        expect(speech).toContain("Barrier đang mở");
        expect(speech).toContain("thượng lộ bình an");
    });

    it("tạo câu thoại Cảnh báo lỗi cổng bãi", () => {
        const customWarning = buildWarningSpeech("Thẻ chưa được kích hoạt");
        expect(customWarning).toContain("Cảnh báo! Thẻ chưa được kích hoạt.");

        const defaultWarning = buildWarningSpeech();
        expect(defaultWarning).toContain(
            "Vé không hợp lệ hoặc xe chưa thanh toán phí đỗ",
        );
    });
});

describe("GateVoiceService - Quản lý âm thanh cổng bãi", () => {
    beforeEach(() => {
        gateVoiceService.setEnabled(true);
        gateVoiceService.setVolume(1.0);
        gateVoiceService.setRate(1.0);
    });

    it("cập nhật cài đặt volume và rate chính xác", () => {
        gateVoiceService.setVolume(0.8);
        expect(gateVoiceService.getSettings().volume).toBe(0.8);

        gateVoiceService.setRate(1.2);
        expect(gateVoiceService.getSettings().rate).toBe(1.2);
    });

    it("bật tắt toggle âm thanh chuẩn", () => {
        gateVoiceService.setEnabled(false);
        expect(gateVoiceService.isEnabled()).toBe(false);

        const newStatus = gateVoiceService.toggleEnabled();
        expect(newStatus).toBe(true);
        expect(gateVoiceService.isEnabled()).toBe(true);
    });
});
