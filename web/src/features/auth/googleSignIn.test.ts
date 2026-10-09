import { describe, expect, it } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import { describeGoogleSignInError } from "./googleSignIn";

const problem = (errorCode: string) => ({ errorCode }) as never;

describe("Google sign-in errors", () => {
    it.each([
        [
            401,
            "identity.invalid_google_token",
            "Google sign-in could not be verified. Please try again.",
        ],
        [
            503,
            "identity.google_login_unavailable",
            "Google sign-in is not available right now. Use your email and password instead.",
        ],
        [
            403,
            "identity.account_suspended",
            "This account is suspended or disabled. Please contact support.",
        ],
        [
            403,
            "identity.account_disabled",
            "This account is suspended or disabled. Please contact support.",
        ],
        [
            423,
            "identity.account_temporarily_locked",
            "This account is temporarily locked. Please try again later.",
        ],
        [
            500,
            "internal.unexpected",
            "We couldn't sign you in with Google. Please try again later.",
        ],
    ])("maps %i %s to a clear message", (status, code, expected) => {
        expect(
            describeGoogleSignInError(
                new ApiError(status, problem(code), undefined),
            ),
        ).toBe(expected);
    });

    it("adds the support ID when the gateway returns one", () => {
        expect(
            describeGoogleSignInError(
                new ApiError(
                    401,
                    problem("identity.invalid_google_token"),
                    "abc-123",
                ),
            ),
        ).toBe(
            "Google sign-in could not be verified. Please try again. Support ID: abc-123",
        );
    });

    it("reports a network failure separately", () => {
        expect(describeGoogleSignInError(new TypeError("fetch failed"))).toBe(
            "We couldn't connect. Check your connection and try again.",
        );
    });
});
