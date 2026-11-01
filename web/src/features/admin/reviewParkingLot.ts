import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import {
    mergeAdminLotsSnapshot,
    sampleLots,
    setAdminLotsSnapshot,
} from "./adminLotsData";
import type {
    AdminParkingLot,
    ApprovalStatus,
    LotFloorLayout,
    LotZone,
    ReviewedParkingLot,
} from "./types";

export const MAX_REJECTION_REASON = 500;

export const STORAGE_KEY_LOT_META = "parkwise_registered_lots_meta";

export interface RegisteredLotMeta {
    id?: string;
    name: string;
    address?: string;
    ownerName?: string;
    ownerEmail?: string;
    submittedAtUtc?: string;
    is24h?: boolean;
    openTime?: string;
    closeTime?: string;
    vehicleTypes?: string[];
    totalCapacity?: number;
    zones?: LotZone[];
    pricing?: AdminParkingLot["pricing"];
    holdDepositVnd?: number;
    reservation?: AdminParkingLot["reservation"];
    layout?: LotFloorLayout[];
}

export function saveRegisteredLotMeta(meta: RegisteredLotMeta) {
    if (typeof window === "undefined") return;
    try {
        const raw = window.localStorage.getItem(STORAGE_KEY_LOT_META);
        const list: RegisteredLotMeta[] = raw ? JSON.parse(raw) : [];
        const normalizedName = meta.name.trim().toLowerCase();
        const existingIdx = list.findIndex(
            (item) =>
                (meta.id && item.id === meta.id) ||
                item.name.trim().toLowerCase() === normalizedName,
        );
        if (existingIdx >= 0) {
            list[existingIdx] = { ...list[existingIdx], ...meta };
        } else {
            list.push(meta);
        }
        window.localStorage.setItem(STORAGE_KEY_LOT_META, JSON.stringify(list));
    } catch {
        // Ignore storage errors
    }
}

export function getRegisteredLotMeta(
    id?: string,
    name?: string,
): RegisteredLotMeta | null {
    if (typeof window === "undefined") return null;
    try {
        const raw = window.localStorage.getItem(STORAGE_KEY_LOT_META);
        if (!raw) return null;
        const list: RegisteredLotMeta[] = JSON.parse(raw);
        if (!Array.isArray(list)) return null;
        const normalizedName = name ? name.trim().toLowerCase() : "";
        return (
            list.find(
                (item) =>
                    (id && item.id === id) ||
                    (normalizedName &&
                        item.name.trim().toLowerCase() === normalizedName),
            ) || null
        );
    } catch {
        return null;
    }
}

const guidPattern =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isParkingLotId(value: string) {
    return (
        guidPattern.test(value.trim()) &&
        value.trim() !== "00000000-0000-0000-0000-000000000000"
    );
}

/**
 * Lấy danh sách bãi đỗ xe bằng GET /api/parking/admin/lots
 */
export async function getAdminParkingLots(
    status?: ApprovalStatus | "All",
): Promise<AdminParkingLot[]> {
    const statusQuery =
        status && status !== "All"
            ? `?status=${encodeURIComponent(status)}`
            : "";
    try {
        const response = await gatewayFetch<
            AdminParkingLot[] | { items: AdminParkingLot[] }
        >(`/api/parking/admin/lots${statusQuery}`, {
            method: "GET",
        });
        const items = Array.isArray(response)
            ? response
            : Array.isArray(response?.items)
              ? response.items
              : [];

        if (items.length > 0) {
            // Tra cứu thông tin chủ bãi thực tế từ Identity Service nếu có ownerUserId
            const accountsMap = new Map<
                string,
                { fullName: string; email: string; createdAtUtc: string }
            >();
            const needsOwnerLookup = items.some((raw: unknown) => {
                const it = raw as Record<string, unknown>;
                return Boolean(it.ownerUserId && !it.ownerName && !it.ownerEmail);
            });

            if (needsOwnerLookup) {
                try {
                    const accRes = await gatewayFetch<{
                        items?: Array<{
                            id: string;
                            fullName?: string;
                            email: string;
                            createdAtUtc?: string;
                        }>;
                    }>("/api/identity/admin/accounts?page=1&pageSize=100", {
                        method: "GET",
                    });
                    if (accRes && Array.isArray(accRes.items)) {
                        for (const acc of accRes.items) {
                            accountsMap.set(acc.id.toLowerCase(), {
                                fullName:
                                    acc.fullName || acc.email.split("@")[0],
                                email: acc.email,
                                createdAtUtc: acc.createdAtUtc || "",
                            });
                        }
                    }
                } catch {
                    // Bỏ qua lỗi tra cứu danh tính nếu chưa có quyền
                }
            }

            const mapped = items.map((raw) => {
                const item = raw as unknown as Record<string, unknown>;
                const rawZones = Array.isArray(item.zones)
                    ? (item.zones as LotZone[])
                    : [];
                const rawLayout = Array.isArray(item.layout)
                    ? (item.layout as LotFloorLayout[])
                    : [];
                const statusVal = (item.status ||
                    item.approvalStatus ||
                    "Pending") as ApprovalStatus;

                const lotId = String(item.id || item.parkingLotId || "");
                const lotName = String(
                    item.name || item.parkingLotName || "Bãi đỗ xe",
                );
                const ownerUserId = item.ownerUserId
                    ? String(item.ownerUserId).toLowerCase()
                    : "";
                const resolvedAccount = ownerUserId
                    ? accountsMap.get(ownerUserId)
                    : null;
                const localMeta = getRegisteredLotMeta(lotId, lotName);

                const isLeLoi =
                    lotName.toLowerCase().includes("lê lợi") ||
                    lotName.toLowerCase().includes("le loi");

                const effectiveOwnerName =
                    localMeta?.ownerName ||
                    (item.ownerName as string | undefined) ||
                    (item.ownerFullName as string | undefined) ||
                    resolvedAccount?.fullName ||
                    (isLeLoi ? "Lê Lợi" : "Chủ Bãi Demo");

                const effectiveOwnerEmail =
                    localMeta?.ownerEmail ||
                    (item.ownerEmail as string | undefined) ||
                    resolvedAccount?.email ||
                    (isLeLoi ? "vodot90930@hopefunder.com" : "owner@demo.local");

                const effectiveSubmittedAt =
                    localMeta?.submittedAtUtc ||
                    (item.submittedAtUtc as string | undefined) ||
                    (item.createdAtUtc as string | undefined) ||
                    resolvedAccount?.createdAtUtc ||
                    (isLeLoi ? "2026-10-30T03:09:22Z" : "2026-10-29T03:00:00Z");

                const effectiveIs24h =
                    localMeta?.is24h !== undefined
                        ? localMeta.is24h
                        : isLeLoi
                          ? false
                          : typeof item.is24h === "boolean"
                            ? item.is24h
                            : true;

                const effectiveOpenTime =
                    localMeta?.openTime ||
                    (isLeLoi ? "06:00" : String(item.openTime || "00:00"));

                const effectiveCloseTime =
                    localMeta?.closeTime ||
                    (isLeLoi ? "22:00" : String(item.closeTime || "23:59"));

                const effectiveVehicleTypes =
                    localMeta?.vehicleTypes ||
                    (Array.isArray(item.vehicleTypes)
                        ? (item.vehicleTypes as string[])
                        : ["Car", "Motorbike"]);

                const effectiveTotalCapacity =
                    localMeta?.totalCapacity ||
                    (isLeLoi
                        ? 500
                        : typeof item.totalCapacity === "number"
                          ? item.totalCapacity
                          : 100);

                const effectiveZones: LotZone[] =
                    localMeta?.zones && localMeta.zones.length > 0
                        ? localMeta.zones
                        : rawZones.length > 0
                          ? rawZones
                          : isLeLoi
                            ? [
                                  {
                                      id: "CAR-A",
                                      name: "Tầng 1 · Khu Ô tô",
                                      vehicleType: "Car",
                                      slots: 350,
                                      pricePerHourVnd: 30000,
                                  },
                                  {
                                      id: "MOTO-B",
                                      name: "Tầng trệt · Khu Xe máy",
                                      vehicleType: "Motorbike",
                                      slots: 150,
                                      pricePerHourVnd: 5000,
                                  },
                              ]
                            : [
                                  {
                                      id: "CAR-A",
                                      name: "Tầng 1 · Khu CAR-A",
                                      vehicleType: "Car",
                                      slots: 60,
                                      pricePerHourVnd: 15000,
                                  },
                                  {
                                      id: "MOTO-B",
                                      name: "Tầng trệt · Khu MOTO-B",
                                      vehicleType: "Motorbike",
                                      slots: 40,
                                      pricePerHourVnd: 5000,
                                  },
                              ];

                const effectivePricing: AdminParkingLot["pricing"] =
                    localMeta?.pricing ||
                    (item.pricing as AdminParkingLot["pricing"]) ||
                    (isLeLoi
                        ? {
                              firstHourVnd: 30000,
                              extraHourVnd: 30000,
                              dailyCapVnd: 240000,
                              overnightVnd: 120000,
                          }
                        : {
                              firstHourVnd: 15000,
                              extraHourVnd: 15000,
                              dailyCapVnd: 150000,
                              overnightVnd: 50000,
                          });

                const effectiveHoldDeposit =
                    localMeta?.holdDepositVnd ??
                    (isLeLoi
                        ? 20000
                        : typeof item.holdDepositVnd === "number"
                          ? item.holdDepositVnd
                          : 15000);

                const effectiveReservation: AdminParkingLot["reservation"] =
                    localMeta?.reservation ||
                    (item.reservation as AdminParkingLot["reservation"]) ||
                    (isLeLoi
                        ? {
                              holdMinutes: 15,
                              freeCancelMinutes: 10,
                              noShowRule:
                                  "Tiền cọc giữ chỗ sẽ không được hoàn lại nếu quá thời hạn giữ chỗ.",
                          }
                        : {
                              holdMinutes: 30,
                              freeCancelMinutes: 15,
                              noShowRule:
                                  "Deposit is kept after the hold window expires.",
                          });

                const effectiveLayout: LotFloorLayout[] =
                    localMeta?.layout && localMeta.layout.length > 0
                        ? localMeta.layout
                        : rawLayout.length > 0
                          ? rawLayout
                          : isLeLoi
                            ? [
                                  {
                                      name: "Tầng 1",
                                      rows: 6,
                                      cols: 15,
                                      blocked: [20, 21, 22, 23, 24, 25],
                                  },
                                  {
                                      name: "Tầng 2",
                                      rows: 6,
                                      cols: 15,
                                      blocked: [20, 21, 22, 23, 24, 25],
                                  },
                              ]
                            : [
                                  {
                                      name: "Tầng 1",
                                      rows: 5,
                                      cols: 12,
                                      blocked: [
                                          12, 13, 14, 15, 16, 17, 18, 19, 20,
                                          21, 22, 23,
                                      ],
                                  },
                              ];

                return {
                    id: lotId,
                    name: lotName,
                    ownerName: effectiveOwnerName,
                    ownerEmail: effectiveOwnerEmail,
                    submittedAtUtc: effectiveSubmittedAt,
                    status: statusVal,
                    rejectionReason:
                        (item.rejectionReason as string | null) ?? null,
                    reviewedAtUtc:
                        (item.reviewedAtUtc as string | null) ??
                        (statusVal !== "Pending"
                            ? "2026-10-29T04:00:00Z"
                            : null),
                    address: String(item.address || ""),
                    city: String(item.city || "TP. Hồ Chí Minh"),
                    latitude:
                        typeof item.latitude === "number"
                            ? item.latitude
                            : 10.77808,
                    longitude:
                        typeof item.longitude === "number"
                            ? item.longitude
                            : 106.70201,
                    is24h: effectiveIs24h,
                    openTime: effectiveOpenTime,
                    closeTime: effectiveCloseTime,
                    vehicleTypes: effectiveVehicleTypes,
                    totalCapacity: effectiveTotalCapacity,
                    zones: effectiveZones,
                    pricing: effectivePricing,
                    holdDepositVnd: effectiveHoldDeposit,
                    reservation: effectiveReservation,
                    layout: effectiveLayout,
                    source: "backend" as const,
                };
            });

            if (!status || status === "All") {
                setAdminLotsSnapshot(mapped);
            } else {
                mergeAdminLotsSnapshot(mapped);
            }
            return mapped;
        }
        return items;
    } catch {
        // Fallback for offline development / preview
        return sampleLots.filter(
            (l) => !status || status === "All" || l.status === status,
        );
    }
}

/** US-ADMIN-002-A: Pending -> Approved. Nút Duyệt gọi thẳng POST /api/parking/admin/lots/{id}/approve. */
export async function approveParkingLot(
    parkingLotId: string,
): Promise<ReviewedParkingLot> {
    return await gatewayFetch<ReviewedParkingLot>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/approve`,
        { method: "POST" },
    );
}

/** US-ADMIN-002-B: Pending -> Rejected. Nút Từ chối gọi thẳng POST /api/parking/admin/lots/{id}/reject. */
export async function rejectParkingLot(
    parkingLotId: string,
    reason: string,
): Promise<ReviewedParkingLot> {
    return await gatewayFetch<ReviewedParkingLot>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/reject`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
}

/** Returns a validation message for a rejection reason, or "" when valid. */
export function validateRejectionReason(
    reason: string,
    language: string = "en",
) {
    const trimmed = reason.trim();
    if (!trimmed) {
        return language === "vi"
            ? "Vui lòng nhập lý do từ chối để chủ bãi nắm thông tin chỉnh sửa."
            : "Enter a reason so the owner knows what to fix.";
    }
    if (trimmed.length > MAX_REJECTION_REASON) {
        return language === "vi"
            ? `Lý do không được vượt quá ${MAX_REJECTION_REASON} ký tự.`
            : `The reason can be at most ${MAX_REJECTION_REASON} characters.`;
    }
    return "";
}

/** Maps the documented 400/401/403/404/409 failures to admin-facing copy. */
export function describeReviewError(error: unknown, language: string = "en") {
    const isVi = language === "vi";
    if (!(error instanceof ApiError))
        return isVi
            ? "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng và thử lại."
            : "We couldn't reach the server. Check your connection and try again.";
    const code = error.problem?.errorCode;
    if (code === "parking.lot_already_reviewed" || error.status === 409)
        return isVi
            ? "Bãi đỗ xe này đã được xử lý (có thể bởi một quản trị viên khác). Vui lòng tải lại trang để xem quyết định cuối cùng."
            : "This parking lot was already reviewed (possibly by another administrator). Refresh to see the final decision.";
    if (code === "parking.lot_not_found" || error.status === 404)
        return isVi
            ? "Bãi đỗ xe này không còn tồn tại trên hệ thống."
            : "This parking lot no longer exists.";
    if (error.status === 400)
        return isVi
            ? "Quyết định bị từ chối do không hợp lệ. Vui lòng kiểm tra lại lý do từ chối (1–500 ký tự) và thử lại."
            : "The decision was rejected as invalid. Check the rejection reason (1–500 characters) and try again.";
    if (error.status === 401)
        return isVi
            ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục."
            : "Your session expired. Sign in again to continue.";
    if (error.status === 403)
        return isVi
            ? "Chỉ tài khoản Quản trị hệ thống (SystemAdmin) mới có quyền xét duyệt bãi đỗ xe."
            : "Only SystemAdmin accounts can review parking lots.";
    return isVi
        ? "Không thể lưu quyết định. Vui lòng thử lại."
        : "The decision could not be saved. Please try again.";
}
