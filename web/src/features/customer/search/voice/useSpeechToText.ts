"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export type SpeechToTextState =
    "idle" | "listening" | "processing" | "success" | "error" | "unsupported";

export interface SpeechRecognitionResultItem {
    transcript: string;
    confidence: number;
}

export interface SpeechRecognitionResultListLike {
    [index: number]: {
        [index: number]: SpeechRecognitionResultItem;
        isFinal?: boolean;
        length: number;
    };
    length: number;
}

export interface SpeechRecognitionEventLike extends Event {
    resultIndex: number;
    results: SpeechRecognitionResultListLike;
}

export interface SpeechRecognitionErrorEventLike extends Event {
    error: string;
    message?: string;
}

export interface SpeechRecognitionInstanceLike extends EventTarget {
    continuous: boolean;
    interimResults: boolean;
    lang: string;
    maxAlternatives: number;
    start: () => void;
    stop: () => void;
    abort: () => void;
    onstart: ((this: SpeechRecognitionInstanceLike, ev: Event) => void) | null;
    onend: ((this: SpeechRecognitionInstanceLike, ev: Event) => void) | null;
    onerror:
        | ((
              this: SpeechRecognitionInstanceLike,
              ev: SpeechRecognitionErrorEventLike,
          ) => void)
        | null;
    onresult:
        | ((
              this: SpeechRecognitionInstanceLike,
              ev: SpeechRecognitionEventLike,
          ) => void)
        | null;
    onaudiostart:
        ((this: SpeechRecognitionInstanceLike, ev: Event) => void) | null;
    onaudioend:
        ((this: SpeechRecognitionInstanceLike, ev: Event) => void) | null;
}

export interface UseSpeechToTextOptions {
    /** Fixed to "vi-VN" by default as per project requirements */
    lang?: string;
    onSuccess?: (transcript: string) => void;
    onError?: (errorMessage: string, errorCode: string) => void;
}

export interface UseSpeechToTextReturn {
    state: SpeechToTextState;
    transcript: string;
    error: string | null;
    errorCode: string | null;
    isSupported: boolean;
    isListening: boolean;
    startListening: () => void;
    stopListening: () => void;
    reset: () => void;
}

function getSpeechRecognitionConstructor():
    (new () => SpeechRecognitionInstanceLike) | null {
    if (typeof window === "undefined") return null;

    // Check secure context if property exists
    if ("isSecureContext" in window && !window.isSecureContext) {
        return null;
    }

    const windowAny = window as unknown as {
        SpeechRecognition?: new () => SpeechRecognitionInstanceLike;
        webkitSpeechRecognition?: new () => SpeechRecognitionInstanceLike;
    };

    return (
        windowAny.SpeechRecognition || windowAny.webkitSpeechRecognition || null
    );
}

export function mapSpeechError(errorCode: string): string {
    switch (errorCode) {
        case "not-allowed":
        case "permission-denied":
            return "Bạn đã từ chối quyền microphone. Vui lòng cho phép quyền trong cài đặt trình duyệt để tìm kiếm bằng giọng nói.";
        case "no-speech":
            return "Không phát hiện thấy giọng nói. Vui lòng thử lại và nói rõ ràng hơn.";
        case "network":
            return "Lỗi kết nối mạng khi xử lý nhận diện giọng nói. Vui lòng thử lại.";
        case "audio-capture":
            return "Không tìm thấy thiết bị thu âm microphone. Vui lòng kiểm tra lại thiết bị.";
        case "aborted":
            return "Quá trình nhận diện giọng nói đã bị hủy.";
        case "service-not-allowed":
            return "Dịch vụ nhận diện giọng nói bị từ chối hoặc bị hạn chế trên trình duyệt.";
        default:
            return "Đã xảy ra lỗi khi nhận diện giọng nói. Vui lòng thử lại.";
    }
}

export function useSpeechToText(
    options: UseSpeechToTextOptions = {},
): UseSpeechToTextReturn {
    const { lang = "vi-VN", onSuccess, onError } = options;

    const [isSupported, setIsSupported] = useState(false);
    const [state, setState] = useState<SpeechToTextState>("idle");
    const [transcript, setTranscript] = useState("");
    const [error, setError] = useState<string | null>(null);
    const [errorCode, setErrorCode] = useState<string | null>(null);

    const recognitionRef = useRef<SpeechRecognitionInstanceLike | null>(null);
    const stateRef = useRef<SpeechToTextState>(state);
    stateRef.current = state;

    useEffect(() => {
        const Ctor = getSpeechRecognitionConstructor();
        if (Ctor) {
            setIsSupported(true);
            setState("idle");
        } else {
            setIsSupported(false);
            setState("unsupported");
        }
    }, []);

    const stopListening = useCallback(() => {
        if (recognitionRef.current) {
            try {
                recognitionRef.current.stop();
            } catch {
                // Ignore if already stopped
            }
        }
    }, []);

    const reset = useCallback(() => {
        if (recognitionRef.current) {
            try {
                recognitionRef.current.abort();
            } catch {
                // Ignore
            }
        }
        setTranscript("");
        setError(null);
        setErrorCode(null);
        setState(isSupported ? "idle" : "unsupported");
    }, [isSupported]);

    const startListening = useCallback(() => {
        const Ctor = getSpeechRecognitionConstructor();
        if (!Ctor) {
            setState("unsupported");
            setError(
                "Trình duyệt không hỗ trợ nhận diện giọng nói Web Speech API.",
            );
            setErrorCode("unsupported");
            return;
        }

        // Abort previous instance if any
        if (recognitionRef.current) {
            try {
                recognitionRef.current.abort();
            } catch {
                // Ignore
            }
        }

        try {
            const instance = new Ctor();

            // Single sentence capture invariants
            instance.lang = lang;
            instance.continuous = false;
            instance.interimResults = false;
            instance.maxAlternatives = 1;

            instance.onstart = () => {
                setState("listening");
                setError(null);
                setErrorCode(null);
                setTranscript("");
            };

            instance.onaudioend = () => {
                if (stateRef.current === "listening") {
                    setState("processing");
                }
            };

            instance.onresult = (event: SpeechRecognitionEventLike) => {
                const results = event.results;
                if (
                    results &&
                    results.length > 0 &&
                    results[0] &&
                    results[0][0]
                ) {
                    const text = results[0][0].transcript?.trim() ?? "";
                    setTranscript(text);
                    setState("success");
                    onSuccess?.(text);
                }
            };

            instance.onerror = (event: SpeechRecognitionErrorEventLike) => {
                const code = event.error || "unknown";
                const message = mapSpeechError(code);
                setErrorCode(code);
                setError(message);
                setState("error");
                onError?.(message, code);
            };

            instance.onend = () => {
                // If stopped while listening or processing without result or error, return to idle
                if (
                    stateRef.current === "listening" ||
                    stateRef.current === "processing"
                ) {
                    setState("idle");
                }
            };

            recognitionRef.current = instance;
            instance.start();
        } catch (err) {
            const message =
                err instanceof Error
                    ? err.message
                    : "Không thể khởi động microphone";
            setError(message);
            setErrorCode("start-failure");
            setState("error");
            onError?.(message, "start-failure");
        }
    }, [lang, onSuccess, onError]);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            if (recognitionRef.current) {
                try {
                    recognitionRef.current.abort();
                } catch {
                    // Ignore
                }
            }
        };
    }, []);

    return {
        state,
        transcript,
        error,
        errorCode,
        isSupported,
        isListening: state === "listening",
        startListening,
        stopListening,
        reset,
    };
}
