-- Development/demo seed for US-SEARCH-001 (Parking Catalog).
-- Apply Parking migrations first. This script intentionally seeds only ParkingLot data;
-- Zone/Spot/Pricing seed belongs to TECH-02 once those tables exist on develop.

INSERT INTO parking.parking_lots
    (id, name, address, latitude, longitude, type, approval_status)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'Bãi đỗ Trung tâm A', '01 Đường Mẫu, TP. Hồ Chí Minh', 10.776530, 106.700981, 'MultiStorey', 'Approved'),
    ('10000000-0000-0000-0000-000000000002', 'Bãi đỗ Ngoài trời B', '02 Đường Mẫu, TP. Hồ Chí Minh', 10.782100, 106.695400, 'Outdoor', 'Approved'),
    ('10000000-0000-0000-0000-000000000003', 'Bãi đỗ Tầng hầm C', '03 Đường Mẫu, TP. Hồ Chí Minh', 10.770500, 106.705200, 'Underground', 'Approved'),
    ('10000000-0000-0000-0000-000000000004', 'Bãi chờ duyệt', '04 Đường Mẫu, TP. Hồ Chí Minh', 10.775000, 106.710000, 'Outdoor', 'Pending')
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    address = EXCLUDED.address,
    latitude = EXCLUDED.latitude,
    longitude = EXCLUDED.longitude,
    type = EXCLUDED.type,
    approval_status = EXCLUDED.approval_status;
