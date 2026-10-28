-- TECH-02 supplemental layout fixtures for disposable migrated Parking databases.
-- Leave BE-5 pricing tables untouched; no pricing schema is assumed here.
-- When the layout migration has not landed yet, safely skip these fixtures.
DO $seed_layout$
BEGIN
    IF to_regclass('parking.floors') IS NULL
       OR to_regclass('parking.zones') IS NULL
       OR to_regclass('parking.parking_slots') IS NULL THEN
        RAISE NOTICE 'TECH-02: layout tables not migrated; catalog seed only';
        RETURN;
    END IF;

    INSERT INTO parking.floors (id, lot_id, name) VALUES
      ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'Tầng 1'),
      ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', 'Tầng trệt'),
      ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003', 'Tầng hầm 1')
    ON CONFLICT (id) DO NOTHING;

    -- BR-07: motorcycle zones have declared capacity but NO individual car slots.
    INSERT INTO parking.zones
      (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
    VALUES
      ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'CAR-A', 'Car', 2, 0, true),
      ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000002', 'MOTO-B', 'Motorbike', 20, 0, false),
      ('30000000-0000-0000-0000-000000000003', '20000000-0000-0000-0000-000000000003', 'CAR-C', 'Car', 1, 0, false)
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO parking.parking_slots
      (id, lot_id, zone_id, slot_code, effective_status, row_version)
    VALUES
      ('40000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', 'A-01', 'Available', 0),
      ('40000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', 'A-02', 'Available', 0),
      ('40000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000003', 'C-01', 'Available', 0)
    ON CONFLICT (id) DO NOTHING;
END $seed_layout$;
