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
import type {
    ApprovalStatus,
    OwnerApplicationDocument,
    OwnerApplicationItem,
} from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

type Filter = ApprovalStatus | "All";

interface VerificationChecklist {
    license: boolean;
    idCard: boolean;
    fireSafety: boolean;
    lease: boolean;
}

const DEFAULT_CHECKLIST: VerificationChecklist = {
    license: false,
    idCard: false,
    fireSafety: false,
    lease: false,
};

export function AdminOwnerApplicationsPage() {
    const { t } = useLanguage();
    const [status, setStatus] = useState<Filter>("Pending");
    const [applications, setApplications] = useState<OwnerApplicationItem[]>([]);
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");
    const [search, setSearch] = useState("");

    // Modal state for approve / reject / inspect
    const [activeApp, setActiveApp] = useState<OwnerApplicationItem | null>(null);
    const [modalAction, setModalAction] = useState<"approve" | "reject" | "inspect" | null>(null);
    const [rejectReason, setRejectReason] = useState("");
    const [actionBusy, setActionBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    // Document inspection and checklist
    const [checklist, setChecklist] = useState<VerificationChecklist>(DEFAULT_CHECKLIST);
    const [previewDoc, setPreviewDoc] = useState<OwnerApplicationDocument | null>(null);

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
                (app.phoneNumber && app.phoneNumber.toLowerCase().includes(term)) ||
                (app.businessName && app.businessName.toLowerCase().includes(term)) ||
                (app.taxCode && app.taxCode.toLowerCase().includes(term))
            );
        });
    }, [applications, search]);

    const counts = useMemo(() => {
        return {
            All: applications.length,
            Pending: applications.filter((a) => a.status === "Pending").length,
            Approved: applications.filter((a) => a.status === "Approved").length,
            Rejected: applications.filter((a) => a.status === "Rejected").length,
        };
    }, [applications]);

    const isAllVerified =
        checklist.license &&
        checklist.idCard &&
        checklist.fireSafety &&
        checklist.lease;

    function openReviewModal(app: OwnerApplicationItem, action: "approve" | "inspect" | "reject") {
        setActiveApp(app);
        setModalAction(action);
        setChecklist(DEFAULT_CHECKLIST);
        setRejectReason("");
    }

    async function handleConfirmApprove() {
        if (!activeApp) return;
        if (!isAllVerified) {
            setErrorMsg(t("ownerAppsMissingDocsAlert"));
            return;
        }

        setActionBusy(true);
        try {
            await approveOwnerApplication(activeApp.accountId);
            setToast(`${t("approved")}: ${activeApp.fullName || activeApp.email}`);
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
            await rejectOwnerApplication(activeApp.accountId, rejectReason.trim());
            setToast(`${t("rejected")}: ${activeApp.fullName || activeApp.email}`);
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

    function handleRejectMissingDocs() {
        if (!activeApp) return;
        const missing: string[] = [];
        if (!checklist.license) missing.push(t("ownerAppsChecklistRule1"));
        if (!checklist.idCard) missing.push(t("ownerAppsChecklistRule2"));
        if (!checklist.fireSafety) missing.push(t("ownerAppsChecklistRule3"));
        if (!checklist.lease) missing.push(t("ownerAppsChecklistRule4"));

        const generatedReason = missing.length > 0
            ? `${t("ownerAppsRejectReasonPlaceholder")}: (${missing.join(", ")}).`
            : t("ownerAppsChecklistWarning");

        setRejectReason(generatedReason);
        setModalAction("reject");
    }

    const tabs: Filter[] = ["Pending", "All", "Approved", "Rejected"];

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">{t("ownerAppsEyebrow")}</p>
                    <h1>{t("ownerAppsTitle")}</h1>
                </div>
                <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                    <Link
                        href="/admin/parking-lots/assignments"
                        className="admin-btn admin-btn-ghost"
                    >
                        👥 {t("assignmentsTitle")}
                    </Link>
                    <Link
                        href="/admin/parking-lots/pending"
                        className="admin-btn admin-btn-ghost"
                    >
                        {t("parkingLotsPendingTab")}
                    </Link>
                    <button
                        type="button"
                        onClick={loadData}
                        className="admin-btn admin-btn-ghost"
                        disabled={loading}
                    >
                        🔄 {t("refresh")}
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
                                ? t("pending")
                                : tab === "Approved"
                                  ? t("approved")
                                  : tab === "Rejected"
                                    ? t("rejected")
                                    : t("all")}
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
                        placeholder={t("ownerAppsSearchPlaceholder")}
                    />
                </label>
            </div>

            <section className="admin-card admin-table-card">
                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>{t("ownerAppsApplicant")}</th>
                            <th>{t("ownerAppsBusiness")}</th>
                            <th>{t("ownerAppsLegalDocs")}</th>
                            <th>{t("ownerAppsSubmittedAt")}</th>
                            <th>{t("status")}</th>
                            <th style={{ textAlign: "right" }}>{t("actions")}</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.map((app) => (
                            <tr key={app.accountId}>
                                <td>
                                    <strong>
                                        {app.fullName || t("ownerAppsNotUpdated")}
                                    </strong>
                                    <small>{app.email}</small>
                                </td>
                                <td>
                                    <strong>{app.businessName || "—"}</strong>
                                    <small>{t("ownerAppsTaxCode")}: {app.taxCode || "—"}</small>
                                </td>
                                <td>
                                    <button
                                        type="button"
                                        className="admin-btn admin-btn-xs admin-btn-ghost"
                                        style={{ fontSize: "12px", padding: "4px 8px" }}
                                        onClick={() => openReviewModal(app, "inspect")}
                                    >
                                        📑 {t("ownerAppsInspectDocs")} ({app.documents?.length || 0})
                                    </button>
                                </td>
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
                                                title={t("ownerAppsVerifyAndApprove")}
                                                onClick={() => openReviewModal(app, "approve")}
                                            >
                                                {t("ownerAppsVerifyAndApprove")}
                                            </button>
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-danger-ghost"
                                                onClick={() => openReviewModal(app, "reject")}
                                            >
                                                {t("ownerAppsReject")}
                                            </button>
                                        </div>
                                    ) : (
                                        <button
                                            type="button"
                                            className="admin-btn admin-btn-xs admin-btn-ghost"
                                            onClick={() => openReviewModal(app, "inspect")}
                                        >
                                            {t("details")}
                                        </button>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>

                {filtered.length === 0 && !loading && (
                    <p className="admin-empty">
                        {status === "Pending"
                            ? t("ownerAppsPendingEmpty")
                            : t("ownerAppsFilterEmpty")}
                    </p>
                )}

                {loading && (
                    <p className="admin-empty">
                        {t("ownerAppsLoading")}
                    </p>
                )}
            </section>

            {/* Modal Thẩm định giấy tờ & Duyệt hồ sơ */}
            {(modalAction === "approve" || modalAction === "inspect") && activeApp && (
                <div
                    className="admin-modal-backdrop"
                    onClick={() => !actionBusy && setModalAction(null)}
                >
                    <div
                        className="admin-modal"
                        role="dialog"
                        aria-modal="true"
                        style={{ maxWidth: "720px", width: "95%", textAlign: "left" }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "1rem" }}>
                            <div>
                                <h2 style={{ margin: 0, fontSize: "1.25rem" }}>
                                    {modalAction === "approve"
                                        ? t("ownerAppsApproveModalTitle")
                                        : t("ownerAppsInspectDocsModalTitle")}
                                </h2>
                                <p style={{ margin: "4px 0 0", color: "var(--staff-text-muted, #94a3b8)", fontSize: "0.875rem" }}>
                                    {activeApp.businessName || activeApp.fullName} ({activeApp.email})
                                </p>
                            </div>
                            <button
                                type="button"
                                onClick={() => setModalAction(null)}
                                style={{ background: "none", border: "none", color: "inherit", fontSize: "1.25rem", cursor: "pointer" }}
                            >
                                ✕
                            </button>
                        </div>

                        {/* Thông tin đối tác */}
                        <div
                            style={{
                                display: "grid",
                                gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
                                gap: "10px",
                                padding: "14px",
                                borderRadius: "8px",
                                background: "#f9fafb",
                                border: "1px solid #e5e7eb",
                                marginBottom: "1rem",
                                fontSize: "0.875rem",
                                color: "#111827",
                            }}
                        >
                            <div>
                                <span style={{ color: "#4b5563" }}>{t("ownerAppsRepresentative")} </span>
                                <strong>{activeApp.fullName || "—"}</strong>
                            </div>
                            <div>
                                <span style={{ color: "#4b5563" }}>{t("ownerAppsTaxCode")}: </span>
                                <strong>{activeApp.taxCode || "—"}</strong>
                            </div>
                            <div>
                                <span style={{ color: "#4b5563" }}>{t("ownerAppsIdCardNumber")} </span>
                                <strong>{activeApp.idCardNumber || "—"}</strong>
                            </div>
                            <div>
                                <span style={{ color: "#4b5563" }}>{t("phone")}: </span>
                                <strong>{activeApp.phoneNumber || "—"}</strong>
                            </div>
                            <div style={{ gridColumn: "1 / -1" }}>
                                <span style={{ color: "#4b5563" }}>{t("ownerAppsRegisteredAddress")} </span>
                                <span>{activeApp.businessAddress || t("ownerAppsNotUpdated")}</span>
                            </div>
                        </div>

                        {/* Danh sách tài liệu đính kèm */}
                        <h3 style={{ fontSize: "1rem", margin: "1rem 0 0.5rem" }}>
                            {t("ownerAppsAttachedDocs")} ({activeApp.documents?.length || 0})
                        </h3>
                        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "10px", marginBottom: "1.5rem" }}>
                            {(activeApp.documents && activeApp.documents.length > 0) ? (
                                activeApp.documents.map((doc) => (
                                    <div
                                        key={doc.id}
                                        style={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: "10px",
                                            padding: "10px",
                                            borderRadius: "6px",
                                            background: "#f9fafb",
                                            border: "1px solid #e5e7eb",
                                            color: "#111827",
                                        }}
                                    >
                                        <div
                                            style={{
                                                width: "48px",
                                                height: "48px",
                                                borderRadius: "4px",
                                                overflow: "hidden",
                                                background: "#111",
                                                flexShrink: 0,
                                                cursor: "pointer",
                                            }}
                                            onClick={() => setPreviewDoc(doc)}
                                        >
                                            {/* eslint-disable-next-line @next/next/no-img-element */}
                                            <img
                                                src={doc.fileUrl}
                                                alt={doc.name}
                                                style={{ width: "100%", height: "100%", objectFit: "cover" }}
                                            />
                                        </div>
                                        <div style={{ overflow: "hidden", flex: 1 }}>
                                            <p style={{ margin: 0, fontSize: "0.85rem", fontWeight: 600, textOverflow: "ellipsis", overflow: "hidden", whiteSpace: "nowrap" }}>
                                                {doc.name}
                                            </p>
                                            <small style={{ color: "var(--staff-text-muted, #94a3b8)", fontSize: "0.75rem" }}>
                                                {formatDate(doc.uploadedAtUtc)}
                                            </small>
                                        </div>
                                        <button
                                            type="button"
                                            className="admin-btn admin-btn-xs admin-btn-ghost"
                                            onClick={() => setPreviewDoc(doc)}
                                            title={t("view")}
                                        >
                                            👁 {t("view")}
                                        </button>
                                    </div>
                                ))
                            ) : (
                                <p style={{ color: "#ef4444", fontSize: "0.875rem", gridColumn: "1 / -1" }}>
                                    {t("ownerAppsNoDocsWarning")}
                                </p>
                            )}
                        </div>

                        {/* Checklist thẩm định */}
                        {modalAction === "approve" && (
                            <div
                                style={{
                                    padding: "14px",
                                    borderRadius: "8px",
                                    background: isAllVerified ? "rgba(34, 197, 94, 0.08)" : "rgba(234, 179, 8, 0.08)",
                                    border: `1px solid ${isAllVerified ? "rgba(34, 197, 94, 0.3)" : "rgba(234, 179, 8, 0.3)"}`,
                                    marginBottom: "1.25rem",
                                }}
                            >
                                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
                                    <strong style={{ fontSize: "0.9rem", color: isAllVerified ? "#4ade80" : "#facc15" }}>
                                        {t("ownerAppsChecklistTitle")}
                                    </strong>
                                    <button
                                        type="button"
                                        className="admin-btn admin-btn-xs admin-btn-ghost"
                                        onClick={() =>
                                            setChecklist({
                                                license: !isAllVerified,
                                                idCard: !isAllVerified,
                                                fireSafety: !isAllVerified,
                                                lease: !isAllVerified,
                                            })
                                        }
                                    >
                                        {isAllVerified ? t("ownerAppsDeselectAllChecklist") : t("ownerAppsSelectAllChecklist")}
                                    </button>
                                </div>

                                <div style={{ display: "grid", gap: "8px", fontSize: "0.85rem" }}>
                                    <label style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer" }}>
                                        <input
                                            type="checkbox"
                                            checked={checklist.license}
                                            onChange={(e) => setChecklist((prev) => ({ ...prev, license: e.target.checked }))}
                                        />
                                        <span>{t("ownerAppsChecklistRule1")}</span>
                                    </label>
                                    <label style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer" }}>
                                        <input
                                            type="checkbox"
                                            checked={checklist.idCard}
                                            onChange={(e) => setChecklist((prev) => ({ ...prev, idCard: e.target.checked }))}
                                        />
                                        <span>{t("ownerAppsChecklistRule2")}</span>
                                    </label>
                                    <label style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer" }}>
                                        <input
                                            type="checkbox"
                                            checked={checklist.fireSafety}
                                            onChange={(e) => setChecklist((prev) => ({ ...prev, fireSafety: e.target.checked }))}
                                        />
                                        <span>{t("ownerAppsChecklistRule3")}</span>
                                    </label>
                                    <label style={{ display: "flex", alignItems: "center", gap: "8px", cursor: "pointer" }}>
                                        <input
                                            type="checkbox"
                                            checked={checklist.lease}
                                            onChange={(e) => setChecklist((prev) => ({ ...prev, lease: e.target.checked }))}
                                        />
                                        <span>{t("ownerAppsChecklistRule4")}</span>
                                    </label>
                                </div>

                                {!isAllVerified && (
                                    <p style={{ margin: "8px 0 0", color: "#facc15", fontSize: "0.8rem" }}>
                                        {t("ownerAppsChecklistWarning")}
                                    </p>
                                )}
                            </div>
                        )}

                        {/* Modal actions */}
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "10px", marginTop: "1rem" }}>
                            {modalAction === "approve" ? (
                                <>
                                    <button
                                        type="button"
                                        className="admin-btn admin-btn-danger-ghost"
                                        onClick={handleRejectMissingDocs}
                                        disabled={actionBusy}
                                    >
                                        {t("ownerAppsRejectMissingDocsBtn")}
                                    </button>
                                    <div style={{ display: "flex", gap: "8px" }}>
                                        <button
                                            type="button"
                                            className="admin-btn admin-btn-ghost"
                                            onClick={() => setModalAction(null)}
                                            disabled={actionBusy}
                                        >
                                            {t("cancel")}
                                        </button>
                                        <button
                                            type="button"
                                            className="admin-btn admin-btn-lime"
                                            onClick={handleConfirmApprove}
                                            disabled={actionBusy || !isAllVerified}
                                            title={isAllVerified ? t("ownerAppsConfirmApprove") : t("ownerAppsChecklistWarning")}
                                        >
                                            {actionBusy ? t("saving") : t("ownerAppsConfirmApprove")}
                                        </button>
                                    </div>
                                </>
                            ) : (
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-ghost"
                                    style={{ marginLeft: "auto" }}
                                    onClick={() => setModalAction(null)}
                                >
                                    {t("close")}
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* Modal Từ chối hồ sơ */}
            {modalAction === "reject" && activeApp && (
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
                        <span className="admin-modal-icon reject">
                            <AdminIcon name="close" size={22} />
                        </span>
                        <h2>{t("ownerAppsRejectConfirmTitle")}</h2>
                        <p>
                            {t("ownerAppsRejectConfirmDesc")} {activeApp.fullName || activeApp.email}.
                        </p>

                        <label className="admin-field" style={{ marginTop: "1rem" }}>
                            <span>{t("ownerAppsRejectReasonLabel")}</span>
                            <textarea
                                rows={4}
                                autoFocus
                                value={rejectReason}
                                onChange={(e) => setRejectReason(e.target.value)}
                                placeholder={t("ownerAppsRejectReasonPlaceholder")}
                                style={{
                                    width: "100%",
                                    padding: "10px 12px",
                                    borderRadius: "8px",
                                    background: "#ffffff",
                                    color: "#111827",
                                    border: "1px solid #d1d5db",
                                    fontSize: "0.875rem",
                                }}
                            />
                        </label>

                        <div className="admin-modal-actions" style={{ marginTop: "1.5rem" }}>
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost"
                                onClick={() => setModalAction(null)}
                                disabled={actionBusy}
                            >
                                {t("cancel")}
                            </button>
                            <button
                                type="button"
                                className="admin-btn admin-btn-danger"
                                onClick={handleConfirmReject}
                                disabled={actionBusy || !rejectReason.trim()}
                            >
                                {actionBusy ? t("saving") : t("ownerAppsConfirmRejectBtn")}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Lightbox / Preview tài liệu */}
            {previewDoc && (
                <div
                    className="admin-modal-backdrop"
                    style={{ zIndex: 9999 }}
                    onClick={() => setPreviewDoc(null)}
                >
                    <div
                        className="admin-modal"
                        style={{ maxWidth: "800px", width: "95%", textAlign: "center", padding: "16px" }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "12px" }}>
                            <strong style={{ fontSize: "1rem" }}>{previewDoc.name}</strong>
                            <button
                                type="button"
                                onClick={() => setPreviewDoc(null)}
                                style={{ background: "none", border: "none", color: "inherit", fontSize: "1.25rem", cursor: "pointer" }}
                            >
                                ✕
                            </button>
                        </div>
                        <div style={{ maxHeight: "70vh", overflow: "auto", borderRadius: "8px" }}>
                            {/* eslint-disable-next-line @next/next/no-img-element */}
                            <img
                                src={previewDoc.fileUrl}
                                alt={previewDoc.name}
                                style={{ maxWidth: "100%", maxHeight: "65vh", objectFit: "contain", borderRadius: "6px" }}
                            />
                        </div>
                        <div style={{ marginTop: "12px", textAlign: "right" }}>
                            <a
                                href={previewDoc.fileUrl}
                                target="_blank"
                                rel="noreferrer"
                                className="admin-btn admin-btn-sm admin-btn-ghost"
                            >
                                {t("ownerAppsOpenNewTab")}
                            </a>
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

