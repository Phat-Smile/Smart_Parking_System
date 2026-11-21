"use client";

import { useState } from "react";
import {
    Mic,
    MicOff,
    Loader2,
    AlertCircle,
    X,
    ShieldCheck,
} from "lucide-react";
import { useSpeechToText } from "./useSpeechToText";
import "./voice-search.css";

export interface VoiceSearchButtonProps {
    onResult: (transcript: string) => void;
    hideIfUnsupported?: boolean;
    className?: string;
    disabled?: boolean;
}

export function VoiceSearchButton({
    onResult,
    hideIfUnsupported = true,
    className = "",
    disabled = false,
}: VoiceSearchButtonProps) {
    const [showPermissionGuide, setShowPermissionGuide] = useState(false);

    const {
        state,
        isSupported,
        isListening,
        errorCode,
        startListening,
        stopListening,
        reset,
    } = useSpeechToText({
        lang: "vi-VN",
        onSuccess: (text) => {
            if (text) {
                onResult(text);
            }
        },
        onError: (_msg, code) => {
            if (code === "not-allowed" || code === "permission-denied") {
                setShowPermissionGuide(true);
            }
        },
    });

    // If unsupported by browser/environment
    if (!isSupported) {
        if (hideIfUnsupported) {
            return null;
        }
        return (
            <div className={`pw-voice-container ${className}`}>
                <button
                    type="button"
                    className="pw-voice-btn"
                    disabled
                    aria-label="Trình duyệt không hỗ trợ tìm kiếm bằng giọng nói"
                    title="Trình duyệt không hỗ trợ Web Speech API"
                >
                    <MicOff size={20} />
                </button>
            </div>
        );
    }

    const handleClick = () => {
        if (isListening) {
            stopListening();
        } else {
            setShowPermissionGuide(false);
            startListening();
        }
    };

    const getAriaLabel = (): string => {
        switch (state) {
            case "listening":
                return "Đang nghe giọng nói, vui lòng nói điểm đến...";
            case "processing":
                return "Đang xử lý giọng nói...";
            case "error":
                return "Lỗi nhận diện giọng nói, nhấn để thử lại";
            default:
                return "Tìm kiếm bãi đỗ bằng giọng nói";
        }
    };

    return (
        <div className={`pw-voice-container ${className}`}>
            <button
                type="button"
                className={`pw-voice-btn ${isListening ? "is-listening" : ""} ${state === "processing" ? "is-processing" : ""} ${state === "error" ? "is-error" : ""}`}
                onClick={handleClick}
                disabled={disabled}
                aria-label={getAriaLabel()}
                aria-pressed={isListening}
                title={getAriaLabel()}
                data-testid="voice-search-button"
            >
                {state === "processing" ? (
                    <Loader2 size={20} className="pw-spin-icon" />
                ) : state === "error" &&
                  (errorCode === "not-allowed" ||
                      errorCode === "permission-denied") ? (
                    <MicOff size={20} />
                ) : (
                    <Mic size={20} />
                )}
            </button>

            {/* Permission Denied Instruction Guide */}
            {showPermissionGuide && (
                <div
                    className="pw-voice-permission-guide"
                    role="alert"
                    aria-live="polite"
                    data-testid="voice-permission-guide"
                >
                    <div className="pw-voice-guide-header">
                        <span className="pw-voice-guide-title">
                            <AlertCircle size={16} />
                            Chưa cấp quyền microphone
                        </span>
                        <button
                            type="button"
                            className="pw-voice-guide-close"
                            onClick={() => setShowPermissionGuide(false)}
                            aria-label="Đóng hướng dẫn cấp quyền"
                        >
                            <X size={16} />
                        </button>
                    </div>

                    <p className="pw-voice-guide-desc">
                        Trình duyệt đang chặn truy cập microphone. Để tìm kiếm
                        bằng giọng nói tiếng Việt, bạn vui lòng cấp quyền:
                    </p>

                    <ol className="pw-voice-guide-steps">
                        <li>
                            Nhấp vào biểu tượng{" "}
                            <strong>ổ khóa hoặc cài đặt trang web</strong> trên
                            thanh địa chỉ.
                        </li>
                        <li>
                            Chuyển quyền <strong>Microphone</strong> sang{" "}
                            <strong>Cho phép (Allow)</strong>.
                        </li>
                        <li>Tải lại trang và thử lại.</li>
                    </ol>

                    <div className="pw-voice-privacy-note">
                        ParkWise tuyệt đối không lưu trữ hay gửi âm thanh của
                        bạn về máy chủ.
                    </div>
                </div>
            )}
        </div>
    );
}
