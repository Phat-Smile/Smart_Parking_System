import type { ProblemDetails } from "./problem-details";

export class ApiError extends Error {
    constructor(
        public readonly status: number,
        public readonly problem: ProblemDetails | undefined,
        public readonly correlationId: string | undefined,
    ) {
        // Deliberately omit server detail/exception text from user-facing error messages.
        super(problem?.title || "The request could not be completed.");
        this.name = "ApiError";
    }

    get traceId(): string | undefined {
        return this.problem?.traceId;
    }
}
