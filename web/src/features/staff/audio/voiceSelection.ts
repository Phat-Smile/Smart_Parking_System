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

function normalizeLang(lang?: string | null): string {
    if (!lang) return "";
    return lang.trim().toLowerCase().replace(/_/g, "-");
}

export function isVietnameseVoice(voice?: VoiceLike | null): boolean {
    if (!voice) return false;
    const lang = normalizeLang(voice.lang);
    if (lang === "vi" || lang.startsWith("vi-")) return true;
    const name = voice.name || "";
    return /vietnam|tiếng việt|tieng viet/i.test(name);
}

/** Điểm càng cao càng nên chọn: giọng thần kinh/trực tuyến đọc tự nhiên hơn giọng cài sẵn. */
export function scoreVietnameseVoice(voice?: VoiceLike | null): number {
    if (!voice) return 0;
    let score = 0;
    const lang = normalizeLang(voice.lang);
    if (lang === "vi-vn") score += 10;
    const name = voice.name || "";
    if (/natural|neural|online/i.test(name)) score += 8;
    if (/hoaimy|hoài my|namminh|nam minh/i.test(name)) score += 6;
    if (/google/i.test(name)) score += 4;
    if (/linh/i.test(name)) score += 3;
    if (voice.localService) score += 1;
    return score;
}

/** Kiểm tra nhanh danh sách có giọng tiếng Việt hay không */
export function hasVietnameseVoice(voices?: readonly VoiceLike[] | null): boolean {
    if (!voices || !Array.isArray(voices)) return false;
    return voices.some(isVietnameseVoice);
}

/** Lấy giọng tiếng Việt tối ưu nhất từ danh sách */
export function getBestVietnameseVoice<T extends VoiceLike>(
    voices?: readonly T[] | null,
): T | null {
    if (!voices || !Array.isArray(voices) || voices.length === 0) return null;
    const vietnamese = voices.filter(isVietnameseVoice);
    if (vietnamese.length === 0) return null;
    return [...vietnamese].sort(
        (a, b) => scoreVietnameseVoice(b) - scoreVietnameseVoice(a),
    )[0] ?? null;
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
    if (!voices || !Array.isArray(voices) || voices.length === 0) {
        return { voice: null, options: [], hasVietnamese: false };
    }

    const vietnamese = voices.filter(isVietnameseVoice);

    if (vietnamese.length === 0) {
        // Không ép một giọng tiếng Anh làm mặc định: để trình duyệt tự chọn theo lang vi-VN,
        // chỉ giữ giọng mà người dùng đã chọn tay trước đó.
        const stored = storedVoiceURI
            ? (voices.find((v) => v && v.voiceURI === storedVoiceURI) ?? null)
            : null;
        return { voice: stored, options: [...voices], hasVietnamese: false };
    }

    const stored = storedVoiceURI
        ? vietnamese.find((v) => v && v.voiceURI === storedVoiceURI)
        : undefined;
    if (stored) {
        return { voice: stored, options: vietnamese, hasVietnamese: true };
    }

    const best = getBestVietnameseVoice(vietnamese);
    return { voice: best, options: vietnamese, hasVietnamese: true };
}
