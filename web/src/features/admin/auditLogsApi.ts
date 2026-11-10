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
    if (query.actionType?.trim())
        params.set("actionType", query.actionType.trim());
    if (query.parkingLotId?.trim())
        params.set("parkingLotId", query.parkingLotId.trim());
    if (query.actorId?.trim()) params.set("actorId", query.actorId.trim());
    if (query.fromUtc?.trim()) params.set("fromUtc", query.fromUtc.trim());
    if (query.toUtc?.trim()) params.set("toUtc", query.toUtc.trim());

    const response = await gatewayFetch<
        | AuditLogsPagedResponse
        | AuditLogItem[]
        | {
              items?: AuditLogItem[];
              Items?: AuditLogItem[];
              data?: AuditLogItem[];
              totalItems?: number;
              TotalItems?: number;
              totalCount?: number;
              page?: number;
              Page?: number;
              pageSize?: number;
              PageSize?: number;
              totalPages?: number;
              TotalPages?: number;
          }
    >(`/api/payment/admin/audit-logs?${params.toString()}`, { method: "GET" });

    if (Array.isArray(response)) {
        return {
            items: response,
            page: query.page ?? 1,
            pageSize: query.pageSize ?? 20,
            totalItems: response.length,
            totalPages: Math.max(
                1,
                Math.ceil(response.length / (query.pageSize ?? 20)),
            ),
        };
    }

    const raw = (response || {}) as Record<string, unknown>;
    const rawItems = raw.items ?? raw.Items ?? raw.data;
    const items = Array.isArray(rawItems) ? (rawItems as AuditLogItem[]) : [];
    const totalItems = Number(
        raw.totalItems ?? raw.TotalItems ?? raw.totalCount ?? items.length,
    );
    const page = Number(raw.page ?? raw.Page ?? query.page ?? 1);
    const pageSize = Number(
        raw.pageSize ?? raw.PageSize ?? query.pageSize ?? 20,
    );
    const totalPages = Number(
        raw.totalPages ??
            raw.TotalPages ??
            Math.max(1, Math.ceil(totalItems / pageSize)),
    );

    return {
        items,
        page,
        pageSize,
        totalItems,
        totalPages,
    };
}
