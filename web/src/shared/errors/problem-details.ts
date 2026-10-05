export interface ProblemDetails {
    type?: string;
    title?: string;
    status?: number;
    detail?: string;
    instance?: string;
    traceId?: string;
    correlationId?: string;
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
    ] as const) {
        if (typeof record[key] === "string") result[key] = record[key];
    }
    if (typeof record.status === "number" && Number.isInteger(record.status))
        result.status = record.status;
    return Object.keys(result).length ? result : undefined;
}
