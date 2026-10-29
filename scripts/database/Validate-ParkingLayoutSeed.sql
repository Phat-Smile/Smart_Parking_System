-- TECH-02 validation of the supplemental layout fixtures when schema is present.
DO $validate_layout$
DECLARE
    floors_count integer;
    zones_count integer;
    slots_count integer;
BEGIN
    IF to_regclass('parking.floors') IS NULL
       OR to_regclass('parking.zones') IS NULL
       OR to_regclass('parking.parking_slots') IS NULL THEN
        RAISE NOTICE 'TECH-02: skip layout assertions (layout migration not present)';
        RETURN;
    END IF;

    SELECT count(*) INTO floors_count FROM parking.floors
      WHERE id IN ('20000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000003');
    SELECT count(*) INTO zones_count FROM parking.zones
      WHERE id IN ('30000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000003');
    SELECT count(*) INTO slots_count FROM parking.parking_slots
      WHERE id IN ('40000000-0000-0000-0000-000000000001', '40000000-0000-0000-0000-000000000002', '40000000-0000-0000-0000-000000000003');
    IF floors_count <> 3 OR zones_count <> 3 OR slots_count <> 3 THEN
        RAISE EXCEPTION 'TECH-02 layout missing: floors %, zones %, slots %', floors_count, zones_count, slots_count;
    END IF;
    IF NOT EXISTS (
      SELECT 1 FROM parking.zones z JOIN parking.floors f ON f.id = z.floor_id
      WHERE f.lot_id = '10000000-0000-0000-0000-000000000002'
        AND z.vehicle_group = 'Motorbike' AND z.capacity = 20 AND NOT z.has_slot_sensor
        AND NOT EXISTS (SELECT 1 FROM parking.parking_slots s WHERE s.zone_id = z.id)
    ) THEN
      RAISE EXCEPTION 'TECH-02 motorcycle-zone fixture is invalid';
    END IF;
    IF NOT EXISTS (
      SELECT 1 FROM parking.zones z JOIN parking.floors f ON f.id = z.floor_id
      WHERE f.lot_id = '10000000-0000-0000-0000-000000000001'
        AND z.vehicle_group = 'Car' AND z.has_slot_sensor
        AND (SELECT count(*) FROM parking.parking_slots s WHERE s.zone_id = z.id) = 2
    ) THEN
      RAISE EXCEPTION 'TECH-02 car-zone fixture is invalid';
    END IF;
    RAISE NOTICE 'TECH-02 layout validated: 3 floors, 3 zones, 3 slots';
END $validate_layout$;
