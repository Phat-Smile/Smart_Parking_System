import { gatewayFetch } from "@/shared/api/client";

export interface AuditLogItem {
    id: string;
    occurredAtUtc: string;
    actionType: string;
    sourceService: string;
    actorId: string | null;
    actorEmail: string | null;
    actorRole: string;
    parkingLotId: string | null;
    targetType: string;
    targetId: string;
    reason: string;
    ipAddress: string | null;
    details: unknown;
}

export interface AuditLogsPagedResponse {
    items: AuditLogItem[];
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
}

export interface AuditLogsQuery {
    page?: number;
    pageSize?: number;
    actionType?: string;
    parkingLotId?: string;
    actorId?: string;
    fromUtc?: string;
    toUtc?: string;
}

export async function getAdminAuditLogs(
    query: AuditLogsQuery = {},
): Promise<AuditLogsPagedResponse> {
    const params = new URLSearchParams();
    params.set("page", String(query.page ?? 1));
    params.set("pageSize", String(query.pageSize ?? 20));
    if (query.actionType?.trim()) params.set("actionType", query.actionType.trim());
    if (query.parkingLotId?.trim()) params.set("parkingLotId", query.parkingLotId.trim());
    if (query.actorId?.trim()) params.set("actorId", query.actorId.trim());
    if (query.fromUtc?.trim()) params.set("fromUtc", query.fromUtc.trim());
    if (query.toUtc?.trim()) params.set("toUtc", query.toUtc.trim());

    return await gatewayFetch<AuditLogsPagedResponse>(
        `/api/payment/admin/audit-logs?${params.toString()}`,
        { method: "GET" },
    );
}
