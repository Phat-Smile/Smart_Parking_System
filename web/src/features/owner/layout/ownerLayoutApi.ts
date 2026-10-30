import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";

export interface LayoutPoint {
    x: number;
    y: number;
}

export interface LayoutBounds {
    x: number;
    y: number;
    width: number;
    height: number;
}

export type SlotOverrideState = "Maintenance" | "Unavailable" | null;

export interface LayoutSlotDto {
    id: string; // GUID
    slotCode: string;
    overrideState?: SlotOverrideState;
    effectiveStatus?: string;
    position: LayoutPoint;
}

export interface LayoutZoneDto {
    id: string; // GUID
    code: string;
    vehicleGroup: "Car" | "Motorbike";
    capacity: number;
    currentVehicleCount?: number;
    hasSlotSensor: boolean;
    bounds?: LayoutBounds;
    polygon?: LayoutPoint[];
    slots: LayoutSlotDto[];
}

export interface LayoutFloorDto {
    id: string; // GUID
    name: string;
    zones: LayoutZoneDto[];
}

export interface ParkingLayoutDocumentDto {
    lotId?: string;
    version: number;
    floors: LayoutFloorDto[];
}

export interface SaveParkingLayoutRequest {
    expectedVersion: number;
    floors: LayoutFloorDto[];
}

/**
 * Generate a random UUID v4 string.
 */
export function generateLayoutGuid(): string {
    if (
        typeof crypto !== "undefined" &&
        typeof crypto.randomUUID === "function"
    ) {
        return crypto.randomUUID();
    }
    // Fallback for test environments without crypto.randomUUID
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, (c) => {
        const r = (Math.random() * 16) | 0;
        const v = c === "x" ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

/**
 * Creates a new floor with unique GUID and default zones.
 */
export function createDefaultFloor(floorIndex: number): LayoutFloorDto {
    return {
        id: generateLayoutGuid(),
        name: `Tầng ${floorIndex}`,
        zones: [
            createDefaultZone("Car", "A", 10),
            createDefaultZone("Motorbike", "M1", 20),
        ],
    };
}

/**
 * Creates a new zone with unique GUID and appropriate defaults based on vehicleGroup.
 */
export function createDefaultZone(
    type: "Car" | "Motorbike",
    code: string,
    capacity: number,
): LayoutZoneDto {
    const isCar = type === "Car";
    return {
        id: generateLayoutGuid(),
        code: code.trim(),
        vehicleGroup: type,
        capacity: Math.max(0, capacity),
        hasSlotSensor: isCar,
        slots: [],
        bounds: isCar ? undefined : { x: 0, y: 0, width: 10, height: 6 },
    };
}

/**
 * Creates a single car slot with unique GUID and coordinates.
 */
export function createDefaultSlot(
    zoneCode: string,
    slotIndex: number,
    position: LayoutPoint,
): LayoutSlotDto {
    const paddedIndex = String(slotIndex).padStart(2, "0");
    return {
        id: generateLayoutGuid(),
        slotCode: `${zoneCode}-${paddedIndex}`,
        overrideState: null,
        position: { x: position.x, y: position.y },
    };
}

/**
 * Validates layout input against backend `ParkingLayoutInputValidator` rules before submitting.
 */
export function validateLayoutInput(floors: LayoutFloorDto[]): {
    valid: boolean;
    errors: string[];
} {
    const errors: string[] = [];

    if (!floors || floors.length === 0) {
        errors.push("Bãi đỗ xe phải có ít nhất một tầng.");
        return { valid: false, errors };
    }

    const floorIds = new Set<string>();
    const floorNames = new Set<string>();
    const zoneIds = new Set<string>();
    const lotSlotCodes = new Set<string>();
    const slotIds = new Set<string>();

    for (const floor of floors) {
        if (!floor.id || floorIds.has(floor.id)) {
            errors.push(`Mỗi tầng cần có ID hợp lệ và duy nhất.`);
        } else {
            floorIds.add(floor.id);
        }

        const trimmedFloorName = floor.name ? floor.name.trim() : "";
        if (!trimmedFloorName || trimmedFloorName.length > 100) {
            errors.push("Tên tầng không được để trống và tối đa 100 ký tự.");
        } else if (floorNames.has(trimmedFloorName.toLowerCase())) {
            errors.push(`Tên tầng '${trimmedFloorName}' bị trùng lặp.`);
        } else {
            floorNames.add(trimmedFloorName.toLowerCase());
        }

        if (!floor.zones || floor.zones.length === 0) {
            errors.push(`Tầng '${floor.name}' cần có ít nhất một khu vực.`);
            continue;
        }

        const floorZoneCodes = new Set<string>();

        for (const zone of floor.zones) {
            if (!zone.id || zoneIds.has(zone.id)) {
                errors.push(`Mỗi khu vực cần có ID hợp lệ và duy nhất.`);
            } else {
                zoneIds.add(zone.id);
            }

            const trimmedZoneCode = zone.code ? zone.code.trim() : "";
            if (!trimmedZoneCode || trimmedZoneCode.length > 50) {
                errors.push(
                    `Mã khu vực ở tầng '${floor.name}' không được để trống (tối đa 50 ký tự).`,
                );
            } else if (floorZoneCodes.has(trimmedZoneCode.toLowerCase())) {
                errors.push(
                    `Mã khu vực '${trimmedZoneCode}' bị trùng lặp trong tầng '${floor.name}'.`,
                );
            } else {
                floorZoneCodes.add(trimmedZoneCode.toLowerCase());
            }

            if (zone.capacity < 0) {
                errors.push(
                    `Sức chứa của khu vực '${zone.code}' phải lớn hơn hoặc bằng 0.`,
                );
            }

            const isCar = zone.vehicleGroup === "Car";
            const slots = zone.slots || [];

            if (!isCar) {
                if (slots.length > 0) {
                    errors.push(
                        `Khu xe máy '${zone.code}' không được chứa từng ô riêng.`,
                    );
                }
                if (
                    !zone.bounds &&
                    (!zone.polygon || zone.polygon.length < 3)
                ) {
                    errors.push(
                        `Khu xe máy '${zone.code}' cần có hình khối xác định (bounds hoặc polygon).`,
                    );
                } else if (
                    zone.bounds &&
                    (zone.bounds.width <= 0 || zone.bounds.height <= 0)
                ) {
                    errors.push(
                        `Chiều rộng và chiều dài của khu xe máy '${zone.code}' phải lớn hơn 0.`,
                    );
                }
            } else {
                if (slots.length > zone.capacity) {
                    errors.push(
                        `Số lượng ô đỗ (${slots.length}) vượt quá sức chứa (${zone.capacity}) của khu ô tô '${zone.code}'.`,
                    );
                }

                const zonePositions = new Set<string>();

                for (const slot of slots) {
                    if (!slot.id || slotIds.has(slot.id)) {
                        errors.push(
                            `Ô đỗ '${slot.slotCode}' cần có ID hợp lệ và duy nhất.`,
                        );
                    } else {
                        slotIds.add(slot.id);
                    }

                    const trimmedSlotCode = slot.slotCode
                        ? slot.slotCode.trim()
                        : "";
                    if (!trimmedSlotCode || trimmedSlotCode.length > 20) {
                        errors.push(
                            `Mã ô đỗ không được để trống và tối đa 20 ký tự.`,
                        );
                    } else if (
                        lotSlotCodes.has(trimmedSlotCode.toLowerCase())
                    ) {
                        errors.push(
                            `Mã ô đỗ '${trimmedSlotCode}' bị trùng lặp trong bãi.`,
                        );
                    } else {
                        lotSlotCodes.add(trimmedSlotCode.toLowerCase());
                    }

                    if (!slot.position) {
                        errors.push(
                            `Ô đỗ '${slot.slotCode}' thiếu tọa độ vị trí.`,
                        );
                    } else {
                        const posKey = `${slot.position.x},${slot.position.y}`;
                        if (zonePositions.has(posKey)) {
                            errors.push(
                                `Ô đỗ '${slot.slotCode}' trùng vị trí tọa độ (${posKey}) trong khu '${zone.code}'.`,
                            );
                        } else {
                            zonePositions.add(posKey);
                        }
                    }
                }
            }
        }
    }

    return {
        valid: errors.length === 0,
        errors,
    };
}

/**
 * Reads layout from backend: GET /api/parking/owner/lots/{lotId}/layout
 */
export async function getOwnerParkingLayout(
    lotId: string,
): Promise<ParkingLayoutDocumentDto> {
    return gatewayFetch<ParkingLayoutDocumentDto>(
        `/api/parking/owner/lots/${encodeURIComponent(lotId)}/layout`,
        { method: "GET" },
    );
}

/**
 * Saves layout to backend: PUT /api/parking/owner/lots/{lotId}/layout
 */
export async function saveOwnerParkingLayout(
    lotId: string,
    req: SaveParkingLayoutRequest,
): Promise<ParkingLayoutDocumentDto> {
    return gatewayFetch<ParkingLayoutDocumentDto>(
        `/api/parking/owner/lots/${encodeURIComponent(lotId)}/layout`,
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(req),
        },
    );
}

/**
 * Formats API errors for display in the layout editor.
 */
export function describeParkingLayoutError(error: unknown): string {
    if (error instanceof ApiError) {
        const code = error.problem?.errorCode;
        const detail = error.problem?.detail || error.message;

        switch (code) {
            case "parking.layout.stale_version":
                return "Sơ đồ đã được cập nhật từ một phiên làm việc khác. Vui lòng tải lại phiên bản mới nhất trước khi lưu.";
            case "parking.layout.conflict":
                return "Xung đột sơ đồ: Không thể xóa tầng, khu, chỗ hoặc thay đổi cấu trúc đã lưu trước đó.";
            case "parking.layout.invalid":
                return `Dữ liệu sơ đồ không hợp lệ: ${detail}`;
            case "parking.layout.lot_not_found":
                return "Không tìm thấy bãi đỗ xe trên hệ thống.";
            case "parking.layout.owner_required":
                return "Bạn không có quyền quản lý sơ đồ bãi đỗ xe này.";
            case "parking.layout.duplicate_id":
                return "Mã định danh trong sơ đồ bị trùng lặp với dữ liệu đã có.";
            default:
                return detail || "Không thể lưu sơ đồ mặt bằng.";
        }
    }

    if (error instanceof TypeError) {
        return "Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại kết nối mạng.";
    }

    if (error instanceof Error) {
        return error.message;
    }

    return "Đã xảy ra lỗi không xác định khi xử lý sơ đồ.";
}
