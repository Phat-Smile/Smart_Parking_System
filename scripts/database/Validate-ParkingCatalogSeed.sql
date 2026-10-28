-- TECH-02. Verify the four stable development/demo fixtures without assuming
-- the database contains *only* these rows. Must be run after the Parking EF migrations.
-- Use psql -v ON_ERROR_STOP=1; a failed assertion aborts the transaction.
DO $verify$
DECLARE
    actual_total integer;
    actual_approved integer;
    actual_pending integer;
    mismatch_count integer;
BEGIN
    SELECT count(*),
           count(*) FILTER (WHERE approval_status = 'Approved'),
           count(*) FILTER (WHERE approval_status = 'Pending')
    INTO actual_total, actual_approved, actual_pending
    FROM parking.parking_lots
    WHERE id IN (
        '10000000-0000-0000-0000-000000000001',
        '10000000-0000-0000-0000-000000000002',
        '10000000-0000-0000-0000-000000000003',
        '10000000-0000-0000-0000-000000000004'
    );

    IF actual_total <> 4 OR actual_approved <> 3 OR actual_pending <> 1 THEN
        RAISE EXCEPTION 'TECH-02 seed failed: total %, Approved %, Pending % (expected 4/3/1)',
            actual_total, actual_approved, actual_pending;
    END IF;

    SELECT count(*) INTO mismatch_count
    FROM (
        VALUES
        ('10000000-0000-0000-0000-000000000001'::uuid, 'Bãi đỗ Trung tâm A', 'MultiStorey', 'Approved', 10.776530::numeric, 106.700981::numeric),
        ('10000000-0000-0000-0000-000000000002'::uuid, 'Bãi đỗ Ngoài trời B', 'Outdoor', 'Approved', 10.782100::numeric, 106.695400::numeric),
        ('10000000-0000-0000-0000-000000000003'::uuid, 'Bãi đỗ Tầng hầm C', 'Underground', 'Approved', 10.770500::numeric, 106.705200::numeric),
        ('10000000-0000-0000-0000-000000000004'::uuid, 'Bãi chờ duyệt', 'Outdoor', 'Pending', 10.775000::numeric, 106.710000::numeric)
    ) expected(id, name, type, status, lat, lng)
    LEFT JOIN parking.parking_lots lot ON lot.id = expected.id
    WHERE lot.id IS NULL OR lot.name IS DISTINCT FROM expected.name
      OR lot.type IS DISTINCT FROM expected.type
      OR lot.approval_status IS DISTINCT FROM expected.status
      OR lot.latitude IS DISTINCT FROM expected.lat
      OR lot.longitude IS DISTINCT FROM expected.lng;

    IF mismatch_count > 0 THEN
        RAISE EXCEPTION 'TECH-02 seed mismatch: % rows differ from pinned dataset', mismatch_count;
    END IF;

    RAISE NOTICE 'TECH-02 seed validated: four stable ids, 3 Approved, 1 Pending, fields match';
END $verify$;
