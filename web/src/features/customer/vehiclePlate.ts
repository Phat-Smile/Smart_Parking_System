export type PlateVehicleGroup = "Car" | "Motorbike" | "Bicycle";

/** Format check for civilian Vietnamese plates; it does not prove ownership. */
export function validateVehiclePlate(input: string, group: PlateVehicleGroup) {
    const normalized = input.trim().toUpperCase().replace(/[\s.-]/g, "");
    if (group === "Bicycle") {
        return { valid: normalized.length === 0, normalized: "" };
    }
    if (!/^[A-Z0-9]+$/.test(normalized)) return { valid: false, normalized };
    const pattern = group === "Car"
        ? /^[1-9][0-9][A-Z]{1,2}[0-9]{4,5}$/
        : /^[1-9][0-9][A-Z][0-9][0-9]{4,5}$/;
    return { valid: pattern.test(normalized), normalized };
}
