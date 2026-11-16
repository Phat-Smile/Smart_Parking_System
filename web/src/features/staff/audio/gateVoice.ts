/**
 * Trợ lý Giọng nói Tiếng Việt cho Cổng Barie (ParkWise Gate Voice Assistant)
 * Cung cấp các câu thoại thông báo chuẩn hóa và điều khiển phát âm thanh tại cổng bãi.
 */

import {
    buildCheckInSpeech,
    buildCheckOutSpeech,
    buildWarningSpeech,
    buildUnpaidExitSpeech,
    formatPlateForSpeech,
    formatSlotForSpeech,
    UNPAID_EXIT_WARNING_MESSAGE,
} from "./phoneticHelper";
import {
    GateVoiceService,
    gateVoiceService,
    type GateVoiceSettings,
    type VoiceLogItem,
} from "./GateVoiceService";

export {
    buildCheckInSpeech,
    buildCheckOutSpeech,
    buildWarningSpeech,
    buildUnpaidExitSpeech,
    formatPlateForSpeech,
    formatSlotForSpeech,
    UNPAID_EXIT_WARNING_MESSAGE,
    GateVoiceService,
    gateVoiceService,
    type GateVoiceSettings,
    type VoiceLogItem,
};

/**
 * Kiểm tra xem trình duyệt có hỗ trợ Web Speech API hay không
 */
export function isGateVoiceSupported(): boolean {
    if (typeof window === "undefined") return false;
    try {
        return (
            "speechSynthesis" in window &&
            typeof SpeechSynthesisUtterance !== "undefined" &&
            !!window.speechSynthesis
        );
    } catch {
        return false;
    }
}

/**
 * Tập hợp các mẫu câu thoại chuẩn mực của Cổng bãi ParkWise
 */
export const GATE_VOICE_PROMPTS = {
    checkIn: buildCheckInSpeech,
    checkOut: buildCheckOutSpeech,
    unpaidWarning: buildUnpaidExitSpeech,
    warning: buildWarningSpeech,
} as const;

/**
 * Tiện ích phát âm thanh thông báo xe vào cổng
 */
export function announceGateCheckIn(params: {
    plateNumber: string;
    slotCode?: string;
    lotName?: string;
}) {
    return gateVoiceService.announceCheckIn(params);
}

/**
 * Tiện ích phát âm thanh thông báo xe ra cổng đã thanh toán thành công
 */
export function announceGateCheckOut(params?: {
    plateNumber?: string;
    lotName?: string;
    amountPaid?: number;
}) {
    return gateVoiceService.announceCheckOut(params);
}

/**
 * Tiện ích phát âm thanh cảnh báo lỗi tại cổng
 */
export function announceGateWarning(reason?: string) {
    return gateVoiceService.announceWarning(reason);
}

/**
 * Tiện ích phát âm thanh cảnh báo chưa thanh toán tại cổng ra
 */
export function announceGateUnpaidExit() {
    return gateVoiceService.announceUnpaidExit();
}

/**
 * Dừng toàn bộ âm thanh và giải phóng hàng đợi phát âm
 */
export function stopGateVoice() {
    return gateVoiceService.stop();
}

/**
 * Xóa các câu thoại đang chờ trong hàng đợi
 */
export function clearGateVoiceQueue() {
    return gateVoiceService.clearQueue();
}

/**
 * Lấy số lượng câu thoại đang chờ phát
 */
export function getGateVoiceQueueLength(): number {
    return gateVoiceService.getQueueLength();
}
