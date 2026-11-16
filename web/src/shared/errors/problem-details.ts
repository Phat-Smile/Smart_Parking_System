export interface ProblemDetails {
    type?: string;
    title?: string;
    status?: number;
    detail?: string;
    instance?: string;
    traceId?: string;
    correlationId?: string;
    errorCode?: string;
    errors?: Record<string, string[]>;
    validationErrors?: Record<string, string[]>;
}

export function parseProblemDetails(
    value: unknown,
): ProblemDetails | undefined {
    if (typeof value !== "object" || value === null || Array.isArray(value))
        return undefined;
    const record = value as Record<string, unknown>;
    const result: ProblemDetails = {};
    for (const key of [
        "type",
        "title",
        "detail",
        "instance",
        "traceId",
        "correlationId",
        "errorCode",
    ] as const) {
        if (typeof record[key] === "string") result[key] = record[key];
    }
    if (typeof record.status === "number" && Number.isInteger(record.status))
        result.status = record.status;
    if (
        typeof record.errors === "object" &&
        record.errors !== null &&
        !Array.isArray(record.errors)
    ) {
        const validationErrors: Record<string, string[]> = {};
        for (const [field, messages] of Object.entries(
            record.errors as Record<string, unknown>,
        )) {
            if (Array.isArray(messages)) {
                const safeMessages = messages.filter(
                    (message): message is string => typeof message === "string",
                );
                if (safeMessages.length) validationErrors[field] = safeMessages;
            }
        }
        if (Object.keys(validationErrors).length) {
            result.validationErrors = validationErrors;
            result.errors = validationErrors;
        }
    }
    return Object.keys(result).length ? result : undefined;
}
