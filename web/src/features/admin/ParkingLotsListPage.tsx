"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AdminIcon, StatusPill, formatDate } from "./AdminUi";
import {
    approveParkingLot,
    describeReviewError,
    getAdminParkingLots,
    isParkingLotId,
    rejectParkingLot,
} from "./reviewParkingLot";
import type { AdminParkingLot, ApprovalStatus } from "./types";

type Filter = ApprovalStatus | "All";

/**
 * Used for both /admin/parking-lots (management, all statuses) and
 * /admin/parking-lots/pending (queue, Pending locked).
 */
export function ParkingLotsListPage({
    mode,
    initialStatus = "All",
}: {
    mode: "all" | "pending";
    initialStatus?: Filter;
}) {
    const router = useRouter();
    const [status, setStatus] = useState<Filter>(
        mode === "pending" ? "Pending" : initialStatus,
    );
    const [lots, setLots] = useState<AdminParkingLot[]>([]);
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");
    const [search, setSearch] = useState("");
    const [lookupId, setLookupId] = useState("");
    const [lookupError, setLookupError] = useState("");

    // Quick review modal
    const [selectedLot, setSelectedLot] = useState<AdminParkingLot | null>(
        null,
    );
    const [reviewAction, setReviewAction] = useState<
        "approve" | "reject" | null
    >(null);
    const [rejectReason, setRejectReason] = useState("");
    const [actionBusy, setActionBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    const loadLots = useCallback(async () => {
        setLoading(true);
        setErrorMsg("");
        try {
            const data = await getAdminParkingLots(
                mode === "pending" ? "Pending" : status,
            );
            setLots(data);
        } catch (err) {
            setErrorMsg(describeReviewError(err));
        } finally {
            setLoading(false);
        }
    }, [mode, status]);

    useEffect(() => {
        let active = true;
        getAdminParkingLots(mode === "pending" ? "Pending" : status)
            .then((data) => {
                if (active) {
                    setLots(data);
                    setLoading(false);
                }
            })
            .catch((err) => {
                if (active) {
                    setErrorMsg(describeReviewError(err));
                    setLoading(false);
                }
            });
        return () => {
            active = false;
        };
    }, [mode, status]);

    useEffect(() => {
        if (!toast) return;
        const timer = setTimeout(() => setToast(null), 4000);
        return () => clearTimeout(timer);
    }, [toast]);

    const counts = useMemo(() => {
        return {
            All: lots.length,
            Pending: lots.filter((l) => l.status === "Pending").length,
            Approved: lots.filter((l) => l.status === "Approved").length,
            Rejected: lots.filter((l) => l.status === "Rejected").length,
        };
    }, [lots]);

    const rows = useMemo(() => {
        const term = search.trim().toLowerCase();
        return lots
            .filter((lot) => status === "All" || lot.status === status)
            .filter(
                (lot) =>
                    !term ||
                    [lot.name, lot.ownerName, lot.ownerEmail, lot.address].some(
                        (v) => v.toLowerCase().includes(term),
                    ),
            )
            .sort((a, b) => a.submittedAtUtc.localeCompare(b.submittedAtUtc));
    }, [lots, search, status]);

    function openById(event: React.FormEvent) {
        event.preventDefault();
        if (!isParkingLotId(lookupId)) {
            setLookupError("Enter a valid parking lot UUID.");
            return;
        }
        router.push(`/admin/parking-lots/${lookupId.trim()}`);
    }

    async function handleQuickApprove() {
        if (!selectedLot) return;
        setActionBusy(true);
        try {
            await approveParkingLot(selectedLot.id);
            setToast(`Đã duyệt bãi đỗ xe ${selectedLot.name}.`);
            setSelectedLot(null);
            setReviewAction(null);
            await loadLots();
        } catch (err) {
            setErrorMsg(describeReviewError(err));
        } finally {
            setActionBusy(false);
        }
    }

    async function handleQuickReject() {
        if (!selectedLot || !rejectReason.trim()) return;
        setActionBusy(true);
        try {
            await rejectParkingLot(selectedLot.id, rejectReason.trim());
            setToast(`Đã từ chối bãi đỗ xe ${selectedLot.name}.`);
            setSelectedLot(null);
            setReviewAction(null);
            setRejectReason("");
            await loadLots();
        } catch (err) {
            setErrorMsg(describeReviewError(err));
        } finally {
            setActionBusy(false);
        }
    }

    const tabs: Filter[] = ["All", "Pending", "Approved", "Rejected"];

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">
                        {mode === "pending"
                            ? "Hàng đợi xét duyệt"
                            : "Quản lý hệ thống"}
                    </p>
                    <h1>
                        {mode === "pending"
                            ? "Bãi đỗ xe chờ duyệt"
                            : "Danh sách bãi đỗ xe"}
                    </h1>
                </div>
                <div style={{ display: "flex", gap: "8px" }}>
                    <button
                        type="button"
                        onClick={loadLots}
                        className="admin-btn admin-btn-ghost"
                        disabled={loading}
                    >
                        🔄 Làm mới
                    </button>
                </div>
            </header>

            {/* Navigation tab bar between Parking Lots, Owner Applications, and Assignments */}
            <div style={{ display: "flex", gap: "10px", marginBottom: "1rem", flexWrap: "wrap" }}>
                <Link
                    href="/admin/parking-lots/pending"
                    className={`admin-btn ${mode === "pending" ? "admin-btn-dark" : "admin-btn-ghost"}`}
                >
                    🅿️ Bãi xe chờ duyệt
                </Link>
                <Link
                    href="/admin/owner-applications"
                    className="admin-btn admin-btn-ghost"
                >
                    👤 Duyệt hồ sơ Chủ bãi
                </Link>
                <Link
                    href="/admin/parking-lots/assignments"
                    className="admin-btn admin-btn-ghost"
                >
                    👥 Phân công nhân viên
                </Link>
            </div>

            {errorMsg && (
                <div
                    className="admin-banner"
                    role="alert"
                    style={{
                        background: "rgba(239, 68, 68, 0.15)",
                        border: "1px solid #ef4444",
                        color: "#f87171",
                    }}
                >
                    <AdminIcon name="alert" size={16} />
                    <span>{errorMsg}</span>
                    <button
                        type="button"
                        onClick={() => setErrorMsg("")}
                        style={{
                            marginLeft: "auto",
                            background: "none",
                            border: "none",
                            color: "inherit",
                            cursor: "pointer",
                        }}
                    >
                        ✕
                    </button>
                </div>
            )}

            <div className="admin-toolbar">
                {mode === "all" && (
                    <div className="admin-tabs" role="tablist">
                        {tabs.map((tab) => (
                            <button
                                key={tab}
                                type="button"
                                role="tab"
                                id={`lots-tab-${tab.toLowerCase()}`}
                                aria-selected={status === tab}
                                onClick={() => setStatus(tab)}
                            >
                                {tab === "Approved" ? "Active" : tab}
                                <span>{counts[tab]}</span>
                            </button>
                        ))}
                    </div>
                )}
                <label className="admin-search">
                    <AdminIcon name="search" size={16} />
                    <input
                        id="lots-search"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        placeholder="Tìm tên bãi, chủ bãi, email hoặc địa chỉ..."
                    />
                </label>
            </div>

            <section className="admin-card admin-table-card">
                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>Bãi đỗ xe</th>
                            <th>Chủ bãi</th>
                            <th>Ngày nộp</th>
                            <th>Sức chứa</th>
                            <th>Trạng thái</th>
                            <th
                                aria-label="Actions"
                                style={{ textAlign: "right" }}
                            >
                                Thao tác
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {rows.map((lot) => (
                            <tr key={lot.id}>
                                <td>
                                    <strong>{lot.name}</strong>
                                    <small>{lot.address}</small>
                                </td>
                                <td>
                                    {lot.ownerName}
                                    <small>{lot.ownerEmail}</small>
                                </td>
                                <td>{formatDate(lot.submittedAtUtc)}</td>
                                <td>{lot.totalCapacity} chỗ</td>
                                <td>
                                    <StatusPill status={lot.status} />
                                </td>
                                <td
                                    className="admin-table-action"
                                    style={{ textAlign: "right" }}
                                >
                                    {lot.status === "Pending" ? (
                                        <div
                                            style={{
                                                display: "inline-flex",
                                                gap: "6px",
                                            }}
                                        >
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-lime"
                                                onClick={() => {
                                                    setSelectedLot(lot);
                                                    setReviewAction("approve");
                                                }}
                                            >
                                                Duyệt
                                            </button>
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-danger-ghost"
                                                onClick={() => {
                                                    setSelectedLot(lot);
                                                    setReviewAction("reject");
                                                }}
                                            >
                                                Từ chối
                                            </button>
                                            <Link
                                                href={`/admin/parking-lots/${lot.id}`}
                                                className="admin-btn admin-btn-sm admin-btn-ghost"
                                            >
                                                Chi tiết
                                            </Link>
                                        </div>
                                    ) : (
                                        <div style={{ display: "inline-flex", gap: "6px" }}>
                                            {lot.status === "Approved" && (
                                                <Link
                                                    href="/admin/parking-lots/assignments"
                                                    className="admin-btn admin-btn-sm admin-btn-ghost"
                                                    title="Phân công nhân viên trực bãi"
                                                >
                                                    👥 Phân công
                                                </Link>
                                            )}
                                            <Link
                                                href={`/admin/parking-lots/${lot.id}`}
                                                className="admin-btn admin-btn-sm admin-btn-ghost"
                                            >
                                                Xem
                                            </Link>
                                        </div>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {rows.length === 0 && !loading && (
                    <p className="admin-empty">
                        {mode === "pending"
                            ? "Không có bãi đỗ xe nào đang chờ duyệt."
                            : "Không tìm thấy bãi đỗ xe nào phù hợp với bộ lọc."}
                    </p>
                )}
                {loading && (
                    <p className="admin-empty">Đang tải dữ liệu bãi đỗ xe...</p>
                )}
            </section>

            <form className="admin-lookup" onSubmit={openById}>
                <div>
                    <strong>Tra cứu bãi đỗ xe theo ID</strong>
                    <small>
                        Dán mã UUID của bãi đỗ xe từ đơn đăng ký để trực tiếp
                        xét duyệt.
                    </small>
                </div>
                <input
                    id="lots-lookup-id"
                    value={lookupId}
                    onChange={(e) => {
                        setLookupId(e.target.value);
                        setLookupError("");
                    }}
                    placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                    aria-invalid={Boolean(lookupError)}
                />
                <button
                    type="submit"
                    className="admin-btn admin-btn-dark"
                    id="lots-lookup-open"
                >
                    Mở
                </button>
                {lookupError && (
                    <p className="admin-field-error" role="alert">
                        {lookupError}
                    </p>
                )}
            </form>

            {/* Modal Approve / Reject */}
            {reviewAction && selectedLot && (
                <div
                    className="admin-modal-backdrop"
                    onClick={() => !actionBusy && setReviewAction(null)}
                >
                    <div
                        className="admin-modal"
                        role="dialog"
                        aria-modal="true"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <span className={`admin-modal-icon ${reviewAction}`}>
                            <AdminIcon
                                name={
                                    reviewAction === "approve"
                                        ? "check"
                                        : "close"
                                }
                                size={22}
                            />
                        </span>
                        <h2>
                            {reviewAction === "approve"
                                ? `Duyệt bãi đỗ xe ${selectedLot.name}?`
                                : `Từ chối bãi đỗ xe ${selectedLot.name}?`}
                        </h2>
                        <p>
                            {reviewAction === "approve"
                                ? `Bãi đỗ xe sẽ được chuyển sang trạng thái Hoạt động (Approved) và xuất hiện trên danh mục công khai cho khách hàng.`
                                : `Vui lòng nhập lý do từ chối để chủ bãi có thể điều chỉnh lại hồ sơ.`}
                        </p>

                        {reviewAction === "reject" && (
                            <label
                                className="admin-field"
                                style={{ marginTop: "1rem" }}
                            >
                                <span>Lý do từ chối:</span>
                                <textarea
                                    rows={4}
                                    autoFocus
                                    value={rejectReason}
                                    onChange={(e) =>
                                        setRejectReason(e.target.value)
                                    }
                                    placeholder="Ví dụ: Chưa cung cấp đủ giấy tờ PCCC hoặc sơ đồ bãi xe không hợp lệ."
                                    style={{
                                        width: "100%",
                                        padding: "8px",
                                        borderRadius: "6px",
                                        background:
                                            "var(--admin-bg-subtle, #1a1e24)",
                                        color: "inherit",
                                        border: "1px solid rgba(255,255,255,0.15)",
                                    }}
                                />
                            </label>
                        )}

                        <div
                            className="admin-modal-actions"
                            style={{ marginTop: "1.5rem" }}
                        >
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost"
                                onClick={() => setReviewAction(null)}
                                disabled={actionBusy}
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                className={`admin-btn ${reviewAction === "approve" ? "admin-btn-lime" : "admin-btn-danger"}`}
                                onClick={
                                    reviewAction === "approve"
                                        ? handleQuickApprove
                                        : handleQuickReject
                                }
                                disabled={
                                    actionBusy ||
                                    (reviewAction === "reject" &&
                                        !rejectReason.trim())
                                }
                            >
                                {actionBusy
                                    ? "Đang lưu..."
                                    : reviewAction === "approve"
                                      ? "Xác nhận duyệt"
                                      : "Xác nhận từ chối"}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {toast && (
                <div className="admin-toast" role="status">
                    {toast}
                </div>
            )}
        </>
    );
}
