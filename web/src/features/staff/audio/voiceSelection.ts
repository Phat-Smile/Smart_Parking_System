/**
 * Chọn giọng đọc tiếng Việt cho trợ lý giọng nói cổng bãi.
 * Hàm thuần, không phụ thuộc trình duyệt, để kiểm thử được.
 */

export interface VoiceLike {
    name: string;
    lang: string;
    voiceURI: string;
    localService?: boolean;
}

function normalizeLang(lang: string): string {
    return lang.trim().toLowerCase().replace("_", "-");
}

export function isVietnameseVoice(voice: VoiceLike): boolean {
    const lang = normalizeLang(voice.lang);
    if (lang === "vi" || lang.startsWith("vi-")) return true;
    return /vietnam|tiếng việt|tieng viet/i.test(voice.name);
}

/** Điểm càng cao càng nên chọn: giọng thần kinh/trực tuyến đọc tự nhiên hơn giọng cài sẵn. */
export function scoreVietnameseVoice(voice: VoiceLike): number {
    let score = 0;
    if (normalizeLang(voice.lang) === "vi-vn") score += 10;
    if (/natural|neural|online/i.test(voice.name)) score += 8;
    if (/hoaimy|hoài my|namminh|nam minh/i.test(voice.name)) score += 6;
    if (/google/i.test(voice.name)) score += 4;
    if (/linh/i.test(voice.name)) score += 3;
    if (voice.localService) score += 1;
    return score;
}

export interface VoicePick<T extends VoiceLike> {
    /** Giọng được chọn; null khi máy không có giọng tiếng Việt và người dùng chưa tự chọn. */
    voice: T | null;
    /** Danh sách cho ô chọn: giọng tiếng Việt, hoặc toàn bộ giọng nếu máy không có giọng Việt. */
    options: T[];
    hasVietnamese: boolean;
}

export function pickVoice<T extends VoiceLike>(
    voices: readonly T[],
    storedVoiceURI: string | null,
): VoicePick<T> {
    const vietnamese = voices.filter(isVietnameseVoice);

    if (vietnamese.length === 0) {
        // Không ép một giọng tiếng Anh làm mặc định: để trình duyệt tự chọn theo lang vi-VN,
        // chỉ giữ giọng mà người dùng đã chọn tay trước đó.
        const stored = storedVoiceURI
            ? (voices.find((v) => v.voiceURI === storedVoiceURI) ?? null)
            : null;
        return { voice: stored, options: [...voices], hasVietnamese: false };
    }

    const stored = storedVoiceURI
        ? vietnamese.find((v) => v.voiceURI === storedVoiceURI)
        : undefined;
    if (stored) {
        return { voice: stored, options: vietnamese, hasVietnamese: true };
    }

    const best = [...vietnamese].sort(
        (a, b) => scoreVietnameseVoice(b) - scoreVietnameseVoice(a),
    )[0];
    return { voice: best, options: vietnamese, hasVietnamese: true };
}
