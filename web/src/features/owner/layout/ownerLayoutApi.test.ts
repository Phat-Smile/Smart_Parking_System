import { describe, expect, it, vi, afterEach } from "vitest";
import {
    describeParkingLayoutError,
    validateLayoutInput,
    getOwnerParkingLayout,
    saveOwnerParkingLayout,
    createDefaultFloor,
    createDefaultZone,
    createDefaultSlot,
} from "./ownerLayoutApi";
import * as client from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";

describe("ownerLayoutApi", () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    describe("validateLayoutInput", () => {
        it("fails when floors list is empty", () => {
            const result = validateLayoutInput([]);
            expect(result.valid).toBe(false);
            expect(result.errors).toContain(
                "Bãi đỗ xe phải có ít nhất một tầng.",
            );
        });

        it("fails when floor name is empty or duplicate", () => {
            const floor1 = createDefaultFloor(1);
            const floor2 = createDefaultFloor(2);
            floor2.name = floor1.name; // Duplicate name

            const result = validateLayoutInput([floor1, floor2]);
            expect(result.valid).toBe(false);
            expect(result.errors.some((e) => e.includes("Tên tầng"))).toBe(
                true,
            );
        });

        it("fails when zone code is duplicate within the same floor", () => {
            const floor = createDefaultFloor(1);
            floor.zones = [
                createDefaultZone("Car", "ZONE-A", 5),
                createDefaultZone("Car", "ZONE-A", 10),
            ];

            const result = validateLayoutInput([floor]);
            expect(result.valid).toBe(false);
            expect(result.errors.some((e) => e.includes("Mã khu"))).toBe(true);
        });

        it("fails when motorbike zone has individual slots", () => {
            const floor = createDefaultFloor(1);
            const motoZone = createDefaultZone("Motorbike", "MOTO-1", 20);
            motoZone.slots = [createDefaultSlot("MOTO-1", 1, { x: 0, y: 0 })];

            const result = validateLayoutInput([
                { ...floor, zones: [motoZone] },
            ]);
            expect(result.valid).toBe(false);
            expect(
                result.errors.some((e) =>
                    e.includes("không được chứa từng ô riêng"),
                ),
            ).toBe(true);
        });

        it("fails when motorbike zone is missing bounds", () => {
            const floor = createDefaultFloor(1);
            const motoZone = createDefaultZone("Motorbike", "MOTO-1", 20);
            motoZone.bounds = undefined;

            const result = validateLayoutInput([
                { ...floor, zones: [motoZone] },
            ]);
            expect(result.valid).toBe(false);
            expect(
                result.errors.some((e) =>
                    e.includes("cần có hình khối xác định"),
                ),
            ).toBe(true);
        });

        it("fails when car zone has more slots than declared capacity", () => {
            const floor = createDefaultFloor(1);
            const carZone = createDefaultZone("Car", "CAR-1", 1);
            carZone.slots = [
                createDefaultSlot("CAR-1", 1, { x: 0, y: 0 }),
                createDefaultSlot("CAR-1", 2, { x: 1, y: 0 }),
            ];

            const result = validateLayoutInput([
                { ...floor, zones: [carZone] },
            ]);
            expect(result.valid).toBe(false);
            expect(
                result.errors.some((e) => e.includes("vượt quá sức chứa")),
            ).toBe(true);
        });

        it("fails when car slots have duplicate codes across the lot", () => {
            const floor = createDefaultFloor(1);
            const carZone1 = createDefaultZone("Car", "CAR-1", 5);
            carZone1.slots = [createDefaultSlot("CAR-1", 1, { x: 0, y: 0 })];
            carZone1.slots[0].slotCode = "A-01";

            const carZone2 = createDefaultZone("Car", "CAR-2", 5);
            carZone2.slots = [createDefaultSlot("CAR-2", 1, { x: 0, y: 0 })];
            carZone2.slots[0].slotCode = "a-01"; // Duplicate (case-insensitive)

            floor.zones = [carZone1, carZone2];
            const result = validateLayoutInput([floor]);
            expect(result.valid).toBe(false);
            expect(
                result.errors.some((e) =>
                    e.includes("Mã ô đỗ 'a-01' bị trùng lặp"),
                ),
            ).toBe(true);
        });

        it("fails when car slots in the same zone share the same grid position", () => {
            const floor = createDefaultFloor(1);
            const carZone = createDefaultZone("Car", "CAR-1", 5);
            carZone.slots = [
                createDefaultSlot("CAR-1", 1, { x: 2, y: 3 }),
                createDefaultSlot("CAR-1", 2, { x: 2, y: 3 }),
            ];

            floor.zones = [carZone];
            const result = validateLayoutInput([floor]);
            expect(result.valid).toBe(false);
            expect(
                result.errors.some((e) => e.includes("trùng vị trí tọa độ")),
            ).toBe(true);
        });

        it("passes for a well-formed layout matching backend validator", () => {
            const floor = createDefaultFloor(1);
            const carZone = createDefaultZone("Car", "A", 2);
            carZone.slots = [
                createDefaultSlot("A", 1, { x: 0, y: 0 }),
                createDefaultSlot("A", 2, { x: 1, y: 0 }),
            ];

            const motoZone = createDefaultZone("Motorbike", "M1", 30);

            floor.zones = [carZone, motoZone];
            const result = validateLayoutInput([floor]);
            expect(result.valid).toBe(true);
            expect(result.errors).toHaveLength(0);
        });
    });

    describe("describeParkingLayoutError", () => {
        it("formats stale version error", () => {
            const err = new ApiError(409, {
                errorCode: "parking.layout.stale_version",
                title: "Layout has changed",
            });
            expect(describeParkingLayoutError(err)).toContain(
                "phiên bản mới nhất",
            );
        });

        it("formats conflict error", () => {
            const err = new ApiError(409, {
                errorCode: "parking.layout.conflict",
                title: "Existing items cannot be deleted",
            });
            expect(describeParkingLayoutError(err)).toContain(
                "Không thể xóa tầng, khu, chỗ",
            );
        });

        it("formats invalid error with details", () => {
            const err = new ApiError(400, {
                errorCode: "parking.layout.invalid",
                detail: "Motorbike zones must not contain individual slots.",
            });
            expect(describeParkingLayoutError(err)).toContain(
                "Dữ liệu sơ đồ không hợp lệ",
            );
        });

        it("formats generic error", () => {
            expect(
                describeParkingLayoutError(new TypeError("Failed to fetch")),
            ).toBe(
                "Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại kết nối mạng.",
            );
        });
    });

    describe("API calls", () => {
        const LOT_ID = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

        it("calls GET /api/parking/owner/lots/{lotId}/layout", async () => {
            const fetchSpy = vi
                .spyOn(client, "gatewayFetch")
                .mockResolvedValue({
                    lotId: LOT_ID,
                    version: 1,
                    floors: [],
                });

            const res = await getOwnerParkingLayout(LOT_ID);
            expect(fetchSpy).toHaveBeenCalledWith(
                `/api/parking/owner/lots/${LOT_ID}/layout`,
                { method: "GET" },
            );
            expect(res.version).toBe(1);
        });

        it("calls PUT /api/parking/owner/lots/{lotId}/layout with expectedVersion and floors", async () => {
            const floor = createDefaultFloor(1);
            const fetchSpy = vi
                .spyOn(client, "gatewayFetch")
                .mockResolvedValue({
                    lotId: LOT_ID,
                    version: 2,
                    floors: [floor],
                });

            const res = await saveOwnerParkingLayout(LOT_ID, {
                expectedVersion: 1,
                floors: [floor],
            });

            expect(fetchSpy).toHaveBeenCalledWith(
                `/api/parking/owner/lots/${LOT_ID}/layout`,
                expect.objectContaining({
                    method: "PUT",
                    body: JSON.stringify({
                        expectedVersion: 1,
                        floors: [floor],
                    }),
                }),
            );
            expect(res.version).toBe(2);
        });
    });
});
