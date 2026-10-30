export type ParkingSpot = {
    id: string;
    side: "north" | "south";
    index: number;
    status: "available" | "occupied" | "reserved";
    feature?: "ev" | "accessible";
};

const occupied = new Set([3, 4, 7, 9, 14, 18, 20, 22]);
const reserved = new Set([11, 17]);

export const parkingSpots: ParkingSpot[] = Array.from(
    { length: 24 },
    (_, index) => {
        const number = index + 1;
        return {
            id: `A${String(number).padStart(2, "0")}`,
            side: index < 12 ? "north" : "south",
            index: index % 12,
            status: occupied.has(number)
                ? "occupied"
                : reserved.has(number)
                  ? "reserved"
                  : "available",
            feature: [2, 8, 16].includes(number)
                ? "ev"
                : [12, 13].includes(number)
                  ? "accessible"
                  : undefined,
        };
    },
);
