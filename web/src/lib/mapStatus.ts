/**
 * mapStatus – lớp dữ liệu chuẩn hóa trạng thái ô đỗ xe.
 *
 * Để BẬT hoặc TẮT việc gộp "Cần xác minh" → "Bảo trì":
 *   đặt MERGE_VERIFY = true (gộp) hoặc false (giữ riêng).
 *
 * Áp dụng hàm mapStatus() TẠI ĐÚNG MỘT CHỖ là seed dữ liệu
 * trong useOwnerStore (createInitialSpots), để engine 3D và
 * toàn bộ UI đều nhận s.s đã được chuẩn hóa.
 */

import type { SlotStatusKey } from "../types/owner";
import { K } from "../types/owner";

/** Bật/tắt gộp trạng thái "Cần xác minh" → "Bảo trì" */
export const MERGE_VERIFY = true;

/**
 * Chuẩn hóa trạng thái ô.
 * Nếu MERGE_VERIFY bật và s === 'verify' → trả 'maintenance'.
 * Tất cả trường hợp khác giữ nguyên.
 */
export function mapStatus(s: SlotStatusKey): SlotStatusKey {
    if (MERGE_VERIFY && s === "verify") return "maintenance";
    return s;
}

/**
 * Danh sách trạng thái hiển thị ra UI (chip, legend, tab thống kê).
 * Bỏ 'verify' khỏi danh sách khi MERGE_VERIFY bật.
 */
export const K_VISIBLE: SlotStatusKey[] = MERGE_VERIFY
    ? K.filter((k) => k !== "verify")
    : K;
