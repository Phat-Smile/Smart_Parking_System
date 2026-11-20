import { describe, expect, it } from "vitest";
import {
    comparePlates,
    findBestPlateMatch,
    getSubstitutionCost,
    normalizePlate,
    weightedLevenshteinDistance,
} from "./plateMatcher";

describe("Plate Normalization & Substitution Cost", () => {
    it("normalizes license plates correctly by stripping spaces, dashes, dots and uppercasing", () => {
        expect(normalizePlate("29A-128.45")).toBe("29A12845");
        expect(normalizePlate("  51f - 782.91 ")).toBe("51F78291");
        expect(normalizePlate("59-V1 / 793.79")).toBe("59V179379");
        expect(normalizePlate("")).toBe("");
        expect(normalizePlate(null)).toBe("");
        expect(normalizePlate(undefined)).toBe("");
    });

    it("assigns optical confusion cost of 0.2 for known character pairs", () => {
        // (8, B)
        expect(getSubstitutionCost("8", "B")).toBe(0.2);
        expect(getSubstitutionCost("B", "8")).toBe(0.2);

        // (0, D)
        expect(getSubstitutionCost("0", "D")).toBe(0.2);
        expect(getSubstitutionCost("D", "0")).toBe(0.2);

        // (0, O)
        expect(getSubstitutionCost("0", "O")).toBe(0.2);
        expect(getSubstitutionCost("O", "0")).toBe(0.2);

        // (1, I)
        expect(getSubstitutionCost("1", "I")).toBe(0.2);
        expect(getSubstitutionCost("I", "1")).toBe(0.2);

        // (5, S)
        expect(getSubstitutionCost("5", "S")).toBe(0.2);
        expect(getSubstitutionCost("S", "5")).toBe(0.2);

        // (2, Z)
        expect(getSubstitutionCost("2", "Z")).toBe(0.2);
        expect(getSubstitutionCost("Z", "2")).toBe(0.2);

        // Same character
        expect(getSubstitutionCost("A", "A")).toBe(0.0);
        expect(getSubstitutionCost("9", "9")).toBe(0.0);

        // Unrelated characters
        expect(getSubstitutionCost("A", "B")).toBe(1.0);
        expect(getSubstitutionCost("3", "8")).toBe(1.0);
    });
});

describe("Weighted Levenshtein Distance", () => {
    it("returns 0 distance for identical plates", () => {
        const dist = weightedLevenshteinDistance("29A-12845", "29A-12845");
        expect(dist).toBe(0);
    });

    it("returns 0.2 distance for optical confusion case 8 vs B (29A-12845 vs 29A-12B45)", () => {
        const dist = weightedLevenshteinDistance("29A-12845", "29A-12B45");
        expect(dist).toBe(0.2);
    });

    it("returns 0.2 distance for optical confusion case 0 vs D (51D-12345 vs 510-12345)", () => {
        const dist = weightedLevenshteinDistance("51D-12345", "510-12345");
        expect(dist).toBe(0.2);
    });

    it("returns 0.2 distance for optical confusion case 0 vs O", () => {
        const dist = weightedLevenshteinDistance("30F-01234", "30F-O1234");
        expect(dist).toBe(0.2);
    });

    it("returns 0.2 distance for optical confusion case 1 vs I", () => {
        const dist = weightedLevenshteinDistance("43A-19999", "43A-I9999");
        expect(dist).toBe(0.2);
    });

    it("returns 0.2 distance for optical confusion case 5 vs S", () => {
        const dist = weightedLevenshteinDistance("60B-56789", "60B-S6789");
        expect(dist).toBe(0.2);
    });

    it("returns 0.2 distance for optical confusion case 2 vs Z", () => {
        const dist = weightedLevenshteinDistance("72A-23456", "72A-Z3456");
        expect(dist).toBe(0.2);
    });

    it("returns large distance for completely different plates", () => {
        const dist = weightedLevenshteinDistance("29A-12845", "51F-99999");
        expect(dist).toBeGreaterThanOrEqual(5.0);
    });
});

describe("Plate Comparison & findBestPlateMatch", () => {
    it("accurately scores identical plates as score = 1.0 and isHighConfidence = true", () => {
        const result = comparePlates("29A-12845", "29A-12845");
        expect(result.distance).toBe(0);
        expect(result.score).toBe(1.0);
        expect(result.isExactMatch).toBe(true);
        expect(result.isHighConfidence).toBe(true);
    });

    it("recognizes optical confusion 29A-12845 vs 29A-12B45 with high confidence (score > 0.95)", () => {
        const result = comparePlates("29A-12845", "29A-12B45");
        expect(result.distance).toBe(0.2);
        expect(result.score).toBeGreaterThan(0.95);
        expect(result.isExactMatch).toBe(false);
        expect(result.isHighConfidence).toBe(true);
    });

    it("recognizes optical confusion 51D-12345 vs 510-12345 with high confidence (score > 0.95)", () => {
        const result = comparePlates("51D-12345", "510-12345");
        expect(result.distance).toBe(0.2);
        expect(result.score).toBeGreaterThan(0.95);
        expect(result.isHighConfidence).toBe(true);
    });

    it("flags completely different plates with low score and isHighConfidence = false", () => {
        const result = comparePlates("29A-12845", "51F-99999");
        expect(result.score).toBeLessThan(0.5);
        expect(result.isHighConfidence).toBe(false);
        expect(result.isExactMatch).toBe(false);
    });

    it("finds best match from candidate list when camera misread 8 as B", () => {
        const scannedPlate = "29A-12B45"; // OCR read 'B' instead of '8'
        const candidates = [
            "51F-78291",
            "30E-88888",
            "29A-12845", // True plate in parking lot
            "59V1-79379",
        ];

        const match = findBestPlateMatch(scannedPlate, candidates);
        expect(match.bestMatch).toBe("29A-12845");
        expect(match.distance).toBe(0.2);
        expect(match.score).toBeGreaterThan(0.95);
        expect(match.isHighConfidence).toBe(true);
    });

    it("never treats two empty plates as an exact or confident match", () => {
        const result = comparePlates("", "   ");
        expect(result.score).toBe(0);
        expect(result.isExactMatch).toBe(false);
        expect(result.isHighConfidence).toBe(false);
    });

    it("does not choose arbitrarily between equally-close distinct plates", () => {
        const scanned = "29A-12B45";
        const left = "29A-12845";
        const right = "29A-12B4S";
        const forward = findBestPlateMatch(scanned, [left, right]);
        const reverse = findBestPlateMatch(scanned, [right, left]);
        expect(forward.bestMatch).toBeNull();
        expect(reverse.bestMatch).toBeNull();
        expect(forward.isHighConfidence).toBe(false);
        expect(reverse.isHighConfidence).toBe(false);
    });

    it("deduplicates alternate formatting of the same plate", () => {
        const result = findBestPlateMatch("29A-12845", ["29A-128.45", "29A12845"]);
        expect(result.bestMatch).toBe("29A-128.45");
        expect(result.isHighConfidence).toBe(true);
    });

    it("handles empty candidates gracefully", () => {
        const match = findBestPlateMatch("29A-12845", []);
        expect(match.bestMatch).toBeNull();
        expect(match.score).toBe(0);
        expect(match.isHighConfidence).toBe(false);
    });

    it("handles empty scanned plate gracefully", () => {
        const match = findBestPlateMatch("", ["29A-12845"]);
        expect(match.bestMatch).toBeNull();
        expect(match.score).toBe(0);
        expect(match.isHighConfidence).toBe(false);
    });
});
