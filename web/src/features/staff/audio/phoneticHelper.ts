/**
 * Bộ chuẩn hóa phát âm tiếng Việt cho Cổng bãi Thông minh (ParkWise Gate TTS)
 * Đảm bảo trình duyệt đọc biển số xe, mã ô đỗ tự nhiên, rõ ràng, không nuốt chữ.
 */

const DIGIT_WORDS: Record<string, string> = {
    "0": "không",
    "1": "một",
    "2": "hai",
    "3": "ba",
    "4": "bốn",
    "5": "năm",
    "6": "sáu",
    "7": "bảy",
    "8": "tám",
    "9": "chín",
};

const LETTER_WORDS: Record<string, string> = {
    A: "A",
    B: "Bê",
    C: "Xê",
    D: "Đê",
    Đ: "Đê",
    E: "E",
    F: "Ép",
    G: "Gờ",
    H: "Hát",
    K: "Ca",
    L: "Lờ",
    M: "Em mờ",
    N: "En nờ",
    P: "Pê",
    Q: "Quy",
    R: "Rờ",
    S: "Sờ",
    T: "Tê",
    U: "U",
    V: "Vê",
    X: "Ích",
    Y: "I dài",
    Z: "Dét",
};

/**
 * Chuẩn hóa biển số xe sang chuỗi phát âm tách âm tiết dễ nghe.
 * Ví dụ: "51F-123.45" -> "5 1 Ép, 1 2 3, 4 5"
 * Trình duyệt TTS khi gặp các số cách nhau bởi khoảng trắng hoặc dấu phẩy
 * sẽ đọc từng số một cách tự nhiên thay vì đọc thành "năm mươi mốt F trừ một trăm hai mươi ba...".
 */
export function formatPlateForSpeech(plate: string): string {
    if (!plate || !plate.trim()) return "";

    const cleaned = plate.trim().toUpperCase();

    // Tách thành 2 phần: mã tỉnh + serie và dãy số đuôi (dựa trên dấu '-' hoặc khoảng trắng)
    const parts = cleaned.split(/[-_\s]+/);

    if (parts.length >= 2) {
        const head = formatPlateSegment(parts[0]);
        const tail = formatTailSegment(parts.slice(1).join(""));
        return `${head}, ${tail}`;
    }

    // Nếu không có dấu gạch ngang, format từng ký tự có ngắt nhịp
    return formatPlateSegment(cleaned);
}

function formatPlateSegment(segment: string): string {
    const chars = segment.split("");
    const words: string[] = [];

    for (const ch of chars) {
        if (DIGIT_WORDS[ch]) {
            words.push(ch); // giữ nguyên chữ số để TTS đọc đúng ngữ điệu số
        } else if (LETTER_WORDS[ch]) {
            words.push(LETTER_WORDS[ch]);
        } else if (ch !== "." && ch !== "-") {
            words.push(ch);
        }
    }
    return words.join(" ");
}

function formatTailSegment(segment: string): string {
    // Nếu có dấu chấm như 123.45 -> tách làm 2 cụm "1 2 3" và "4 5"
    if (segment.includes(".")) {
        const sub = segment.split(".");
        const p1 = sub[0]
            .split("")
            .map((c) => DIGIT_WORDS[c] || c)
            .join(" ");
        const p2 = sub[1]
            .split("")
            .map((c) => DIGIT_WORDS[c] || c)
            .join(" ");
        return `${p1}, ${p2}`;
    }

    // Nếu là chuỗi liền 5 số: 12345 -> "1 2 3, 4 5"
    if (segment.length === 5) {
        const p1 = segment.slice(0, 3).split("").join(" ");
        const p2 = segment.slice(3).split("").join(" ");
        return `${p1}, ${p2}`;
    }

    return segment.split("").join(" ");
}

/**
 * Chuẩn hóa mã ô đỗ / khu vực đỗ sang lời đọc thân thiện.
 * Ví dụ: "A-01" -> "ô A không một", "B-12" -> "ô B mười hai", "CAR-A" -> "khu vực ô tô A".
 */
export function formatSlotForSpeech(slotCode?: string): string {
    if (!slotCode || !slotCode.trim()) {
        return "khu vực đã được hướng dẫn";
    }

    const code = slotCode.trim().toUpperCase();

    if (code.startsWith("CAR-")) {
        const zone = code.replace("CAR-", "");
        return `khu vực ô tô ${zone}`;
    }

    if (code.startsWith("MOTO-") || code.startsWith("BIKE-")) {
        const zone = code.replace(/^(MOTO-|BIKE-)/, "");
        return `khu vực xe máy ${zone}`;
    }

    // Định dạng ô cụ thể như A-01, B-12
    if (/^[A-Z]-\d+$/.test(code)) {
        const [row, num] = code.split("-");
        const rowWord = LETTER_WORDS[row] || row;
        if (num.startsWith("0")) {
            const digitSpelled = num
                .split("")
                .map((d) => DIGIT_WORDS[d] || d)
                .join(" ");
            return `ô ${rowWord} ${digitSpelled}`;
        }
        return `ô ${rowWord} ${num}`;
    }

    return `ô ${code}`;
}

/**
 * Sinh câu thông báo khi xe vào bãi (Check-in)
 * Chuẩn: "Xe [Biển số] đã xác thực, mời xe vào ô đỗ [Mã ô]."
 */
export function buildCheckInSpeech(params: {
    plateNumber: string;
    slotCode?: string;
    lotName?: string;
}): string {
    const platePhonetic =
        formatPlateForSpeech(params.plateNumber) || params.plateNumber;
    const rawSlot = params.slotCode?.trim();

    if (!rawSlot) {
        return `Xe ${platePhonetic} đã xác thực, mời xe vào ô đỗ khu vực đã được hướng dẫn.`;
    }

    const slotPhonetic = formatSlotForSpeech(rawSlot);
    const slotTarget = slotPhonetic.startsWith("ô ")
        ? slotPhonetic.slice(2)
        : slotPhonetic;

    return `Xe ${platePhonetic} đã xác thực, mời xe vào ô đỗ ${slotTarget}.`;
}

/**
 * Sinh lời thông báo khi xe xuất bãi thanh toán thành công (Check-out)
 * Chuẩn: "Thanh toán thành công, mời xe xuất bến. ParkWise chúc quý khách thượng lộ bình an!"
 */
export function buildCheckOutSpeech(params?: {
    plateNumber?: string;
    lotName?: string;
    amountPaid?: number;
}): string {
    return "Thanh toán thành công, mời xe xuất bến. ParkWise chúc quý khách thượng lộ bình an!";
}

export const UNPAID_EXIT_WARNING_MESSAGE =
    "Phiên đỗ chưa thanh toán, vui lòng thanh toán trước khi mở rào chắn!";

/**
 * Sinh câu cảnh báo khi cổng ra chưa thanh toán
 */
export function buildUnpaidExitSpeech(): string {
    return UNPAID_EXIT_WARNING_MESSAGE;
}

/**
 * Sinh lời cảnh báo lỗi tại cổng
 */
export function buildWarningSpeech(reason?: string): string {
    if (reason && reason.trim()) {
        const trimmed = reason.trim();
        if (
            trimmed === UNPAID_EXIT_WARNING_MESSAGE ||
            /chưa thanh toán/i.test(trimmed)
        ) {
            return UNPAID_EXIT_WARNING_MESSAGE;
        }
        return `Cảnh báo! ${trimmed}. Vui lòng liên hệ nhân viên trực bãi để được hỗ trợ!`;
    }
    return UNPAID_EXIT_WARNING_MESSAGE;
}
