import { z } from "zod";
import { passwordSchema } from "../passwordSchema";

const fullNamePattern = /^[\p{L}]+(?:[ '-][\p{L}]+)*$/u;
const phonePattern = /^\s*(0|\+84)[35789][0-9]{8}\s*$/;

export const registerSchema = z
    .object({
        fullName: z
            .string()
            .trim()
            .refine(
                (name) =>
                    !name ||
                    (name.length >= 2 &&
                        name.length <= 100 &&
                        fullNamePattern.test(name)),
                "Enter a valid full name (2–100 letters)",
            ),
        email: z
            .string()
            .trim()
            .min(1, "Email is required")
            .email("Enter a valid email address")
            .max(254, "Email is too long"),
        password: passwordSchema,
        phoneNumber: z
            .string()
            .trim()
            .refine(
                (phone) => !phone || phonePattern.test(phone),
                "Enter a valid Vietnamese mobile number",
            ),
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
