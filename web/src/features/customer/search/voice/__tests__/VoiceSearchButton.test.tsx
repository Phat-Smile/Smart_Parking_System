import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { VoiceSearchButton } from "../VoiceSearchButton";

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

describe("VoiceSearchButton", () => {
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

    it("renders null by default when browser does not support Web Speech API", () => {
        const onResult = vi.fn();
        const { container } = render(<VoiceSearchButton onResult={onResult} />);

        expect(container.firstChild).toBeNull();
    });

    it("renders disabled button when unsupported and hideIfUnsupported is false", () => {
        const onResult = vi.fn();
        render(
            <VoiceSearchButton onResult={onResult} hideIfUnsupported={false} />,
        );

        const btn = screen.getByRole("button", {
            name: /Trình duyệt không hỗ trợ tìm kiếm bằng giọng nói/i,
        });
        expect(btn).toBeInTheDocument();
        expect(btn).toBeDisabled();
    });

    it("renders active button with Vietnamese aria-label and min 44px class when supported", () => {
        (window as any).SpeechRecognition = MockSpeechRecognition;
        const onResult = vi.fn();

        render(<VoiceSearchButton onResult={onResult} />);

        const btn = screen.getByRole("button", {
            name: "Tìm kiếm bãi đỗ bằng giọng nói",
        });
        expect(btn).toBeInTheDocument();
        expect(btn).not.toBeDisabled();
        expect(btn).toHaveClass("pw-voice-btn");
    });

    it("toggles listening state on click", async () => {
        const user = userEvent.setup();
        (window as any).SpeechRecognition = MockSpeechRecognition;
        const onResult = vi.fn();

        render(<VoiceSearchButton onResult={onResult} />);

        const btn = screen.getByRole("button", {
            name: "Tìm kiếm bãi đỗ bằng giọng nói",
        });
        await user.click(btn);

        // While listening
        expect(btn).toHaveClass("is-listening");
        expect(btn).toHaveAttribute("aria-pressed", "true");
        expect(btn).toHaveAttribute(
            "aria-label",
            "Đang nghe giọng nói, vui lòng nói điểm đến...",
        );
    });

    it("invokes onResult with recognized speech text", async () => {
        const user = userEvent.setup();
        let activeInstance: MockSpeechRecognition | null = null;
        class CustomMock extends MockSpeechRecognition {
            constructor() {
                super();
                activeInstance = this;
            }
        }
        (window as any).SpeechRecognition = CustomMock;
        const onResult = vi.fn();

        render(<VoiceSearchButton onResult={onResult} />);

        const btn = screen.getByRole("button", {
            name: "Tìm kiếm bãi đỗ bằng giọng nói",
        });
        await user.click(btn);

        // Simulate voice result
        act(() => {
            if (
                activeInstance &&
                (activeInstance as MockSpeechRecognition).onresult
            ) {
                (activeInstance as MockSpeechRecognition).onresult!({
                    resultIndex: 0,
                    results: [
                        [
                            {
                                transcript: "Landmark 81 Bình Thạnh",
                                confidence: 0.98,
                            },
                        ],
                    ] as any,
                } as any);
            }
        });

        expect(onResult).toHaveBeenCalledWith("Landmark 81 Bình Thạnh");
    });

    it("displays inline helpful instruction guide when microphone permission is denied", async () => {
        const user = userEvent.setup();
        let activeInstance: MockSpeechRecognition | null = null;
        class CustomMock extends MockSpeechRecognition {
            constructor() {
                super();
                activeInstance = this;
            }
        }
        (window as any).SpeechRecognition = CustomMock;
        const onResult = vi.fn();

        render(<VoiceSearchButton onResult={onResult} />);

        const btn = screen.getByRole("button", {
            name: "Tìm kiếm bãi đỗ bằng giọng nói",
        });
        await user.click(btn);

        // Simulate permission denied error
        act(() => {
            if (
                activeInstance &&
                (activeInstance as MockSpeechRecognition).onerror
            ) {
                (activeInstance as MockSpeechRecognition).onerror!({
                    error: "not-allowed",
                } as any);
            }
        });

        // Permission guide should be displayed
        expect(
            screen.getByTestId("voice-permission-guide"),
        ).toBeInTheDocument();
        expect(
            screen.getByText("Chưa cấp quyền microphone"),
        ).toBeInTheDocument();
        expect(
            screen.getByText(/Trình duyệt đang chặn truy cập microphone/i),
        ).toBeInTheDocument();
        expect(screen.getByText(/Chuyển quyền/i)).toBeInTheDocument();

        // Close button works
        const closeBtn = screen.getByRole("button", {
            name: "Đóng hướng dẫn cấp quyền",
        });
        await user.click(closeBtn);
        expect(
            screen.queryByTestId("voice-permission-guide"),
        ).not.toBeInTheDocument();
    });
});
