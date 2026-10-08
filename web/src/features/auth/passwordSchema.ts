import { z } from "zod";

/** Matches the accepted Identity registration password policy. */
export const passwordSchema = z
    .string()
    .min(8, "Password must contain at least 8 characters")
    .max(72, "Password must contain no more than 72 characters")
    .regex(/[A-Z]/, "Password must contain an uppercase letter")
    .regex(/[a-z]/, "Password must contain a lowercase letter")
    .regex(/[0-9]/, "Password must contain a number")
    .regex(/[^A-Za-z0-9]/, "Password must contain a special character");
