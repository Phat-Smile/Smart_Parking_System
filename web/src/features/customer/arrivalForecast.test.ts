import { describe, expect, it } from "vitest";
import {
    FORECAST_HOURS,
    arrivalAtIso,
    forecastHours,
    forecastWindowStart,
    initialArrivalHour,
    percentByLot,
} from "./arrivalForecast";

describe("forecast window", () => {
    it("keeps the selected hour near the start and inside the day", () => {
        expect(forecastHours(10)).toEqual([8, 9, 10, 11, 12, 13, 14, 15]);
        expect(forecastWindowStart(0)).toBe(0);
        expect(forecastWindowStart(1)).toBe(0);
        expect(forecastHours(23)).toEqual([16, 17, 18, 19, 20, 21, 22, 23]);
        expect(forecastHours(10)).toHaveLength(FORECAST_HOURS);
    });
});

describe("arrivalAtIso", () => {
    it("builds the instant from a local date and hour", () => {
        const iso = arrivalAtIso("2026-11-13", 10);
        expect(new Date(iso).getHours()).toBe(10);
        expect(new Date(iso).getDate()).toBe(13);
    });

    it("uses today when the date is empty", () => {
        const now = new Date(2026, 10, 12, 9, 30);
        const iso = arrivalAtIso("", 14, now);
        expect(new Date(iso).getDate()).toBe(12);
        expect(new Date(iso).getHours()).toBe(14);
    });
});

describe("initialArrivalHour", () => {
    it("reads HH:MM and falls back to the next full hour", () => {
        const now = new Date(2026, 10, 12, 9, 30);
        expect(initialArrivalHour("10:15", now)).toBe(10);
        expect(initialArrivalHour("", now)).toBe(10);
        expect(initialArrivalHour("not a time", now)).toBe(10);
        expect(initialArrivalHour("", new Date(2026, 10, 12, 23, 10))).toBe(23);
    });
});

describe("percentByLot", () => {
    it("maps lot ids to rounded probabilities", () => {
        expect(
            percentByLot([
                { lotId: "a", probabilityPercent: 81.6 },
                { lotId: "b", probabilityPercent: 30.2 },
            ] as never),
        ).toEqual({ a: 82, b: 30 });
    });

    it("omits lots with null probability", () => {
        expect(
            percentByLot([
                { lotId: "a", probabilityPercent: 81.6 },
                { lotId: "b", probabilityPercent: null },
            ] as never),
        ).toEqual({ a: 82 });
    });
});
