import { describe, expect, it } from "vitest";
import { passwordSchema } from "./passwordSchema";

describe("ParkWise password policy", () => {
    it("matches the accepted Identity length and character rules", () => {
        expect(passwordSchema.safeParse("Valid123!").success).toBe(true);
        expect(passwordSchema.safeParse("Valid123").success).toBe(false);
        expect(passwordSchema.safeParse("valid123!").success).toBe(false);
        expect(passwordSchema.safeParse("VALID123!").success).toBe(false);
        expect(passwordSchema.safeParse("ValidPassword!").success).toBe(false);
        expect(passwordSchema.safeParse("A1!" + "a".repeat(69)).success).toBe(
            true,
        );
        expect(passwordSchema.safeParse("A1!" + "a".repeat(70)).success).toBe(
            false,
        );
        expect(passwordSchema.safeParse("A1!a" + "é".repeat(34)).success).toBe(
            true,
        );
        expect(passwordSchema.safeParse("A1!a" + "é".repeat(35)).success).toBe(
            false,
        );
    });
});
