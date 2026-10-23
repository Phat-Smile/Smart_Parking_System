"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { AdminIcon, StatusPill, formatDate } from "./AdminUi";
import {
    approveOwnerApplication,
    describeOwnerAppError,
    getOwnerApplications,
    rejectOwnerApplication,
} from "./ownerApplicationsApi";
import type { ApprovalStatus, OwnerApplicationItem } from "./types";

type Filter = ApprovalStatus | "All";

export function AdminOwnerApplicationsPage() {
    const [status, setStatus] = useState<Filter>("Pending");
    const [applications, setApplications] = useState<OwnerApplicationItem[]>(
        [],
    );
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");
    const [search, setSearch] = useState("");

    // Modal state for approve / reject
    const [activeApp, setActiveApp] = useState<OwnerApplicationItem | null>(
        null,
    );
    const [modalAction, setModalAction] = useState<"approve" | "reject" | null>(
        null,
    );
    const [rejectReason, setRejectReason] = useState("");
    const [actionBusy, setActionBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    const loadData = useCallback(async () => {
        setLoading(true);
        setErrorMsg("");
        try {
            const res = await getOwnerApplications({ status });
            setApplications(res.items);
        } catch (err) {
            setErrorMsg(describeOwnerAppError(err));
        } finally {
            setLoading(false);
        }
    }, [status]);

    useEffect(() => {
        let active = true;
        getOwnerApplications({ status })
            .then((res) => {
                if (active) {
                    setApplications(res.items);
                    setLoading(false);
                }
            })
            .catch((err) => {
                if (active) {
                    setErrorMsg(describeOwnerAppError(err));
                    setLoading(false);
                }
            });
        return () => {
            active = false;
        };
    }, [status]);

    useEffect(() => {
        if (!toast) return;
        const timer = setTimeout(() => setToast(null), 4000);
        return () => clearTimeout(timer);
    }, [toast]);

    const filtered = useMemo(() => {
        const term = search.trim().toLowerCase();
        return applications.filter((app) => {
            if (!term) return true;
            return (
                (app.fullName && app.fullName.toLowerCase().includes(term)) ||
                (app.email && app.email.toLowerCase().includes(term)) ||
                (app.phoneNumber &&
                    app.phoneNumber.toLowerCase().includes(term)) ||
                (app.businessName &&
                    app.businessName.toLowerCase().includes(term)) ||
                (app.taxCode && app.taxCode.toLowerCase().includes(term))
            );
        });
    }, [applications, search]);

    const counts = useMemo(() => {
        return {
            All: applications.length,
            Pending: applications.filter((a) => a.status === "Pending").length,
            Approved: applications.filter((a) => a.status === "Approved")
                .length,
            Rejected: applications.filter((a) => a.status === "Rejected")
                .length,
        };
    }, [applications]);

    async function handleConfirmApprove() {
        if (!activeApp) return;
        setActionBusy(true);
        try {
            await approveOwnerApplication(activeApp.accountId);
            setToast(
                `Đã duyệt hồ sơ chủ bãi cho ${activeApp.fullName || activeApp.email}.`,
            );
            setModalAction(null);
            setActiveApp(null);
            await loadData();
        } catch (err) {
            setErrorMsg(describeOwnerAppError(err));
        } finally {
            setActionBusy(false);
        }
    }

    async function handleConfirmReject() {
        if (!activeApp || !rejectReason.trim()) return;
        setActionBusy(true);
        try {
            await rejectOwnerApplication(
                activeApp.accountId,
                rejectReason.trim(),
            );
            setToast(
                `Đã từ chối hồ sơ của ${activeApp.fullName || activeApp.email}.`,
            );
            setModalAction(null);
            setActiveApp(null);
            setRejectReason("");
            await loadData();
        } catch (err) {
            setErrorMsg(describeOwnerAppError(err));
        } finally {
            setActionBusy(false);
        }
    }

    const tabs: Filter[] = ["Pending", "All", "Approved", "Rejected"];

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">Xác thực đối tác</p>
                    <h1>Duyệt hồ sơ Chủ bãi</h1>
                </div>
                <div style={{ display: "flex", gap: "8px" }}>
                    <Link
                        href="/admin/parking-lots/pending"
                        className="admin-btn admin-btn-ghost"
                    >
                        ← Bãi xe chờ duyệt
                    </Link>
                    <button
                        type="button"
                        onClick={loadData}
                        className="admin-btn admin-btn-ghost"
                        disabled={loading}
                    >
                        🔄 Làm mới
                    </button>
                </div>
            </header>

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
                <div className="admin-tabs" role="tablist">
                    {tabs.map((tab) => (
                        <button
                            key={tab}
                            type="button"
                            role="tab"
                            id={`owner-app-tab-${tab.toLowerCase()}`}
                            aria-selected={status === tab}
                            onClick={() => setStatus(tab)}
                        >
                            {tab === "Pending"
                                ? "Chờ duyệt"
                                : tab === "Approved"
                                  ? "Đã duyệt"
                                  : tab === "Rejected"
                                    ? "Từ chối"
                                    : "Tất cả"}
                            <span>{counts[tab]}</span>
                        </button>
                    ))}
                </div>

                <label className="admin-search">
                    <AdminIcon name="search" size={16} />
                    <input
                        id="owner-apps-search"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        placeholder="Tìm tên, email, MST, SĐT hoặc doanh nghiệp..."
                    />
                </label>
            </div>

            <section className="admin-card admin-table-card">
                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>Họ và tên / Tài khoản</th>
                            <th>Doanh nghiệp / MST</th>
                            <th>Số điện thoại</th>
                            <th>Ngày nộp</th>
                            <th>Trạng thái</th>
                            <th style={{ textAlign: "right" }}>Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.map((app) => (
                            <tr key={app.accountId}>
                                <td>
                                    <strong>
                                        {app.fullName || "Chưa cập nhật"}
                                    </strong>
                                    <small>{app.email}</small>
                                </td>
                                <td>
                                    <strong>{app.businessName || "—"}</strong>
                                    <small>MST: {app.taxCode || "—"}</small>
                                </td>
                                <td>{app.phoneNumber || "—"}</td>
                                <td>{formatDate(app.createdAtUtc)}</td>
                                <td>
                                    <StatusPill status={app.status} />
                                </td>
                                <td
                                    className="admin-table-action"
                                    style={{ textAlign: "right" }}
                                >
                                    {app.status === "Pending" ? (
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
                                                    setActiveApp(app);
                                                    setModalAction("approve");
                                                }}
                                            >
                                                Duyệt
                                            </button>
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-danger-ghost"
                                                onClick={() => {
                                                    setActiveApp(app);
                                                    setModalAction("reject");
                                                }}
                                            >
                                                Từ chối
                                            </button>
                                        </div>
                                    ) : (
                                        <small
                                            style={{
                                                color: "var(--staff-text-muted)",
                                            }}
                                        >
                                            {app.status === "Approved"
                                                ? "Đã nâng cấp quyền"
                                                : "Đã từ chối"}
                                        </small>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>

                {filtered.length === 0 && !loading && (
                    <p className="admin-empty">
                        {status === "Pending"
                            ? "Không có hồ sơ chủ bãi nào đang chờ duyệt."
                            : "Không tìm thấy hồ sơ phù hợp với bộ lọc."}
                    </p>
                )}

                {loading && (
                    <p className="admin-empty">
                        Đang tải danh sách hồ sơ chủ bãi...
                    </p>
                )}
            </section>

            {/* Modal Approve / Reject */}
            {modalAction && activeApp && (
                <div
                    className="admin-modal-backdrop"
                    onClick={() => !actionBusy && setModalAction(null)}
                >
                    <div
                        className="admin-modal"
                        role="dialog"
                        aria-modal="true"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <span className={`admin-modal-icon ${modalAction}`}>
                            <AdminIcon
                                name={
                                    modalAction === "approve"
                                        ? "check"
                                        : "close"
                                }
                                size={22}
                            />
                        </span>
                        <h2>
                            {modalAction === "approve"
                                ? "Phê duyệt hồ sơ Chủ bãi?"
                                : "Từ chối hồ sơ Chủ bãi?"}
                        </h2>
                        <p>
                            {modalAction === "approve"
                                ? `Tài khoản ${activeApp.fullName || activeApp.email} sẽ được cấp vai trò ParkingLotOwner và có thể đăng ký, quản lý bãi đỗ xe.`
                                : `Vui lòng nhập lý do từ chối để thông báo cho đối tác ${activeApp.fullName || activeApp.email}.`}
                        </p>

                        {modalAction === "reject" && (
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
                                    placeholder="Ví dụ: Mã số thuế không hợp lệ hoặc thiếu giấy phép kinh doanh."
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
                                onClick={() => setModalAction(null)}
                                disabled={actionBusy}
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                className={`admin-btn ${modalAction === "approve" ? "admin-btn-lime" : "admin-btn-danger"}`}
                                onClick={
                                    modalAction === "approve"
                                        ? handleConfirmApprove
                                        : handleConfirmReject
                                }
                                disabled={
                                    actionBusy ||
                                    (modalAction === "reject" &&
                                        !rejectReason.trim())
                                }
                            >
                                {actionBusy
                                    ? "Đang lưu..."
                                    : modalAction === "approve"
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
