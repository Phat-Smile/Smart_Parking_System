/**
 * Smart ParkWise ANPR - Weighted Levenshtein Distance OCR Matcher
 * 
 * Thuật toán so khớp mờ biển số xe thông minh sử dụng ma trận khoảng cách Levenshtein có trọng số
 * (Optical Confusion Matrix), chuyên trị các lỗi nhầm lẫn ký tự quang học phổ biến của camera và OCR:
 * - Số 8 và chữ B (cost = 0.2)
 * - Số 0 và chữ D (cost = 0.2)
 * - Số 0 và chữ O (cost = 0.2)
 * - Số 1 và chữ I (cost = 0.2)
 * - Số 5 và chữ S (cost = 0.2)
 * - Số 2 và chữ Z (cost = 0.2)
 * 
 * Giúp nhận diện chính xác xe ra cổng khi OCR bị sai lệch 1 ký tự hình ảnh,
 * đồng thời hỗ trợ nhân viên đối soát giấy tờ xe tại quầy tránh lỗi 409 ExitNotVerified.
 */

export interface PlateMatchResult {
    bestMatch: string | null;
    score: number;
    distance: number;
    isHighConfidence: boolean;
}

export interface PlateComparisonResult {
    plateA: string;
    plateB: string;
    normalizedA: string;
    normalizedB: string;
    distance: number;
    score: number;
    isExactMatch: boolean;
    isHighConfidence: boolean;
}

/**
 * Cặp ký tự thường xuyên bị đọc nhầm trong OCR biển số xe Việt Nam.
 * Trọng số lỗi quang học (optical confusion cost) = 0.2 thay vì 1.0.
 */
export const OPTICAL_CONFUSION_PAIRS: ReadonlyArray<readonly [string, string]> = [
    ["8", "B"],
    ["0", "D"],
    ["0", "O"],
    ["1", "I"],
    ["5", "S"],
    ["2", "Z"],
    ["D", "O"], // Cặp D-O cũng rất hay gặp do bo góc camera
];

/**
 * Chuẩn hóa biển số xe:
 * Viết hoa, loại bỏ mọi dấu gạch ngang, dấu chấm, khoảng trắng và ký tự đặc biệt.
 * Ví dụ: "29A-128.45" -> "29A12845"
 */
export function normalizePlate(plate: string | null | undefined): string {
    if (!plate) return "";
    return plate.toUpperCase().replace(/[^A-Z0-9]/g, "");
}

/**
 * Tính chi phí thay thế (substitution cost) giữa 2 ký tự:
 * - Ký tự giống nhau: 0.0
 * - Cặp quang học hay nhầm: 0.2
 * - Ký tự khác: 1.0
 */
export function getSubstitutionCost(charA: string, charB: string): number {
    if (charA === charB) {
        return 0.0;
    }

    for (const [c1, c2] of OPTICAL_CONFUSION_PAIRS) {
        if ((charA === c1 && charB === c2) || (charA === c2 && charB === c1)) {
            return 0.2;
        }
    }

    return 1.0;
}

/**
 * Tính khoảng cách Levenshtein có trọng số giữa 2 biển số xe.
 * 
 * - Thao tác Xóa (Deletion): cost = 1.0
 * - Thao tác Chèn (Insertion): cost = 1.0
 * - Thao tác Thay thế (Substitution):
 *     + Giống nhau: 0.0
 *     + Cặp quang học hay nhầm: 0.2
 *     + Ký tự khác: 1.0
 * 
 * @param plateA Biển số thứ nhất
 * @param plateB Biển số thứ hai
 * @returns Khoảng cách có trọng số (làm tròn 3 chữ số thập phân)
 */
export function weightedLevenshteinDistance(
    plateA: string,
    plateB: string,
): number {
    const normA = normalizePlate(plateA);
    const normB = normalizePlate(plateB);

    const m = normA.length;
    const n = normB.length;

    if (m === 0) return n * 1.0;
    if (n === 0) return m * 1.0;

    // Khởi tạo bảng quy hoạch động dp[(m + 1) x (n + 1)]
    const dp: number[][] = Array.from({ length: m + 1 }, () =>
        new Array(n + 1).fill(0),
    );

    for (let i = 0; i <= m; i++) {
        dp[i][0] = i * 1.0;
    }
    for (let j = 0; j <= n; j++) {
        dp[0][j] = j * 1.0;
    }

    for (let i = 1; i <= m; i++) {
        const charA = normA[i - 1];
        for (let j = 1; j <= n; j++) {
            const charB = normB[j - 1];
            const subCost = getSubstitutionCost(charA, charB);

            const delCost = dp[i - 1][j] + 1.0;
            const insCost = dp[i][j - 1] + 1.0;
            const repCost = dp[i - 1][j - 1] + subCost;

            dp[i][j] = Math.min(delCost, insCost, repCost);
        }
    }

    return Math.round(dp[m][n] * 1000) / 1000;
}

/**
 * So sánh 2 biển số và trả về kết quả chi tiết
 */
export function comparePlates(
    plateA: string,
    plateB: string,
): PlateComparisonResult {
    const normA = normalizePlate(plateA);
    const normB = normalizePlate(plateB);

    if (!normA && !normB) {
        return {
            plateA,
            plateB,
            normalizedA: "",
            normalizedB: "",
            distance: 0,
            score: 1.0,
            isExactMatch: true,
            isHighConfidence: true,
        };
    }

    if (!normA || !normB) {
        return {
            plateA,
            plateB,
            normalizedA: normA,
            normalizedB: normB,
            distance: Math.max(normA.length, normB.length),
            score: 0.0,
            isExactMatch: false,
            isHighConfidence: false,
        };
    }

    const distance = weightedLevenshteinDistance(normA, normB);
    const maxLen = Math.max(normA.length, normB.length);
    const rawScore = maxLen > 0 ? Math.max(0, 1.0 - distance / maxLen) : 0;
    const score = Math.round(rawScore * 10000) / 10000;

    const isExactMatch = normA === normB;
    const isHighConfidence = isExactMatch || score >= 0.95 || distance <= 0.2;

    return {
        plateA,
        plateB,
        normalizedA: normA,
        normalizedB: normB,
        distance,
        score,
        isExactMatch,
        isHighConfidence,
    };
}

/**
 * Tìm biển số trùng khớp nhất trong danh sách ứng viên (candidatePlates)
 * so với biển số đọc được từ camera/OCR (scannedPlate).
 * 
 * @param scannedPlate Biển số OCR nhận diện được từ camera
 * @param candidatePlates Danh sách biển số các xe đang có phiên đỗ trong bãi
 * @returns Thông tin biển số khớp nhất kèm độ tin cậy
 */
export function findBestPlateMatch(
    scannedPlate: string,
    candidatePlates: string[],
): PlateMatchResult {
    const normScanned = normalizePlate(scannedPlate);

    if (!normScanned || !candidatePlates || candidatePlates.length === 0) {
        return {
            bestMatch: null,
            score: 0,
            distance: Infinity,
            isHighConfidence: false,
        };
    }

    let bestCandidate: string | null = null;
    let minDistance = Infinity;
    let maxScore = -1;

    for (const candidate of candidatePlates) {
        const normCand = normalizePlate(candidate);
        if (!normCand) continue;

        const dist = weightedLevenshteinDistance(normScanned, normCand);
        const maxLen = Math.max(normScanned.length, normCand.length);
        const rawScore = maxLen > 0 ? Math.max(0, 1.0 - dist / maxLen) : 0;
        const score = Math.round(rawScore * 10000) / 10000;

        if (score > maxScore) {
            maxScore = score;
            minDistance = dist;
            bestCandidate = candidate;
        } else if (score === maxScore && dist < minDistance) {
            minDistance = dist;
            bestCandidate = candidate;
        }
    }

    const isHighConfidence = maxScore >= 0.95 || minDistance <= 0.2;

    return {
        bestMatch: bestCandidate,
        score: maxScore >= 0 ? maxScore : 0,
        distance: minDistance < Infinity ? minDistance : Infinity,
        isHighConfidence,
    };
}
