/**
 * lotGeo.ts – Tọa độ địa lý [longitude, latitude] cho từng bãi xe theo ID.
 * Không thêm trường vào kiểu ParkingLot gốc.
 */

export const LOT_COORDINATES: Record<number, [number, number]> = {
    1: [106.7035, 10.7745], // Bãi xe Nguyễn Huệ (Quận 1)
    2: [106.6982, 10.7725], // Bãi xe Bến Thành (Quận 1)
    3: [106.7195, 10.776], // Bãi xe Thủ Thiêm (TP. Thủ Đức)
    4: [106.715, 10.732], // Bãi xe Phú Mỹ Hưng (Quận 7)
};

export function getLotCoordinates(lotId: number): [number, number] {
    if (LOT_COORDINATES[lotId]) {
        return LOT_COORDINATES[lotId];
    }
    // Tọa độ mặc định tại TP.HCM cho bãi tạo mới
    const offset = (lotId * 0.007) % 0.04;
    return [106.7 + offset, 10.775 - offset];
}
