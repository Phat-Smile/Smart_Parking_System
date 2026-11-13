-- DEMO-ONLY fixture. Source snapshot a40485e990; apply ONLY after all Parking migrations.
-- Never execute against shared/production databases or overwrite owner-declared real information.
\set ON_ERROR_STOP on
BEGIN;
DO $$ BEGIN
  IF current_database() <> 'parking_system_qa_migration' THEN
    RAISE EXCEPTION 'Refusing to seed unexpected database: %', current_database();
  END IF;
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='parking' AND table_name='parking_lots' AND column_name='public_info') THEN
    RAISE EXCEPTION 'Missing public_info column; apply Parking EF migrations first';
  END IF;
END $$;

-- Approved demo lot 10000000-0000-0000-0000-000000000001
UPDATE parking.parking_lots SET
 opening_hours = '{"days":[{"dayOfWeek":0,"isClosed":false,"is24Hours":true,"opensAt":null,"closesAt":null},{"dayOfWeek":1,"isClosed":false,"is24Hours":true,"opensAt":null,"closesAt":null},{"dayOfWeek":2,"isClosed":false,"is24Hours":true,"opensAt":null,"closesAt":null},{"dayOfWeek":3,"isClosed":false,"is24Hours":true,"opensAt":null,"closesAt":null},{"dayOfWeek":4,"isClosed":false,"is24Hours":true,"opensAt":null,"closesAt":null},{"dayOfWeek":5,"isClosed":false,"is24Hours":true,"opensAt":null,"closesAt":null},{"dayOfWeek":6,"isClosed":false,"is24Hours":true,"opensAt":null,"closesAt":null}]}'::jsonb,
 supports_electric_vehicles = true,
 supports_large_vehicles = true,
 has_ev_charging = true,
 is_covered = true,
 has_cctv = true,
 public_info = '{"description":"[DEMO] Bãi nhiều tầng ở trung tâm thành phố; các thông tin vận hành chỉ là mẫu QA, không phải công bố từ chủ bãi thật.","entryInstructions":"[DEMO] Đi theo biển P và nhận hướng dẫn từ nhân viên tại cổng.","rules":["[DEMO] Đỗ xe đúng ô do nhân viên chỉ định.","[DEMO] Giữ phiếu hoặc mã phiên khi nhận xe."],"photoUrls":["/images/parkwise/dashboard-parking-garage.webp"],"photosAreIllustrative":true,"paymentMethods":["[DEMO] Tiền mặt","[DEMO] Ví điện tử"],"evChargingPoints":2}'::jsonb
WHERE id = '10000000-0000-0000-0000-000000000001' AND approval_status = 'Approved';

INSERT INTO parking.pricing_policies
 ("Id", "LotId", "VehicleType", "Version", "BillingMode", "BlockMinutes", "Price", "OvernightFee", "EffectiveFromUtc")
SELECT '50000000-0000-0000-0000-000000000001'::uuid, id, 'CAR', 1, 'Block', 60, 20000, 30000, '2026-01-01T00:00:00Z'::timestamptz
FROM parking.parking_lots WHERE id='10000000-0000-0000-0000-000000000001' AND approval_status='Approved'
ON CONFLICT ("LotId", "VehicleType", "Version") DO NOTHING;

-- Approved demo lot 10000000-0000-0000-0000-000000000002
UPDATE parking.parking_lots SET
 opening_hours = '{"days":[{"dayOfWeek":0,"isClosed":false,"is24Hours":false,"opensAt":"06:00","closesAt":"22:00"},{"dayOfWeek":1,"isClosed":false,"is24Hours":false,"opensAt":"06:00","closesAt":"22:00"},{"dayOfWeek":2,"isClosed":false,"is24Hours":false,"opensAt":"06:00","closesAt":"22:00"},{"dayOfWeek":3,"isClosed":false,"is24Hours":false,"opensAt":"06:00","closesAt":"22:00"},{"dayOfWeek":4,"isClosed":false,"is24Hours":false,"opensAt":"06:00","closesAt":"22:00"},{"dayOfWeek":5,"isClosed":false,"is24Hours":false,"opensAt":"06:00","closesAt":"22:00"},{"dayOfWeek":6,"isClosed":false,"is24Hours":false,"opensAt":"06:00","closesAt":"22:00"}]}'::jsonb,
 supports_electric_vehicles = false,
 supports_large_vehicles = false,
 has_ev_charging = false,
 is_covered = false,
 has_cctv = true,
 public_info = '{"description":"[DEMO] Bãi đỗ ngoài trời có khu ô tô và khu xe máy; thông tin dùng để thử tích hợp FE–BE, không phải dữ liệu bãi thật.","entryInstructions":"[DEMO] Đến đúng tọa độ trên bản đồ mẫu, kiểm tra biển P rồi liên hệ nhân viên tại cổng.","rules":["[DEMO] Đi chậm trong khu vực bãi.","[DEMO] Tuân theo hướng dẫn điều phối.","[DEMO] Không để tài sản có giá trị trong xe."],"photoUrls":["/images/parkwise/dashboard-parking-entrance.webp","/images/parkwise/dashboard-parking-structure.webp"],"photosAreIllustrative":true,"paymentMethods":["[DEMO] Tiền mặt","[DEMO] Ví điện tử"],"evChargingPoints":0}'::jsonb
WHERE id = '10000000-0000-0000-0000-000000000002' AND approval_status = 'Approved';

INSERT INTO parking.pricing_policies
 ("Id", "LotId", "VehicleType", "Version", "BillingMode", "BlockMinutes", "Price", "OvernightFee", "EffectiveFromUtc")
SELECT '50000000-0000-0000-0000-000000000002'::uuid, id, 'CAR', 1, 'Block', 60, 15000, 30000, '2026-01-01T00:00:00Z'::timestamptz
FROM parking.parking_lots WHERE id='10000000-0000-0000-0000-000000000002' AND approval_status='Approved'
ON CONFLICT ("LotId", "VehicleType", "Version") DO NOTHING;

-- Approved demo lot 10000000-0000-0000-0000-000000000003
UPDATE parking.parking_lots SET
 opening_hours = '{"days":[{"dayOfWeek":0,"isClosed":false,"is24Hours":false,"opensAt":"07:00","closesAt":"23:00"},{"dayOfWeek":1,"isClosed":false,"is24Hours":false,"opensAt":"07:00","closesAt":"23:00"},{"dayOfWeek":2,"isClosed":false,"is24Hours":false,"opensAt":"07:00","closesAt":"23:00"},{"dayOfWeek":3,"isClosed":false,"is24Hours":false,"opensAt":"07:00","closesAt":"23:00"},{"dayOfWeek":4,"isClosed":false,"is24Hours":false,"opensAt":"07:00","closesAt":"23:00"},{"dayOfWeek":5,"isClosed":false,"is24Hours":false,"opensAt":"07:00","closesAt":"23:00"},{"dayOfWeek":6,"isClosed":false,"is24Hours":false,"opensAt":"07:00","closesAt":"23:00"}]}'::jsonb,
 supports_electric_vehicles = false,
 supports_large_vehicles = false,
 has_ev_charging = false,
 is_covered = true,
 has_cctv = true,
 public_info = '{"description":"[DEMO] Bãi đỗ tầng hầm, mô phỏng giờ hoạt động và nội quy để kiểm thử UI.","entryInstructions":"[DEMO] Đi theo đường xuống hầm, quan sát chiều cao giới hạn thực tế trước khi vào.","rules":["[DEMO] Giảm tốc độ khi xuống hầm.","[DEMO] Tắt động cơ tại vị trí được yêu cầu."],"photoUrls":["/images/parkwise/dashboard-parking-structure.webp"],"photosAreIllustrative":true,"paymentMethods":["[DEMO] Tiền mặt","[DEMO] Ví điện tử"],"evChargingPoints":0}'::jsonb
WHERE id = '10000000-0000-0000-0000-000000000003' AND approval_status = 'Approved';

INSERT INTO parking.pricing_policies
 ("Id", "LotId", "VehicleType", "Version", "BillingMode", "BlockMinutes", "Price", "OvernightFee", "EffectiveFromUtc")
SELECT '50000000-0000-0000-0000-000000000003'::uuid, id, 'CAR', 1, 'Block', 60, 22000, 30000, '2026-01-01T00:00:00Z'::timestamptz
FROM parking.parking_lots WHERE id='10000000-0000-0000-0000-000000000003' AND approval_status='Approved'
ON CONFLICT ("LotId", "VehicleType", "Version") DO NOTHING;

-- Correct the earlier QA mismatch: B had motorbike layout in FE but motorbike availability was NULL.
-- Only add the seeded zone when none exists. Never touch occupancy of an existing zone.
INSERT INTO parking.floors (id, lot_id, name)
SELECT '20000000-0000-0000-0000-000000000002', id, 'Tầng trệt'
FROM parking.parking_lots
WHERE id = '10000000-0000-0000-0000-000000000002' AND approval_status = 'Approved'
ON CONFLICT (id) DO NOTHING;

INSERT INTO parking.zones
 (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
SELECT '30000000-0000-0000-0000-000000000002',
 '20000000-0000-0000-0000-000000000002', 'MOTO-B', 'Motorbike', 20, 0, false
WHERE NOT EXISTS (
 SELECT 1 FROM parking.zones z JOIN parking.floors f ON f.id=z.floor_id
 WHERE f.lot_id = '10000000-0000-0000-0000-000000000002' AND z.vehicle_group='Motorbike')
ON CONFLICT (id) DO NOTHING;

-- Ensure B has car slots only if it does not have any; avoid duplicating existing 10-car QA fixtures.
DO $car_fixture$
DECLARE
    i integer;
BEGIN
 IF NOT EXISTS(SELECT 1 FROM parking.parking_slots WHERE lot_id='10000000-0000-0000-0000-000000000002') THEN
   INSERT INTO parking.zones(id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
   VALUES('31000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000002',
     'CAR-B', 'Car', 10, 0, false)
   ON CONFLICT (id) DO NOTHING;
   FOR i IN 1..10 LOOP
     INSERT INTO parking.parking_slots(id,lot_id,zone_id,slot_code,effective_status,row_version)
     VALUES(('43000000-0000-0000-0000-'||lpad(i::text,12,'0'))::uuid,
       '10000000-0000-0000-0000-000000000002',
       '31000000-0000-0000-0000-000000000002', 'B-'||lpad(i::text,2,'0'), 'Available', 0)
     ON CONFLICT (id) DO NOTHING;
   END LOOP;
 END IF;
END $car_fixture$;

INSERT INTO parking.pricing_policies
 ("Id", "LotId", "VehicleType", "Version", "BillingMode", "BlockMinutes", "Price", "OvernightFee", "EffectiveFromUtc")
SELECT '52000000-0000-0000-0000-000000000002', id, 'MOTORBIKE', 1,
       'PerEntry', NULL, 5000, 10000, '2026-01-01T00:00:00Z'::timestamptz
FROM parking.parking_lots
WHERE id='10000000-0000-0000-0000-000000000002' AND approval_status='Approved'
ON CONFLICT ("LotId", "VehicleType", "Version") DO NOTHING;

-- Explicit post-seed assertions, rolled back if they fail.
DO $assert$
DECLARE
  present_count integer;
BEGIN
 SELECT count(*) INTO present_count FROM parking.parking_lots
 WHERE id IN ('10000000-0000-0000-0000-000000000001',
              '10000000-0000-0000-0000-000000000002',
              '10000000-0000-0000-0000-000000000003')
 AND opening_hours IS NOT NULL AND public_info IS NOT NULL AND approval_status='Approved';
 IF present_count <> 3 THEN
   RAISE EXCEPTION 'Expected 3 complete demo lots, found %', present_count;
 END IF;
END $assert$;
COMMIT;

SELECT p.id,p.name,p.opening_hours IS NOT NULL AS has_hours,
       p.public_info->>'description' AS description,
       (SELECT count(*) FROM parking.pricing_policies t WHERE t."LotId"=p.id) AS price_policies
FROM parking.parking_lots p WHERE p.id IN (
 '10000000-0000-0000-0000-000000000001',
 '10000000-0000-0000-0000-000000000002',
 '10000000-0000-0000-0000-000000000003');
