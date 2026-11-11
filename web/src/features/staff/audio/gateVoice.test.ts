import { describe, expect, it, beforeEach, afterEach, vi } from "vitest";
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

describe("chimeGenerator & GateVoiceService - chống kẹt hàng đợi audio", () => {
    beforeEach(() => {
        vi.resetModules();
        vi.useFakeTimers();
    });

    afterEach(() => {
        vi.useRealTimers();
        vi.unstubAllGlobals();
        vi.restoreAllMocks();
    });

    it("playGateChime timeouts AudioContext.resume() after ~1s instead of hanging forever", async () => {
        const createOscillator = vi.fn();
        class HangingAudioContext {
            state = "suspended";
            currentTime = 0;
            destination = {};
            resume = vi.fn(() => new Promise<void>(() => {}));
            createOscillator = createOscillator;
            createGain = vi.fn();
        }
        vi.stubGlobal("AudioContext", HangingAudioContext);

        const { playGateChime } = await import("./chimeGenerator");
        let finished = false;
        const promise = playGateChime(0.4).then(() => {
            finished = true;
        });

        await vi.advanceTimersByTimeAsync(1100);
        await Promise.resolve();

        expect(finished).toBe(true);
        await promise;
        expect(createOscillator).not.toHaveBeenCalled();
    });

    it("resets processingQueue in finally when speak throws not-allowed and recovers for subsequent utterances", async () => {
        class MockUtterance {
            text: string;
            lang = "";
            rate = 1;
            pitch = 1;
            volume = 1;
            voice: unknown = null;
            onend: (() => void) | null = null;
            onerror: ((ev: { error: string }) => void) | null = null;
            constructor(text: string) {
                this.text = text;
            }
        }

        const spokenTexts: string[] = [];
        let shouldThrow = true;
        const mockSynth = {
            paused: false,
            onvoiceschanged: null as (() => void) | null,
            getVoices: () => [],
            cancel: vi.fn(),
            resume: vi.fn(),
            speak: vi.fn((utterance: MockUtterance) => {
                if (shouldThrow) {
                    shouldThrow = false;
                    throw new DOMException("not-allowed", "NotAllowedError");
                }
                spokenTexts.push(utterance.text);
                utterance.onend?.();
            }),
        };

        vi.stubGlobal("speechSynthesis", mockSynth);
        vi.stubGlobal("SpeechSynthesisUtterance", MockUtterance);

        const { gateVoiceService: service } =
            await import("./GateVoiceService");
        service.setEnabled(true);
        service.setPlayChime(false);

        await service
            .speak({ text: "Xe đầu tiên", playChime: false })
            .catch(() => {});
        await vi.advanceTimersByTimeAsync(100);

        expect(
            (service as unknown as { processingQueue: boolean })
                .processingQueue,
        ).toBe(false);

        await service.speak({ text: "Xe thứ hai", playChime: false });
        await vi.advanceTimersByTimeAsync(100);

        expect(spokenTexts).toContain("Xe thứ hai");
        expect(
            (service as unknown as { processingQueue: boolean })
                .processingQueue,
        ).toBe(false);
    });

    it("does not block speech queue when chime AudioContext.resume rejects with not-allowed", async () => {
        class RejectingAudioContext {
            state = "suspended";
            currentTime = 0;
            destination = {};
            resume = vi.fn(() =>
                Promise.reject(
                    new DOMException("not-allowed", "NotAllowedError"),
                ),
            );
            createOscillator = vi.fn();
            createGain = vi.fn();
        }
        class MockUtterance {
            text: string;
            lang = "";
            rate = 1;
            pitch = 1;
            volume = 1;
            voice: unknown = null;
            onend: (() => void) | null = null;
            onerror: ((ev: { error: string }) => void) | null = null;
            constructor(text: string) {
                this.text = text;
            }
        }
        const spokenTexts: string[] = [];
        const mockSynth = {
            paused: false,
            onvoiceschanged: null,
            getVoices: () => [],
            cancel: vi.fn(),
            resume: vi.fn(),
            speak: vi.fn((utterance: MockUtterance) => {
                spokenTexts.push(utterance.text);
                utterance.onerror?.({ error: "not-allowed" });
            }),
        };

        vi.stubGlobal("AudioContext", RejectingAudioContext);
        vi.stubGlobal("speechSynthesis", mockSynth);
        vi.stubGlobal("SpeechSynthesisUtterance", MockUtterance);

        const { gateVoiceService: service } =
            await import("./GateVoiceService");
        service.setEnabled(true);
        service.setPlayChime(true);

        const p = service.speak({ text: "Xe test", playChime: true });
        await vi.advanceTimersByTimeAsync(1100);
        await p;

        expect(spokenTexts).toContain("Xe test");
        expect(
            (service as unknown as { processingQueue: boolean })
                .processingQueue,
        ).toBe(false);
    });

    it("stops stale processQueue run immediately after urgent stop() so it does not steal items from the new urgent run", async () => {
        class MockUtterance {
            text: string;
            lang = "";
            rate = 1;
            pitch = 1;
            volume = 1;
            voice: unknown = null;
            onend: (() => void) | null = null;
            onerror: ((ev: { error: string }) => void) | null = null;
            constructor(text: string) {
                this.text = text;
            }
        }

        const utterances: MockUtterance[] = [];
        const mockSynth = {
            paused: false,
            onvoiceschanged: null,
            getVoices: () => [],
            cancel: vi.fn(),
            resume: vi.fn(),
            speak: vi.fn((utterance: MockUtterance) => {
                utterances.push(utterance);
            }),
        };

        vi.stubGlobal("speechSynthesis", mockSynth);
        vi.stubGlobal("SpeechSynthesisUtterance", MockUtterance);

        const { gateVoiceService: service } =
            await import("./GateVoiceService");
        service.setEnabled(true);
        service.setPlayChime(false);

        const oldPromise = service.speak({
            text: "Câu cũ chưa xong",
            playChime: false,
        });
        await Promise.resolve();
        expect(utterances.map((u) => u.text)).toEqual(["Câu cũ chưa xong"]);

        const urgentPromise = service.speak({
            text: "Cảnh báo khẩn cấp",
            urgent: true,
            playChime: false,
        });
        const afterUrgentPromise = service.speak({
            text: "Câu sau khẩn cấp",
            playChime: false,
        });
        await Promise.resolve();
        expect(utterances.map((u) => u.text)).toEqual([
            "Câu cũ chưa xong",
            "Cảnh báo khẩn cấp",
        ]);

        // Câu cũ kết thúc sau khi đã bị urgent stop(): luồng cũ không được rút "Câu sau khẩn cấp"
        utterances[0].onend?.();
        await Promise.resolve();
        await Promise.resolve();

        expect(utterances.map((u) => u.text)).toEqual([
            "Câu cũ chưa xong",
            "Cảnh báo khẩn cấp",
        ]);

        // Chỉ khi câu khẩn cấp kết thúc, luồng mới mới phát "Câu sau khẩn cấp"
        utterances[1].onend?.();
        await Promise.resolve();
        await Promise.resolve();

        expect(utterances.map((u) => u.text)).toEqual([
            "Câu cũ chưa xong",
            "Cảnh báo khẩn cấp",
            "Câu sau khẩn cấp",
        ]);

        utterances[2].onend?.();
        await Promise.all([oldPromise, urgentPromise, afterUrgentPromise]);
        expect(
            (service as unknown as { processingQueue: boolean })
                .processingQueue,
        ).toBe(false);
    });
});
