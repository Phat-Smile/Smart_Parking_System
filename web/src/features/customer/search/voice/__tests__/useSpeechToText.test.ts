import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useSpeechToText, mapSpeechError } from "../useSpeechToText";

class MockSpeechRecognition extends EventTarget {
    continuous = false;
    interimResults = false;
    lang = "vi-VN";
    maxAlternatives = 1;

    start = vi.fn(() => {
        if (this.onstart) {
            this.onstart(new Event("start"));
        }
    });

    stop = vi.fn(() => {
        if (this.onend) {
            this.onend(new Event("end"));
        }
    });

    abort = vi.fn(() => {
        if (this.onend) {
            this.onend(new Event("end"));
        }
    });

    onstart: ((ev: Event) => void) | null = null;
    onend: ((ev: Event) => void) | null = null;
    onerror: ((ev: any) => void) | null = null;
    onresult: ((ev: any) => void) | null = null;
    onaudiostart: ((ev: Event) => void) | null = null;
    onaudioend: ((ev: Event) => void) | null = null;
}

describe("useSpeechToText", () => {
    const originalSpeechRecognition = (window as any).SpeechRecognition;
    const originalWebkitSpeechRecognition = (window as any)
        .webkitSpeechRecognition;

    beforeEach(() => {
        delete (window as any).SpeechRecognition;
        delete (window as any).webkitSpeechRecognition;
    });

    afterEach(() => {
        (window as any).SpeechRecognition = originalSpeechRecognition;
        (window as any).webkitSpeechRecognition =
            originalWebkitSpeechRecognition;
    });

    it("returns unsupported state when Web Speech API is missing from window", () => {
        const { result } = renderHook(() => useSpeechToText());

        expect(result.current.isSupported).toBe(false);
        expect(result.current.state).toBe("unsupported");
    });

    it("initializes to idle state when SpeechRecognition is supported", () => {
        (window as any).SpeechRecognition = MockSpeechRecognition;

        const { result } = renderHook(() => useSpeechToText());

        expect(result.current.isSupported).toBe(true);
        expect(result.current.state).toBe("idle");
    });

    it("transitions to listening when startListening is invoked", () => {
        (window as any).SpeechRecognition = MockSpeechRecognition;

        const { result } = renderHook(() => useSpeechToText());

        act(() => {
            result.current.startListening();
        });

        expect(result.current.state).toBe("listening");
        expect(result.current.isListening).toBe(true);
    });

    it("captures speech transcript and transitions to success state", () => {
        (window as any).SpeechRecognition = MockSpeechRecognition;
        const onSuccess = vi.fn();

        let activeInstance: MockSpeechRecognition | null = null;
        class CustomMock extends MockSpeechRecognition {
            constructor() {
                super();
                activeInstance = this;
            }
        }
        (window as any).SpeechRecognition = CustomMock;

        const { result } = renderHook(() => useSpeechToText({ onSuccess }));

        act(() => {
            result.current.startListening();
        });

        expect(result.current.state).toBe("listening");
        expect(activeInstance).not.toBeNull();

        // Simulate onresult event
        act(() => {
            if (activeInstance && activeInstance.onresult) {
                activeInstance.onresult({
                    resultIndex: 0,
                    results: [
                        [
                            {
                                transcript: "Chợ Bến Thành Quận 1",
                                confidence: 0.95,
                            },
                        ],
                    ] as any,
                } as any);
            }
        });

        expect(result.current.state).toBe("success");
        expect(result.current.transcript).toBe("Chợ Bến Thành Quận 1");
        expect(onSuccess).toHaveBeenCalledWith("Chợ Bến Thành Quận 1");
    });

    it("handles not-allowed error when microphone permission is denied", () => {
        (window as any).SpeechRecognition = MockSpeechRecognition;
        const onError = vi.fn();

        let activeInstance: MockSpeechRecognition | null = null;
        class CustomMock extends MockSpeechRecognition {
            constructor() {
                super();
                activeInstance = this;
            }
        }
        (window as any).SpeechRecognition = CustomMock;

        const { result } = renderHook(() => useSpeechToText({ onError }));

        act(() => {
            result.current.startListening();
        });

        // Simulate error event
        act(() => {
            if (activeInstance && activeInstance.onerror) {
                activeInstance.onerror({ error: "not-allowed" } as any);
            }
        });

        expect(result.current.state).toBe("error");
        expect(result.current.errorCode).toBe("not-allowed");
        expect(result.current.error).toContain(
            "Bạn đã từ chối quyền microphone",
        );
        expect(onError).toHaveBeenCalledWith(
            expect.stringContaining("Bạn đã từ chối quyền microphone"),
            "not-allowed",
        );
    });

    it("resets state when reset is called", () => {
        (window as any).SpeechRecognition = MockSpeechRecognition;

        const { result } = renderHook(() => useSpeechToText());

        act(() => {
            result.current.startListening();
        });
        expect(result.current.state).toBe("listening");

        act(() => {
            result.current.reset();
        });
        expect(result.current.state).toBe("idle");
        expect(result.current.transcript).toBe("");
        expect(result.current.error).toBeNull();
    });

    describe("mapSpeechError", () => {
        it("maps known speech recognition error codes to Vietnamese instructions", () => {
            expect(mapSpeechError("not-allowed")).toContain(
                "Bạn đã từ chối quyền microphone",
            );
            expect(mapSpeechError("no-speech")).toContain(
                "Không phát hiện thấy giọng nói",
            );
            expect(mapSpeechError("network")).toContain("Lỗi kết nối mạng");
            expect(mapSpeechError("audio-capture")).toContain(
                "Không tìm thấy thiết bị thu âm",
            );
            expect(mapSpeechError("aborted")).toContain("đã bị hủy");
            expect(mapSpeechError("unknown-code")).toContain("Đã xảy ra lỗi");
        });
    });
});
