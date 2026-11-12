import { describe, expect, it } from "vitest";
import { formatVehiclePlate, validateVehiclePlate } from "./vehiclePlate";

describe("Vietnamese civilian plate format", () => {
    it.each([
        ["30F-256.58", "Car", true],
        ["51LD-123.45", "Car", true],
        ["30F-2568", "Car", true],
        ["59A1-123.45", "Motorbike", true],
        ["59A1-1234", "Motorbike", true],
        ["59A1-123.45", "Car", false],
        ["51LD-123.45", "Motorbike", false],
        ["abc", "Car", false],
        ["00A-123.45", "Car", false],
        ["", "Bicycle", true],
        ["59A1-123.45", "Bicycle", false],
    ] as const)("checks %s as %s", (plate, group, valid) => {
        expect(validateVehiclePlate(plate, group).valid).toBe(valid);
    });
    it("normalizes spacing, separators and case", () => {
        expect(
            validateVehiclePlate(" 59a1 - 123.45 ", "Motorbike").normalized,
        ).toBe("59A112345");
    });
});

describe("Vietnamese civilian plate display format", () => {
    it.each([
        ["51K12345", "Car", "51K-123.45"],
        ["51K-123.45", "Car", "51K-123.45"],
        ["59A112345", "Motorbike", "59A1-123.45"],
        ["59A1-123.45", "Motorbike", "59A1-123.45"],
        ["", "Bicycle", ""],
    ] as const)("formats %s as %s", (plate, group, expected) => {
        expect(formatVehiclePlate(plate, group)).toBe(expected);
    });
});
