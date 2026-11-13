export type PlateVehicleGroup = "Car" | "Motorbike" | "Bicycle";

/** Formats a Vietnamese civilian plate for display while preserving partial input. */
export function formatVehiclePlate(input: string, group: PlateVehicleGroup) {
    const normalized = input.trim().replace(/\s+/g, " ").toUpperCase();
    if (group === "Bicycle") return normalized;
    const compact = normalized.replace(/[ .-]/g, "");
    const parts = (
        group === "Motorbike"
            ? /^(\d{2}[A-Z]\d)(\d{0,5})$/
            : /^(\d{2}[A-Z]{1,2})(\d{0,5})$/
    ).exec(compact);
    if (!parts) return normalized;
    const [, prefix, serial] = parts;
    if (!serial) return prefix;
    return `${prefix}-${serial.slice(0, 3)}${serial.length > 3 ? `.${serial.slice(3)}` : ""}`;
}

/** Format check for civilian Vietnamese plates; it does not prove ownership. */
export function validateVehiclePlate(input: string, group: PlateVehicleGroup) {
    const normalized = input
        .trim()
        .toUpperCase()
        .replace(/[\s.-]/g, "");
    if (group === "Bicycle") {
        return { valid: normalized.length === 0, normalized: "" };
    }
    if (!/^[A-Z0-9]+$/.test(normalized)) return { valid: false, normalized };
    const pattern =
        group === "Car"
            ? /^[1-9][0-9][A-Z]{1,2}[0-9]{4,5}$/
            : /^[1-9][0-9][A-Z][0-9][0-9]{4,5}$/;
    return { valid: pattern.test(normalized), normalized };
}
