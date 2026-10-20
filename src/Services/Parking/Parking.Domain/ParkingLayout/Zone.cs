using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Domain.ParkingLayout;

public sealed class Zone
{
    public const int CodeMaxLength = 50;

    private Zone()
    {
    }

    public Guid Id { get; private set; }
    public Guid FloorId { get; private set; }
    public string Code { get; private set; } = null!;

    // BR-07.a: one vehicle group per zone.
    public VehicleType VehicleGroup { get; private set; }

    // Declared by the owner (context section 5). For a car zone the real limit is its usable slots (BR-02.a).
    public int Capacity { get; private set; }

    // Motorbike zones only: in - out, never below 0 (BR-07.b). Kept for US-PARK-008; TECH-06 does not change it.
    public int CurrentVehicleCount { get; private set; }

    // BR-04.c: without a slot sensor, staff confirm that the car has parked.
    public bool HasSlotSensor { get; private set; }

    public static Zone Create(Guid id, Guid floorId, string code, VehicleType vehicleGroup, int capacity, bool hasSlotSensor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        return new Zone
        {
            Id = id,
            FloorId = floorId,
            Code = code.Trim(),
            VehicleGroup = vehicleGroup,
            Capacity = capacity,
            HasSlotSensor = hasSlotSensor
        };
    }
}
