-- =====================================================================
-- Cleanup-ArrivalHistory.sql
-- Safely removes all simulated arrival history sessions without affecting real sessions.
-- Target Table: parking.parking_sessions
-- =====================================================================

DELETE FROM parking.parking_sessions
WHERE plate_number LIKE 'SIMC%' OR plate_number LIKE 'SIMM%';
