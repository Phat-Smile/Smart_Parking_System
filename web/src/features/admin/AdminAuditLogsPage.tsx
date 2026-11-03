"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { AdminIcon, formatDate } from "./AdminUi";
import {
    getAdminAuditLogs,
    type AuditLogItem,
    type AuditLogsQuery,
} from "./auditLogsApi";
import { useLanguage } from "@/shared/i18n/LanguageContext";

const ACTION_TYPE_OPTIONS = [
    { value: "", label: "Tất cả hành động" },
    { value: "WalletTopUp", label: "Nạp tiền ví (Top-up)" },
    { value: "WalletPayment", label: "Thanh toán ví (Payment)" },
    { value: "WalletRefund", label: "Hoàn tiền ví (Refund)" },
    { value: "PricingConfigured", label: "Cấu hình bảng giá (Pricing)" },
    { value: "DepositPolicyConfigured", label: "Chính sách đặt cọc (Deposit)" },
    { value: "SessionOpened", label: "Mở phiên đỗ xe (Entry)" },
    { value: "SessionCompleted", label: "Kết thúc phiên đỗ (Exit)" },
    { value: "SlotStatusChanged", label: "Trạng thái ô đỗ (Sensor/IoT)" },
];

function getActionBadgeStyle(actionType: string): {
    bg: string;
    color: string;
    border: string;
} {
    if (actionType.includes("TopUp") || actionType.includes("Refund")) {
        return {
            bg: "rgba(16, 185, 129, 0.12)",
            color: "#059669",
            border: "rgba(16, 185, 129, 0.3)",
        };
    }
    if (actionType.includes("Payment")) {
        return {
            bg: "rgba(59, 130, 246, 0.12)",
            color: "#2563eb",
            border: "rgba(59, 130, 246, 0.3)",
        };
    }
    if (actionType.includes("Pricing") || actionType.includes("Policy")) {
        return {
            bg: "rgba(245, 158, 11, 0.12)",
            color: "#d97706",
            border: "rgba(245, 158, 11, 0.3)",
        };
    }
    if (actionType.includes("Session")) {
        return {
            bg: "rgba(139, 92, 246, 0.12)",
            color: "#7c3aed",
            border: "rgba(139, 92, 246, 0.3)",
        };
    }
    return {
        bg: "rgba(100, 116, 139, 0.12)",
        color: "#475569",
        border: "rgba(100, 116, 139, 0.3)",
    };
}

export function AdminAuditLogsPage() {
    const { t } = useLanguage();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    const [page, setPage] = useState(1);
    const [actionType, setActionType] = useState("");
    const [parkingLotId, setParkingLotId] = useState("");
    const [actorId, setActorId] = useState("");
    const [fromUtc, setFromUtc] = useState("");
    const [toUtc, setToUtc] = useState("");
    const [selectedLog, setSelectedLog] = useState<AuditLogItem | null>(null);

    const queryKey = [
        "admin-audit-logs",
        page,
        actionType,
        parkingLotId,
        actorId,
        fromUtc,
        toUtc,
    ];

    const { data, isLoading, isError, error, refetch } = useQuery({
        queryKey,
        queryFn: () =>
            getAdminAuditLogs({
                page,
                pageSize: 20,
                actionType: actionType || undefined,
                parkingLotId: parkingLotId || undefined,
                actorId: actorId || undefined,
                fromUtc: fromUtc ? new Date(fromUtc).toISOString() : undefined,
                toUtc: toUtc ? new Date(toUtc).toISOString() : undefined,
            }),
        enabled: Boolean(session),
    });

    const resetFilters = () => {
        setActionType("");
        setParkingLotId("");
        setActorId("");
        setFromUtc("");
        setToUtc("");
        setPage(1);
    };

    return (
        <div className="admin-main">
            <header className="admin-heading">
                <div>
                    <span className="admin-eyebrow">Quản trị & Đối soát</span>
                    <h1>Nhật ký kiểm toán hệ thống</h1>
                    <p style={{ color: "var(--muted)", marginTop: "4px" }}>
                        Tra cứu toàn bộ biến động tài chính ví, đối soát giao
                        dịch, cập nhật giá và lịch sử hệ thống (US-ADMIN-005).
                    </p>
                </div>
            </header>

            {/* Filter toolbar */}
            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
                    gap: "12px",
                    background: "#fff",
                    padding: "16px 20px",
                    borderRadius: "12px",
                    border: "1px solid var(--line)",
                    marginBottom: "20px",
                    boxShadow: "0 1px 3px rgba(0,0,0,0.02)",
                }}
            >
                <div>
                    <label
                        style={{
                            display: "block",
                            fontSize: "12px",
                            fontWeight: 700,
                            marginBottom: "6px",
                            color: "var(--muted)",
                        }}
                    >
                        Loại hành động
                    </label>
                    <select
                        aria-label="Loại hành động"
                        value={actionType}
                        onChange={(e) => {
                            setActionType(e.target.value);
                            setPage(1);
                        }}
                        style={{
                            width: "100%",
                            padding: "8px 12px",
                            borderRadius: "8px",
                            border: "1px solid var(--line)",
                            background: "#fff",
                            fontSize: "13px",
                        }}
                    >
                        {ACTION_TYPE_OPTIONS.map((opt) => (
                            <option key={opt.value} value={opt.value}>
                                {opt.label}
                            </option>
                        ))}
                    </select>
                </div>

                <div>
                    <label
                        style={{
                            display: "block",
                            fontSize: "12px",
                            fontWeight: 700,
                            marginBottom: "6px",
                            color: "var(--muted)",
                        }}
                    >
                        Từ thời điểm
                    </label>
                    <input
                        type="datetime-local"
                        aria-label="Từ ngày"
                        value={fromUtc}
                        onChange={(e) => {
                            setFromUtc(e.target.value);
                            setPage(1);
                        }}
                        style={{
                            width: "100%",
                            padding: "8px 12px",
                            borderRadius: "8px",
                            border: "1px solid var(--line)",
                            fontSize: "13px",
                        }}
                    />
                </div>

                <div>
                    <label
                        style={{
                            display: "block",
                            fontSize: "12px",
                            fontWeight: 700,
                            marginBottom: "6px",
                            color: "var(--muted)",
                        }}
                    >
                        Đến thời điểm
                    </label>
                    <input
                        type="datetime-local"
                        aria-label="Đến ngày"
                        value={toUtc}
                        onChange={(e) => {
                            setToUtc(e.target.value);
                            setPage(1);
                        }}
                        style={{
                            width: "100%",
                            padding: "8px 12px",
                            borderRadius: "8px",
                            border: "1px solid var(--line)",
                            fontSize: "13px",
                        }}
                    />
                </div>

                <div
                    style={{
                        display: "flex",
                        alignItems: "flex-end",
                        gap: "8px",
                    }}
                >
                    <button
                        type="button"
                        onClick={resetFilters}
                        style={{
                            padding: "8px 16px",
                            borderRadius: "8px",
                            border: "1px solid var(--line)",
                            background: "var(--soft)",
                            fontWeight: 600,
                            cursor: "pointer",
                            fontSize: "13px",
                            height: "38px",
                        }}
                    >
                        Đặt lại
                    </button>
                    <button
                        type="button"
                        onClick={() => refetch()}
                        style={{
                            padding: "8px 16px",
                            borderRadius: "8px",
                            border: "none",
                            background: "var(--ink)",
                            color: "#fff",
                            fontWeight: 600,
                            cursor: "pointer",
                            fontSize: "13px",
                            height: "38px",
                        }}
                    >
                        Làm mới
                    </button>
                </div>
            </div>

            {/* Content states */}
            {isLoading && (
                <div
                    style={{
                        padding: "40px",
                        textAlign: "center",
                        color: "var(--muted)",
                    }}
                >
                    <div
                        style={{ display: "inline-block", marginBottom: "8px" }}
                    >
                        ⏳
                    </div>
                    <p>Đang tải nhật ký kiểm toán...</p>
                </div>
            )}

            {isError && (
                <div
                    style={{
                        padding: "16px 20px",
                        borderRadius: "12px",
                        background: "#fef2f2",
                        border: "1px solid #fecaca",
                        color: "#991b1b",
                        marginBottom: "20px",
                    }}
                >
                    <strong>⚠️ Lỗi tải dữ liệu kiểm toán</strong>
                    <p style={{ marginTop: "4px", fontSize: "13px" }}>
                        {(error as Error)?.message ||
                            "Không thể kết nối đến máy chủ thanh toán."}
                    </p>
                    <button
                        type="button"
                        onClick={() => refetch()}
                        style={{
                            marginTop: "8px",
                            padding: "6px 12px",
                            borderRadius: "6px",
                            background: "#991b1b",
                            color: "#fff",
                            border: "none",
                            cursor: "pointer",
                            fontSize: "12px",
                            fontWeight: 600,
                        }}
                    >
                        Thử lại
                    </button>
                </div>
            )}

            {!isLoading && !isError && data && (
                <>
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: "12px",
                            border: "1px solid var(--line)",
                            overflow: "hidden",
                            boxShadow: "0 1px 3px rgba(0,0,0,0.02)",
                        }}
                    >
                        <table
                            style={{
                                width: "100%",
                                borderCollapse: "collapse",
                                fontSize: "13.5px",
                            }}
                        >
                            <thead>
                                <tr
                                    style={{
                                        background: "var(--soft)",
                                        borderBottom: "1px solid var(--line)",
                                        textAlign: "left",
                                    }}
                                >
                                    <th
                                        style={{
                                            padding: "12px 16px",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Thời gian
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 16px",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Hành động
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 16px",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Dịch vụ
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 16px",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Người thực hiện
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 16px",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Đối tượng
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 16px",
                                            fontWeight: 700,
                                        }}
                                    >
                                        Lý do / Nội dung
                                    </th>
                                    <th
                                        style={{
                                            padding: "12px 16px",
                                            fontWeight: 700,
                                            textAlign: "right",
                                        }}
                                    >
                                        Thao tác
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {data.items.length === 0 ? (
                                    <tr>
                                        <td
                                            colSpan={7}
                                            style={{
                                                padding: "40px 16px",
                                                textAlign: "center",
                                                color: "var(--muted)",
                                            }}
                                        >
                                            Không tìm thấy bản ghi kiểm toán nào
                                            khớp bộ lọc.
                                        </td>
                                    </tr>
                                ) : (
                                    data.items.map((item) => {
                                        const badge = getActionBadgeStyle(
                                            item.actionType,
                                        );
                                        return (
                                            <tr
                                                key={item.id}
                                                style={{
                                                    borderBottom:
                                                        "1px solid var(--line)",
                                                    transition:
                                                        "background 0.15s",
                                                }}
                                            >
                                                <td
                                                    style={{
                                                        padding: "12px 16px",
                                                        whiteSpace: "nowrap",
                                                    }}
                                                >
                                                    {formatDate(
                                                        item.occurredAtUtc,
                                                    )}
                                                </td>
                                                <td
                                                    style={{
                                                        padding: "12px 16px",
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            display:
                                                                "inline-block",
                                                            padding: "3px 8px",
                                                            borderRadius: "6px",
                                                            fontSize: "12px",
                                                            fontWeight: 700,
                                                            background:
                                                                badge.bg,
                                                            color: badge.color,
                                                            border: `1px solid ${badge.border}`,
                                                        }}
                                                    >
                                                        {item.actionType}
                                                    </span>
                                                </td>
                                                <td
                                                    style={{
                                                        padding: "12px 16px",
                                                        color: "var(--muted)",
                                                    }}
                                                >
                                                    {item.sourceService}
                                                </td>
                                                <td
                                                    style={{
                                                        padding: "12px 16px",
                                                    }}
                                                >
                                                    <div>
                                                        <strong>
                                                            {item.actorEmail ||
                                                                "System / Anonymous"}
                                                        </strong>
                                                        <div
                                                            style={{
                                                                fontSize:
                                                                    "11px",
                                                                color: "var(--muted)",
                                                            }}
                                                        >
                                                            Role:{" "}
                                                            {item.actorRole ||
                                                                "—"}
                                                        </div>
                                                    </div>
                                                </td>
                                                <td
                                                    style={{
                                                        padding: "12px 16px",
                                                    }}
                                                >
                                                    <div>
                                                        <span>
                                                            {item.targetType}
                                                        </span>
                                                        <div
                                                            style={{
                                                                fontSize:
                                                                    "11px",
                                                                fontFamily:
                                                                    "var(--font-mono)",
                                                                color: "var(--muted)",
                                                            }}
                                                        >
                                                            {item.targetId
                                                                .length > 12
                                                                ? `${item.targetId.slice(0, 10)}...`
                                                                : item.targetId}
                                                        </div>
                                                    </div>
                                                </td>
                                                <td
                                                    style={{
                                                        padding: "12px 16px",
                                                        maxWidth: "240px",
                                                        overflow: "hidden",
                                                        textOverflow:
                                                            "ellipsis",
                                                        whiteSpace: "nowrap",
                                                    }}
                                                >
                                                    {item.reason || "—"}
                                                </td>
                                                <td
                                                    style={{
                                                        padding: "12px 16px",
                                                        textAlign: "right",
                                                    }}
                                                >
                                                    <button
                                                        type="button"
                                                        onClick={() =>
                                                            setSelectedLog(item)
                                                        }
                                                        style={{
                                                            padding: "4px 10px",
                                                            borderRadius: "6px",
                                                            border: "1px solid var(--line)",
                                                            background: "#fff",
                                                            fontSize: "12px",
                                                            fontWeight: 600,
                                                            cursor: "pointer",
                                                        }}
                                                    >
                                                        Chi tiết
                                                    </button>
                                                </td>
                                            </tr>
                                        );
                                    })
                                )}
                            </tbody>
                        </table>

                        {/* Pagination footer */}
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                padding: "12px 20px",
                                background: "var(--soft)",
                                borderTop: "1px solid var(--line)",
                                fontSize: "13px",
                            }}
                        >
                            <span>
                                Hiển thị <strong>{data.items.length}</strong> /{" "}
                                <strong>{data.totalItems}</strong> sự kiện
                                (Trang {data.page} /{" "}
                                {Math.max(1, data.totalPages)})
                            </span>
                            <div style={{ display: "flex", gap: "8px" }}>
                                <button
                                    type="button"
                                    disabled={data.page <= 1}
                                    onClick={() =>
                                        setPage((p) => Math.max(1, p - 1))
                                    }
                                    style={{
                                        padding: "6px 12px",
                                        borderRadius: "6px",
                                        border: "1px solid var(--line)",
                                        background:
                                            data.page <= 1
                                                ? "var(--soft)"
                                                : "#fff",
                                        cursor:
                                            data.page <= 1
                                                ? "not-allowed"
                                                : "pointer",
                                        fontWeight: 600,
                                    }}
                                >
                                    Trang trước
                                </button>
                                <button
                                    type="button"
                                    disabled={data.page >= data.totalPages}
                                    onClick={() => setPage((p) => p + 1)}
                                    style={{
                                        padding: "6px 12px",
                                        borderRadius: "6px",
                                        border: "1px solid var(--line)",
                                        background:
                                            data.page >= data.totalPages
                                                ? "var(--soft)"
                                                : "#fff",
                                        cursor:
                                            data.page >= data.totalPages
                                                ? "not-allowed"
                                                : "pointer",
                                        fontWeight: 600,
                                    }}
                                >
                                    Trang sau
                                </button>
                            </div>
                        </div>
                    </div>
                </>
            )}

            {/* Details Modal */}
            {selectedLog && (
                <div
                    style={{
                        position: "fixed",
                        inset: 0,
                        backgroundColor: "rgba(0, 0, 0, 0.4)",
                        display: "grid",
                        placeItems: "center",
                        zIndex: 100,
                        padding: "20px",
                    }}
                    onClick={() => setSelectedLog(null)}
                >
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: "16px",
                            maxWidth: "600px",
                            width: "100%",
                            maxHeight: "85vh",
                            overflow: "hidden",
                            display: "flex",
                            flexDirection: "column",
                            boxShadow: "0 20px 25px -5px rgba(0, 0, 0, 0.1)",
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <header
                            style={{
                                padding: "16px 20px",
                                borderBottom: "1px solid var(--line)",
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                            }}
                        >
                            <h2 style={{ fontSize: "16px", fontWeight: 700 }}>
                                Chi tiết bản ghi kiểm toán #
                                {selectedLog.id.slice(0, 8)}
                            </h2>
                            <button
                                type="button"
                                onClick={() => setSelectedLog(null)}
                                style={{
                                    border: "none",
                                    background: "transparent",
                                    fontSize: "18px",
                                    cursor: "pointer",
                                }}
                            >
                                ✕
                            </button>
                        </header>

                        <div
                            style={{
                                padding: "20px",
                                overflowY: "auto",
                                display: "grid",
                                gap: "12px",
                                fontSize: "13px",
                            }}
                        >
                            <div>
                                <span
                                    style={{
                                        color: "var(--muted)",
                                        display: "block",
                                        fontSize: "12px",
                                    }}
                                >
                                    Hành động:
                                </span>
                                <strong>{selectedLog.actionType}</strong> (
                                {selectedLog.sourceService})
                            </div>
                            <div>
                                <span
                                    style={{
                                        color: "var(--muted)",
                                        display: "block",
                                        fontSize: "12px",
                                    }}
                                >
                                    Thời gian:
                                </span>
                                <strong>
                                    {formatDate(selectedLog.occurredAtUtc)}
                                </strong>{" "}
                                (<code>{selectedLog.occurredAtUtc}</code>)
                            </div>
                            <div>
                                <span
                                    style={{
                                        color: "var(--muted)",
                                        display: "block",
                                        fontSize: "12px",
                                    }}
                                >
                                    Người thực hiện:
                                </span>
                                <strong>{selectedLog.actorEmail || "—"}</strong>{" "}
                                | Role: {selectedLog.actorRole || "—"}
                                <div
                                    style={{
                                        fontSize: "11px",
                                        fontFamily: "var(--font-mono)",
                                        color: "var(--muted)",
                                    }}
                                >
                                    Actor ID: {selectedLog.actorId || "—"}
                                </div>
                            </div>
                            <div>
                                <span
                                    style={{
                                        color: "var(--muted)",
                                        display: "block",
                                        fontSize: "12px",
                                    }}
                                >
                                    Đối tượng:
                                </span>
                                <strong>{selectedLog.targetType}</strong>:{" "}
                                <code>{selectedLog.targetId}</code>
                            </div>
                            {selectedLog.parkingLotId && (
                                <div>
                                    <span
                                        style={{
                                            color: "var(--muted)",
                                            display: "block",
                                            fontSize: "12px",
                                        }}
                                    >
                                        Mã bãi đỗ:
                                    </span>
                                    <code>{selectedLog.parkingLotId}</code>
                                </div>
                            )}
                            <div>
                                <span
                                    style={{
                                        color: "var(--muted)",
                                        display: "block",
                                        fontSize: "12px",
                                    }}
                                >
                                    Lý do / Mô tả:
                                </span>
                                <p
                                    style={{
                                        background: "var(--soft)",
                                        padding: "8px 12px",
                                        borderRadius: "8px",
                                        margin: "4px 0",
                                    }}
                                >
                                    {selectedLog.reason || "Không có mô tả"}
                                </p>
                            </div>
                            {selectedLog.ipAddress && (
                                <div>
                                    <span
                                        style={{
                                            color: "var(--muted)",
                                            display: "block",
                                            fontSize: "12px",
                                        }}
                                    >
                                        Địa chỉ IP:
                                    </span>
                                    <code>{selectedLog.ipAddress}</code>
                                </div>
                            )}
                            <div>
                                <span
                                    style={{
                                        color: "var(--muted)",
                                        display: "block",
                                        fontSize: "12px",
                                        marginBottom: "4px",
                                    }}
                                >
                                    Dữ liệu JSON chi tiết (Payload Details):
                                </span>
                                <pre
                                    style={{
                                        background: "var(--soft)",
                                        padding: "12px",
                                        borderRadius: "8px",
                                        fontSize: "12px",
                                        fontFamily: "var(--font-mono)",
                                        maxHeight: "200px",
                                        overflowY: "auto",
                                        whiteSpace: "pre-wrap",
                                        wordBreak: "break-all",
                                    }}
                                >
                                    {JSON.stringify(
                                        selectedLog.details,
                                        null,
                                        2,
                                    ) || "{}"}
                                </pre>
                            </div>
                        </div>

                        <footer
                            style={{
                                padding: "12px 20px",
                                borderTop: "1px solid var(--line)",
                                display: "flex",
                                justifyContent: "flex-end",
                            }}
                        >
                            <button
                                type="button"
                                onClick={() => setSelectedLog(null)}
                                style={{
                                    padding: "8px 16px",
                                    borderRadius: "8px",
                                    border: "none",
                                    background: "var(--ink)",
                                    color: "#fff",
                                    fontWeight: 600,
                                    cursor: "pointer",
                                }}
                            >
                                Đóng
                            </button>
                        </footer>
                    </div>
                </div>
            )}
        </div>
    );
}
