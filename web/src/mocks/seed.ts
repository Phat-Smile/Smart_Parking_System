import { ParkingLot, Slot } from "../domain/types";

export const MOCK_LOTS: ParkingLot[] = [
    {
        id: "lot-1",
        name: "Vincom Center Landmark 81",
        address: "720A Dien Bien Phu, Binh Thanh, HCMC",
        location: { lat: 10.7946, lng: 106.7225 },
        operatingHours: { open: "08:00", close: "22:00", is24Hours: false },
        vehicleTypes: ["sedan", "suv", "ev"],
        floors: [
            { id: "f1", level: 1, label: "B1" },
            { id: "f2", level: 2, label: "B2" },
            { id: "f3", level: 3, label: "B3" },
        ],
        zones: [
            {
                id: "z1",
                name: "Zone A",
                floorId: "f1",
                vehicleType: "suv",
                capacity: 20,
                status: "active",
            },
            {
                id: "z2",
                name: "Zone B",
                floorId: "f1",
                vehicleType: "ev",
                capacity: 10,
                status: "active",
            },
        ],
        pricing: { baseRate: 30000, dailyMax: 300000 },
        holdDeposit: 50000,
        reservationPolicy: { allowReservations: true, holdDurationMinutes: 30 },
        approval: { status: "approved" },
        status: "active",
    },
    // We can add 2 more lots to meet the "3 lots" requirement.
];

export const generateMockSlots = (): Slot[] => {
    const slots: Slot[] = [];
    const floors = ["f1", "f2", "f3"];
    let slotIndex = 1;

    floors.forEach((floorId) => {
        // 20 slots per floor (2 rows x 10)
        for (let row = 0; row < 2; row++) {
            for (let index = 0; index < 10; index++) {
                let status: Slot["status"] = "available";
                const rand = Math.random();

                if (rand < 0.2) status = "occupied";
                else if (rand < 0.3) status = "assigned";
                else if (rand < 0.35) status = "maintenance";

                const vehicle =
                    status === "occupied"
                        ? {
                              plate: `51F-${100 + slotIndex}`,
                              type: rand < 0.1 ? "ev" : "sedan",
                          }
                        : undefined;

                slots.push({
                    id: `slot-${slotIndex}`,
                    code: `${floorId.toUpperCase()}-${row}${index}`,
                    floorId,
                    zoneId: row === 0 ? "z1" : "z2",
                    row,
                    index,
                    vehicleType: row === 0 ? "suv" : "sedan",
                    status,
                    vehicle,
                });
                slotIndex++;
            }
        }
    });

    return slots;
};

export const MOCK_SLOTS = generateMockSlots();
