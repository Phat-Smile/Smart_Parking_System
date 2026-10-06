import { z } from "zod";

export const registerSchema = z
    .object({
        fullName: z.string().trim().min(1, "Full name is required"),
        email: z
            .string()
            .trim()
            .min(1, "Email is required")
            .email("Enter a valid email address")
            .max(254, "Email is too long"),
        password: z
            .string()
            .min(1, "Password is required")
            .min(8, "Use at least 8 characters")
            .max(72, "Use no more than 72 characters")
            .regex(/[A-Z]/, "Include an uppercase letter")
            .regex(/[a-z]/, "Include a lowercase letter")
            .regex(/[0-9]/, "Include a number")
            .regex(/[^A-Za-z0-9]/, "Include a special character"),
        confirmPassword: z.string().min(1, "Confirm your password"),
        agreeTerms: z
            .boolean()
            .refine((value) => value, "Agreement is required"),
    })
    .refine((value) => value.password === value.confirmPassword, {
        path: ["confirmPassword"],
        message: "Passwords do not match",
    });

export type RegisterValues = z.infer<typeof registerSchema>;
