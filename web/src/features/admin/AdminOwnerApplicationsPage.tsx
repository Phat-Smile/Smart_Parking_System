"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { AdminIcon, StatusPill, formatDate } from "./AdminUi";
import {
    approveOwnerApplication,
    describeOwnerAppError,
    downloadOwnerApplicationDocument,
    getOwnerApplicationDocuments,
    getOwnerApplications,
    rejectOwnerApplication,
} from "./ownerApplicationsApi";
import type { OwnerApplicationDocumentsResponse } from "./ownerApplicationsApi";
import type { ApprovalStatus, OwnerApplicationItem } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { findProfileByCccd } from "@/features/simulator/vneid/vneidSeedData";

type Filter = ApprovalStatus | "All";
type SortOrder = "default" | "newest" | "oldest" | "name";
type ReviewDocument = OwnerApplicationDocumentsResponse["documents"][number];
type DocumentPreview = { name: string; url: string; isImage: boolean };

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

function formatOwnerSubmittedDate(
    iso: string | null | undefined,
    isVi: boolean,
): string {
    if (!iso) return "—";
    const dt = new Date(iso);
    if (Number.isNaN(dt.getTime())) return "—";
    if (isVi) {
        const day = String(dt.getDate()).padStart(2, "0");
        const month = String(dt.getMonth() + 1).padStart(2, "0");
        const year = dt.getFullYear();
        const hours = String(dt.getHours()).padStart(2, "0");
        const minutes = String(dt.getMinutes()).padStart(2, "0");
        return `${day}/${month}/${year} · ${hours}:${minutes}`;
    }
    return formatDate(iso);
}

export function AdminOwnerApplicationsPage() {
    const { t, language } = useLanguage();
    const isVi = language === "vi";
    const [status, setStatus] = useState<Filter>("All");
    const [applications, setApplications] = useState<OwnerApplicationItem[]>(
        [],
    );
    const [loading, setLoading] = useState(false);
    const [rawError, setRawError] = useState<unknown>(null);
    const [search, setSearch] = useState("");
    const [sortOrder, setSortOrder] = useState<SortOrder>("default");

    const errorMsg = useMemo(() => {
        if (!rawError) return "";
        if (rawError === "missing_docs") return t("ownerAppsMissingDocsAlert");
        if (typeof rawError === "string") return rawError;
        return describeOwnerAppError(rawError, language);
    }, [rawError, language, t]);

    // Modal state for approve / reject / inspect
    const [activeApp, setActiveApp] = useState<OwnerApplicationItem | null>(
        null,
    );
    const [modalAction, setModalAction] = useState<
        "approve" | "reject" | "inspect" | null
    >(null);
    const [rejectReason, setRejectReason] = useState("");
    const [actionBusy, setActionBusy] = useState(false);
    const [toast, setToast] = useState<string | null>(null);

    // Document inspection and checklist
    const [checklist, setChecklist] =
        useState<VerificationChecklist>(DEFAULT_CHECKLIST);
    // Documents come from Identity when an application is opened; a file is fetched with the admin token.
    const [docs, setDocs] = useState<ReviewDocument[]>([]);
    const [missingDocTypes, setMissingDocTypes] = useState<string[]>([]);
    const [docsLoading, setDocsLoading] = useState(false);
    const [previewDoc, setPreviewDoc] = useState<DocumentPreview | null>(null);

    async function loadDocuments(accountId: string) {
        setDocs([]);
        setMissingDocTypes([]);
        setDocsLoading(true);
        try {
            const res = await getOwnerApplicationDocuments(accountId);
            setDocs(res.documents);
            setMissingDocTypes(res.missingDocumentTypes);
        } catch (err) {
            setRawError(err);
        } finally {
            setDocsLoading(false);
        }
    }

    function closePreview() {
        setPreviewDoc((prev) => {
            if (prev) URL.revokeObjectURL(prev.url);
            return null;
        });
    }

    async function openDocument(doc: ReviewDocument) {
        if (!activeApp) return;
        try {
            const blob = await downloadOwnerApplicationDocument(
                activeApp.accountId,
                doc.documentType,
            );
            closePreview();
            setPreviewDoc({
                name: doc.originalFileName,
                url: URL.createObjectURL(blob),
                isImage: blob.type.startsWith("image/"),
            });
        } catch (err) {
            setRawError(err);
        }
    }

    const fetchAllApplications = useCallback(async () => {
        const PAGE_LIMIT = 100;
        const firstPage = await getOwnerApplications({
            status: "All",
            page: 1,
            pageSize: PAGE_LIMIT,
        });
        const allItems = [...firstPage.items];
        const totalCount = firstPage.totalCount ?? allItems.length;
        const totalPages = Math.ceil(totalCount / PAGE_LIMIT);

        for (let p = 2; p <= totalPages; p++) {
            const nextPage = await getOwnerApplications({
                status: "All",
                page: p,
                pageSize: PAGE_LIMIT,
            });
            if (!nextPage.items || nextPage.items.length === 0) break;
            allItems.push(...nextPage.items);
        }
        return allItems;
    }, []);

    const loadData = useCallback(async () => {
        setLoading(true);
        setRawError(null);
        try {
            const items = await fetchAllApplications();
            setApplications(items);
        } catch (err) {
            setRawError(err);
        } finally {
            setLoading(false);
        }
    }, [fetchAllApplications]);

    function handleStatusTabChange(nextStatus: Filter) {
        if (nextStatus === status) return;
        setRawError(null);
        setStatus(nextStatus);
    }

    useEffect(() => {
        let active = true;
        setLoading(true);
        fetchAllApplications()
            .then((items) => {
                if (active) {
                    setApplications(items);
                    setLoading(false);
                }
            })
            .catch((err) => {
                if (active) {
                    setRawError(err);
                    setLoading(false);
                }
            });
        return () => {
            active = false;
        };
    }, [fetchAllApplications]);

    useEffect(() => {
        if (!toast) return;
        const timer = setTimeout(() => setToast(null), 4000);
        return () => clearTimeout(timer);
    }, [toast]);

    useEffect(() => {
        if (!modalAction && !previewDoc) return;
        const onKeyDown = (e: KeyboardEvent) => {
            if (e.key === "Escape" && !actionBusy) {
                if (previewDoc) {
                    closePreview();
                } else {
                    setModalAction(null);
                }
            }
        };
        window.addEventListener("keydown", onKeyDown);
        return () => window.removeEventListener("keydown", onKeyDown);
    }, [modalAction, previewDoc, actionBusy]);

    const hasActiveFilters = Boolean(
        search.trim() !== "" || sortOrder !== "default",
    );

    function handleClearFilters() {
        setSearch("");
        setSortOrder("default");
    }

    const filtered = useMemo(() => {
        const term = search.trim().toLowerCase();
        const matching = applications.filter((app) => {
            if (status !== "All" && app.status !== status) return false;
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

        if (sortOrder === "default") return matching;

        return [...matching].sort((a, b) => {
            if (sortOrder === "name") {
                const nameA = a.fullName || a.email || "";
                const nameB = b.fullName || b.email || "";
                return nameA.localeCompare(nameB, isVi ? "vi" : "en");
            }
            const dateA = a.createdAtUtc ?? "";
            const dateB = b.createdAtUtc ?? "";
            if (sortOrder === "newest") {
                return dateB.localeCompare(dateA);
            }
            return dateA.localeCompare(dateB);
        });
    }, [applications, status, search, sortOrder, isVi]);

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

    const verifiedCount = useMemo(() => {
        return [
            checklist.license,
            checklist.idCard,
            checklist.fireSafety,
            checklist.lease,
        ].filter(Boolean).length;
    }, [checklist]);

    const isAllVerified =
        checklist.license &&
        checklist.idCard &&
        checklist.fireSafety &&
        checklist.lease;

    function openReviewModal(
        app: OwnerApplicationItem,
        action: "approve" | "inspect" | "reject",
    ) {
        setActiveApp(app);
        setModalAction(action);
        const hasVneidMatch = Boolean(
            app.idCardNumber && findProfileByCccd(app.idCardNumber),
        );
        setChecklist({
            ...DEFAULT_CHECKLIST,
            idCard: hasVneidMatch,
        });
        setRejectReason("");
        void loadDocuments(app.accountId);
    }

    async function handleConfirmApprove() {
        if (!activeApp) return;
        if (!isAllVerified) {
            setRawError("missing_docs");
            return;
        }

        setActionBusy(true);
        try {
            await approveOwnerApplication(activeApp.accountId);
            setToast(
                `${t("approved")}: ${activeApp.fullName || activeApp.email}`,
            );
            setModalAction(null);
            setActiveApp(null);
            await loadData();
        } catch (err) {
            setRawError(err);
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
                `${t("rejected")}: ${activeApp.fullName || activeApp.email}`,
            );
            setModalAction(null);
            setActiveApp(null);
            setRejectReason("");
            await loadData();
        } catch (err) {
            setRawError(err);
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

        const generatedReason =
            missing.length > 0
                ? `${t("ownerAppsRejectReasonPlaceholder")}: (${missing.join(", ")}).`
                : t("ownerAppsChecklistWarning");

        setRejectReason(generatedReason);
        setModalAction("reject");
    }

    const tabs: Filter[] = ["All", "Pending", "Approved", "Rejected"];

    return (
        <div className="admin-owner-apps-page">
            {/* 1. Page Heading */}
            <header className="admin-heading admin-dash-heading">
                <div className="admin-dash-heading-left">
                    <p className="admin-eyebrow">{t("ownerAppsEyebrow")}</p>
                    <h1>{t("ownerAppsTitle")}</h1>
                </div>
                <div className="admin-dash-heading-actions">
                    <button
                        type="button"
                        onClick={loadData}
                        className="admin-btn admin-btn-ghost"
                        disabled={loading}
                        title={
                            isVi
                                ? "Tải lại dữ liệu hồ sơ từ hệ thống"
                                : "Reload applications from server"
                        }
                    >
                        <AdminIcon
                            name="refresh"
                            size={15}
                            className={loading ? "admin-spin" : undefined}
                        />
                        <span>
                            {loading
                                ? isVi
                                    ? "Đang tải..."
                                    : "Refreshing..."
                                : t("refresh")}
                        </span>
                    </button>
                </div>
            </header>

            {/* 3. Error Banner */}
            {errorMsg && (
                <div
                    className="admin-error-box admin-owner-error-banner"
                    role="alert"
                >
                    <div className="admin-error-box-content">
                        <span className="admin-error-box-icon">
                            <AdminIcon name="alert" size={18} />
                        </span>
                        <div>
                            <strong>
                                {isVi
                                    ? "Thông báo xử lý hồ sơ"
                                    : "Application Notice"}
                            </strong>
                            <p>{errorMsg}</p>
                        </div>
                    </div>
                    <div className="admin-owner-error-actions">
                        <button
                            type="button"
                            className="admin-btn admin-btn-xs admin-btn-ghost"
                            onClick={loadData}
                            disabled={loading}
                        >
                            <AdminIcon name="refresh" size={13} />
                            <span>{isVi ? "Thử lại" : "Retry"}</span>
                        </button>
                        <button
                            type="button"
                            className="admin-owner-error-dismiss"
                            onClick={() => setRawError(null)}
                            aria-label={t("close")}
                        >
                            ✕
                        </button>
                    </div>
                </div>
            )}

            {/* 2. Unified Smartbase Data Grid Shell */}
            <section
                className="admin-card admin-datagrid-card admin-table-card admin-owner-table-card"
                aria-label={
                    isVi
                        ? "Bảng dữ liệu và thanh công cụ duyệt hồ sơ chủ bãi"
                        : "Owner applications data grid and toolbar"
                }
            >
                <div className="admin-datagrid-status-bar">
                    <div
                        className="admin-tabs admin-status-tabs admin-datagrid-view-tabs admin-owner-status-tabs"
                        role="tablist"
                    >
                        {tabs.map((tab) => {
                            const tabKey = tab.toLowerCase();
                            const isSelected = status === tab;
                            return (
                                <button
                                    key={tab}
                                    type="button"
                                    role="tab"
                                    id={`owner-app-tab-${tabKey}`}
                                    className={`admin-status-tab tab-${tabKey} ${isSelected ? "is-selected" : ""}`}
                                    aria-selected={isSelected}
                                    onClick={() => handleStatusTabChange(tab)}
                                >
                                    {tab !== "All" && (
                                        <span
                                            className={`admin-tab-dot dot-${tabKey}`}
                                            aria-hidden="true"
                                        />
                                    )}
                                    <span>
                                        {tab === "Pending"
                                            ? t("pending")
                                            : tab === "Approved"
                                              ? t("approved")
                                              : tab === "Rejected"
                                                ? t("rejected")
                                                : t("all")}
                                    </span>
                                    <span
                                        className={`admin-tab-badge badge-${tabKey}`}
                                    >
                                        {counts[tab]}
                                    </span>
                                </button>
                            );
                        })}
                    </div>

                    <span className="admin-datagrid-result-count admin-owner-result-badge">
                        {isVi ? (
                            <>
                                Đang hiển thị <strong>{filtered.length}</strong>{" "}
                                hồ sơ
                            </>
                        ) : (
                            <>
                                Showing <strong>{filtered.length}</strong>{" "}
                                applications
                            </>
                        )}
                    </span>
                </div>

                <div className="admin-datagrid-toolbar-row1">
                    <label
                        className="admin-search admin-toolbar-search admin-datagrid-search admin-owner-search"
                        htmlFor="owner-apps-search"
                    >
                        <AdminIcon name="search" size={15} />
                        <input
                            id="owner-apps-search"
                            type="search"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                            placeholder={t("ownerAppsSearchPlaceholder")}
                        />
                        {search && (
                            <button
                                type="button"
                                className="admin-search-clear"
                                onClick={() => setSearch("")}
                                aria-label={
                                    isVi ? "Xóa từ khóa" : "Clear search"
                                }
                            >
                                <AdminIcon name="close" size={13} />
                            </button>
                        )}
                    </label>

                    <div className="admin-toolbar-filters admin-owner-toolbar-filters">
                        <div className="admin-filter-group admin-owner-sort-group">
                            <AdminIcon name="sort" size={14} />
                            <select
                                className="admin-select admin-toolbar-select"
                                value={sortOrder}
                                onChange={(e) =>
                                    setSortOrder(e.target.value as SortOrder)
                                }
                                aria-label={
                                    isVi ? "Sắp xếp hồ sơ" : "Sort applications"
                                }
                            >
                                <option value="default">
                                    {isVi
                                        ? "Mặc định hệ thống"
                                        : "System default"}
                                </option>
                                <option value="newest">
                                    {isVi
                                        ? "Ngày nộp: Mới nhất"
                                        : "Submitted: Newest"}
                                </option>
                                <option value="oldest">
                                    {isVi
                                        ? "Ngày nộp: Cũ nhất"
                                        : "Submitted: Oldest"}
                                </option>
                                <option value="name">
                                    {isVi
                                        ? "Tên chủ bãi (A → Z)"
                                        : "Applicant Name (A → Z)"}
                                </option>
                            </select>
                        </div>
                    </div>
                </div>

                {hasActiveFilters && (
                    <div className="admin-toolbar-summary admin-owner-toolbar-summary">
                        <span>
                            {isVi ? (
                                <>
                                    Đang hiển thị{" "}
                                    <strong>{filtered.length}</strong> /{" "}
                                    <strong>{applications.length}</strong> hồ sơ
                                    khớp điều kiện lọc
                                </>
                            ) : (
                                <>
                                    Showing <strong>{filtered.length}</strong>{" "}
                                    of <strong>{applications.length}</strong>{" "}
                                    matching applications
                                </>
                            )}
                        </span>
                    </div>
                )}
                <div className="admin-table-scroll">
                    <table className="admin-table admin-owner-table">
                        <thead>
                            <tr>
                                <th>{t("ownerAppsApplicant")}</th>
                                <th>{t("ownerAppsBusiness")}</th>
                                <th>{t("ownerAppsLegalDocs")}</th>
                                <th>{t("ownerAppsSubmittedAt")}</th>
                                <th>{t("status")}</th>
                                <th style={{ textAlign: "right" }}>
                                    {t("actions")}
                                </th>
                            </tr>
                        </thead>
                        <tbody>
                            {loading &&
                                applications.length === 0 &&
                                [1, 2, 3].map((idx) => (
                                    <tr
                                        key={`skeleton-${idx}`}
                                        className="admin-owner-skeleton-row"
                                        aria-hidden="true"
                                    >
                                        <td>
                                            <div className="admin-skeleton-line is-title" />
                                            <div className="admin-skeleton-line is-sub" />
                                        </td>
                                        <td>
                                            <div className="admin-skeleton-line is-title" />
                                            <div className="admin-skeleton-line is-sub" />
                                        </td>
                                        <td>
                                            <div className="admin-skeleton-pill" />
                                        </td>
                                        <td>
                                            <div className="admin-skeleton-line is-sub" />
                                        </td>
                                        <td>
                                            <div className="admin-skeleton-pill" />
                                        </td>
                                        <td style={{ textAlign: "right" }}>
                                            <div
                                                className="admin-skeleton-pill"
                                                style={{ marginLeft: "auto" }}
                                            />
                                        </td>
                                    </tr>
                                ))}

                            {filtered.map((app) => {
                                const hasBusinessName = Boolean(
                                    app.businessName && app.businessName.trim(),
                                );
                                const hasTaxCode = Boolean(
                                    app.taxCode && app.taxCode.trim(),
                                );
                                const initial = (
                                    app.fullName?.trim()?.[0] ||
                                    app.email?.trim()?.[0] ||
                                    "P"
                                ).toUpperCase();

                                return (
                                    <tr key={app.accountId}>
                                        {/* 7.1 Applicant / Account */}
                                        <td className="admin-owner-applicant-cell">
                                            <div className="admin-owner-applicant-wrap">
                                                <span
                                                    className="admin-owner-avatar"
                                                    aria-hidden="true"
                                                >
                                                    {initial}
                                                </span>
                                                <div className="admin-owner-applicant-meta">
                                                    <strong
                                                        className={
                                                            app.fullName
                                                                ? "admin-owner-name"
                                                                : "admin-cell-unupdated"
                                                        }
                                                    >
                                                        {app.fullName ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </strong>
                                                    <small
                                                        className="admin-owner-email"
                                                        title={app.email}
                                                    >
                                                        {app.email ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </small>
                                                </div>
                                            </div>
                                        </td>

                                        {/* 7.2 Business / Tax ID */}
                                        <td className="admin-owner-business-cell">
                                            <div className="admin-owner-business-stack">
                                                {hasBusinessName ? (
                                                    <strong className="admin-owner-business-name">
                                                        {app.businessName}
                                                    </strong>
                                                ) : (
                                                    <span className="admin-cell-unupdated">
                                                        {t(
                                                            "ownerAppsNotUpdated",
                                                        )}
                                                    </span>
                                                )}
                                                <small className="admin-owner-tax">
                                                    <span>
                                                        {t("ownerAppsTaxCode")}
                                                        :{" "}
                                                    </span>
                                                    {hasTaxCode ? (
                                                        <code className="admin-tax-code">
                                                            {app.taxCode}
                                                        </code>
                                                    ) : (
                                                        <span className="admin-cell-unupdated">
                                                            {t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                        </span>
                                                    )}
                                                </small>
                                            </div>
                                        </td>

                                        {/* 7.3 Legal Documents */}
                                        <td className="admin-owner-docs-cell">
                                            {Array.isArray(app.documents) &&
                                            app.documents.length === 0 ? (
                                                <span className="admin-cell-unupdated">
                                                    {isVi
                                                        ? "Chưa có tài liệu"
                                                        : "No documents"}
                                                </span>
                                            ) : (
                                                <button
                                                    type="button"
                                                    className="admin-btn admin-btn-xs admin-btn-ghost admin-owner-doc-btn"
                                                    onClick={() =>
                                                        openReviewModal(
                                                            app,
                                                            "inspect",
                                                        )
                                                    }
                                                >
                                                    <AdminIcon
                                                        name="file"
                                                        size={13}
                                                    />
                                                    <span>
                                                        {t(
                                                            "ownerAppsInspectDocs",
                                                        )}
                                                    </span>
                                                    {Array.isArray(
                                                        app.documents,
                                                    ) &&
                                                        app.documents.length >
                                                            0 && (
                                                            <b className="admin-owner-doc-count">
                                                                {
                                                                    app
                                                                        .documents
                                                                        .length
                                                                }
                                                            </b>
                                                        )}
                                                </button>
                                            )}
                                        </td>

                                        {/* 7.4 Submitted Date */}
                                        <td className="admin-lot-cell-date admin-owner-date-cell">
                                            {app.createdAtUtc ? (
                                                <span className="admin-owner-date">
                                                    {formatOwnerSubmittedDate(
                                                        app.createdAtUtc,
                                                        isVi,
                                                    )}
                                                </span>
                                            ) : (
                                                <span className="admin-cell-unupdated">
                                                    {t("ownerAppsNotUpdated")}
                                                </span>
                                            )}
                                        </td>

                                        {/* 7.5 Status */}
                                        <td className="admin-lot-cell-status admin-owner-status-cell">
                                            <StatusPill status={app.status} />
                                        </td>

                                        {/* 7.6 Actions */}
                                        <td className="admin-table-action admin-owner-action-cell">
                                            <div className="admin-row-actions admin-owner-row-actions">
                                                {app.status === "Pending" ? (
                                                    <>
                                                        <button
                                                            type="button"
                                                            className="admin-btn admin-btn-sm admin-btn-lime admin-owner-action-btn"
                                                            title={t(
                                                                "ownerAppsVerifyAndApprove",
                                                            )}
                                                            disabled={
                                                                actionBusy
                                                            }
                                                            onClick={() =>
                                                                openReviewModal(
                                                                    app,
                                                                    "approve",
                                                                )
                                                            }
                                                        >
                                                            <AdminIcon
                                                                name="check"
                                                                size={13}
                                                            />
                                                            <span>
                                                                {t(
                                                                    "ownerAppsVerifyAndApprove",
                                                                )}
                                                            </span>
                                                        </button>
                                                        <button
                                                            type="button"
                                                            className="admin-btn admin-btn-sm admin-btn-danger-ghost admin-owner-action-btn"
                                                            disabled={
                                                                actionBusy
                                                            }
                                                            onClick={() =>
                                                                openReviewModal(
                                                                    app,
                                                                    "reject",
                                                                )
                                                            }
                                                        >
                                                            <AdminIcon
                                                                name="close"
                                                                size={13}
                                                            />
                                                            <span>
                                                                {t(
                                                                    "ownerAppsReject",
                                                                )}
                                                            </span>
                                                        </button>
                                                    </>
                                                ) : (
                                                    <button
                                                        type="button"
                                                        className="admin-btn admin-btn-sm admin-btn-ghost admin-owner-action-btn"
                                                        onClick={() =>
                                                            openReviewModal(
                                                                app,
                                                                "inspect",
                                                            )
                                                        }
                                                    >
                                                        <AdminIcon
                                                            name="eye"
                                                            size={13}
                                                        />
                                                        <span>
                                                            {t("details")}
                                                        </span>
                                                    </button>
                                                )}
                                            </div>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>

                {/* Empty State */}
                {filtered.length === 0 && !loading && (
                    <div className="admin-empty-box admin-owner-empty-state">
                        <span className="admin-empty-icon">
                            <AdminIcon
                                name={hasActiveFilters ? "search" : "inbox"}
                                size={26}
                            />
                        </span>
                        <strong>
                            {isVi
                                ? "Không tìm thấy hồ sơ"
                                : "No applications found"}
                        </strong>
                        <p>
                            {status === "Pending" && !hasActiveFilters
                                ? t("ownerAppsPendingEmpty")
                                : t("ownerAppsFilterEmpty")}
                        </p>
                        {hasActiveFilters && (
                            <button
                                type="button"
                                className="admin-btn admin-btn-sm admin-btn-ghost"
                                onClick={handleClearFilters}
                            >
                                <AdminIcon name="close" size={14} />
                                <span>
                                    {isVi ? "Xóa bộ lọc" : "Clear filters"}
                                </span>
                            </button>
                        )}
                    </div>
                )}

                {loading && (
                    <p className="admin-empty admin-owner-loading-note">
                        {t("ownerAppsLoading")}
                    </p>
                )}
            </section>

            {/* 6. Modal Thẩm định giấy tờ & Duyệt hồ sơ */}
            {(modalAction === "approve" || modalAction === "inspect") &&
                activeApp && (
                    <div
                        className="admin-modal-backdrop"
                        onClick={() => !actionBusy && setModalAction(null)}
                    >
                        <div
                            className="admin-modal admin-owner-review-modal"
                            role="dialog"
                            aria-modal="true"
                            aria-labelledby="owner-review-modal-title"
                            onClick={(e) => e.stopPropagation()}
                        >
                            {/* Modal Header */}
                            <header className="admin-owner-modal-header">
                                <div className="admin-owner-modal-title-group">
                                    <div className="admin-owner-modal-title-row">
                                        <h2 id="owner-review-modal-title">
                                            {modalAction === "approve"
                                                ? t(
                                                      "ownerAppsApproveModalTitle",
                                                  )
                                                : t(
                                                      "ownerAppsInspectDocsModalTitle",
                                                  )}
                                        </h2>
                                        <StatusPill status={activeApp.status} />
                                    </div>
                                    <p className="admin-owner-modal-subtitle">
                                        {activeApp.businessName ||
                                            activeApp.fullName ||
                                            t("ownerAppsNotUpdated")}{" "}
                                        · {activeApp.email}
                                    </p>
                                </div>
                                <button
                                    type="button"
                                    className="admin-owner-modal-close"
                                    onClick={() => setModalAction(null)}
                                    disabled={actionBusy}
                                    aria-label={t("close")}
                                >
                                    ✕
                                </button>
                            </header>

                            {/* Modal Scrollable Body */}
                            <div className="admin-owner-modal-body">
                                {/* Section 1 & 2: Applicant & Business Information */}
                                <div className="admin-owner-info-sections">
                                    {/* Section 1: Applicant Info */}
                                    <section className="admin-owner-info-card">
                                        <h3 className="admin-owner-section-heading">
                                            <AdminIcon name="users" size={15} />
                                            <span>
                                                {isVi
                                                    ? "Thông tin chủ bãi"
                                                    : "Applicant Information"}
                                            </span>
                                        </h3>
                                        <dl className="admin-owner-info-list">
                                            <div className="admin-owner-info-item">
                                                <dt>
                                                    {t(
                                                        "ownerAppsRepresentative",
                                                    )}
                                                </dt>
                                                <dd>
                                                    <strong>
                                                        {activeApp.fullName ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </strong>
                                                </dd>
                                            </div>
                                            <div className="admin-owner-info-item">
                                                <dt>Email:</dt>
                                                <dd>
                                                    <span>
                                                        {activeApp.email ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </span>
                                                </dd>
                                            </div>
                                            <div className="admin-owner-info-item">
                                                <dt>{t("phone")}:</dt>
                                                <dd>
                                                    <strong>
                                                        {activeApp.phoneNumber ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </strong>
                                                </dd>
                                            </div>
                                            <div className="admin-owner-info-item">
                                                <dt>
                                                    {t("ownerAppsIdCardNumber")}
                                                </dt>
                                                <dd className="admin-owner-cccd-val">
                                                    <strong>
                                                        {activeApp.idCardNumber ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </strong>
                                                    {activeApp.idCardNumber &&
                                                        findProfileByCccd(
                                                            activeApp.idCardNumber,
                                                        ) && (
                                                            <span
                                                                data-testid="admin-vneid-badge"
                                                                className="admin-vneid-badge"
                                                                title={`Trùng khớp CSDL Quốc gia VNeID: ${findProfileByCccd(activeApp.idCardNumber)?.fullName}`}
                                                            >
                                                                🇻🇳 VNeID Mức 2
                                                            </span>
                                                        )}
                                                </dd>
                                            </div>
                                        </dl>
                                    </section>

                                    {/* Section 2: Business Info */}
                                    <section className="admin-owner-info-card">
                                        <h3 className="admin-owner-section-heading">
                                            <AdminIcon
                                                name="building"
                                                size={15}
                                            />
                                            <span>
                                                {isVi
                                                    ? "Thông tin doanh nghiệp"
                                                    : "Business Information"}
                                            </span>
                                        </h3>
                                        <dl className="admin-owner-info-list">
                                            <div className="admin-owner-info-item">
                                                <dt>
                                                    {isVi
                                                        ? "Tên doanh nghiệp:"
                                                        : "Business Name:"}
                                                </dt>
                                                <dd>
                                                    <strong>
                                                        {activeApp.businessName ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </strong>
                                                </dd>
                                            </div>
                                            <div className="admin-owner-info-item">
                                                <dt>
                                                    {t("ownerAppsTaxCode")}:
                                                </dt>
                                                <dd>
                                                    {activeApp.taxCode ? (
                                                        <code className="admin-tax-code">
                                                            {activeApp.taxCode}
                                                        </code>
                                                    ) : (
                                                        <span className="admin-cell-unupdated">
                                                            {t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                        </span>
                                                    )}
                                                </dd>
                                            </div>
                                            <div className="admin-owner-info-item is-full">
                                                <dt>
                                                    {t(
                                                        "ownerAppsRegisteredAddress",
                                                    )}
                                                </dt>
                                                <dd>
                                                    <span>
                                                        {activeApp.businessAddress ||
                                                            t(
                                                                "ownerAppsNotUpdated",
                                                            )}
                                                    </span>
                                                </dd>
                                            </div>
                                        </dl>
                                    </section>
                                </div>

                                {/* Section 3: Attached Legal Documents */}
                                <section className="admin-owner-docs-section">
                                    <div className="admin-owner-docs-header">
                                        <h3 className="admin-owner-section-heading">
                                            <AdminIcon name="file" size={15} />
                                            <span>
                                                {t("ownerAppsAttachedDocs")} (
                                                {docs.length})
                                            </span>
                                        </h3>
                                    </div>

                                    <div className="admin-owner-docs-grid">
                                        {docsLoading ? (
                                            <div className="admin-owner-docs-loading">
                                                <AdminIcon
                                                    name="refresh"
                                                    size={16}
                                                />
                                                <span>{t("loading")}</span>
                                            </div>
                                        ) : docs.length > 0 ? (
                                            docs.map((doc) => (
                                                <div
                                                    key={doc.id}
                                                    className="admin-owner-doc-card"
                                                >
                                                    <span
                                                        className="admin-owner-doc-icon"
                                                        aria-hidden="true"
                                                    >
                                                        <AdminIcon
                                                            name="file"
                                                            size={18}
                                                        />
                                                    </span>
                                                    <div className="admin-owner-doc-info">
                                                        <p
                                                            className="admin-owner-doc-title"
                                                            title={`${doc.documentType} · ${doc.originalFileName}`}
                                                        >
                                                            {doc.documentType} ·{" "}
                                                            {
                                                                doc.originalFileName
                                                            }
                                                        </p>
                                                        <small className="admin-owner-doc-date">
                                                            {formatDate(
                                                                doc.uploadedAtUtc,
                                                            )}
                                                        </small>
                                                    </div>
                                                    <button
                                                        type="button"
                                                        className="admin-btn admin-btn-xs admin-btn-ghost admin-owner-doc-open"
                                                        onClick={() =>
                                                            void openDocument(
                                                                doc,
                                                            )
                                                        }
                                                        title={t("view")}
                                                    >
                                                        👁 {t("view")}
                                                    </button>
                                                </div>
                                            ))
                                        ) : (
                                            <div className="admin-owner-no-docs">
                                                <p>
                                                    {t(
                                                        "ownerAppsNoDocsWarning",
                                                    )}
                                                </p>
                                            </div>
                                        )}

                                        {missingDocTypes.length > 0 && (
                                            <div className="admin-owner-missing-docs">
                                                <AdminIcon
                                                    name="alert"
                                                    size={15}
                                                />
                                                <p>
                                                    <strong>
                                                        {isVi
                                                            ? "Thiếu giấy tờ bắt buộc: "
                                                            : "Missing required documents: "}
                                                    </strong>
                                                    {missingDocTypes.join(", ")}
                                                </p>
                                            </div>
                                        )}
                                    </div>
                                </section>

                                {/* Section 4: Verification Checklist (when Approving) */}
                                {modalAction === "approve" && (
                                    <section
                                        className={`admin-owner-checklist ${isAllVerified ? "is-verified" : "is-incomplete"}`}
                                    >
                                        <div className="admin-owner-checklist-header">
                                            <div className="admin-owner-checklist-title-wrap">
                                                <strong>
                                                    {t(
                                                        "ownerAppsChecklistTitle",
                                                    )}
                                                </strong>
                                                <span className="admin-owner-checklist-progress">
                                                    {verifiedCount}/4
                                                </span>
                                            </div>
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-xs admin-btn-ghost"
                                                onClick={() =>
                                                    setChecklist({
                                                        license: !isAllVerified,
                                                        idCard: !isAllVerified,
                                                        fireSafety:
                                                            !isAllVerified,
                                                        lease: !isAllVerified,
                                                    })
                                                }
                                            >
                                                {isAllVerified
                                                    ? t(
                                                          "ownerAppsDeselectAllChecklist",
                                                      )
                                                    : t(
                                                          "ownerAppsSelectAllChecklist",
                                                      )}
                                            </button>
                                        </div>

                                        <div className="admin-owner-checklist-items">
                                            <label className="admin-owner-check-item">
                                                <input
                                                    type="checkbox"
                                                    checked={checklist.license}
                                                    onChange={(e) =>
                                                        setChecklist(
                                                            (prev) => ({
                                                                ...prev,
                                                                license:
                                                                    e.target
                                                                        .checked,
                                                            }),
                                                        )
                                                    }
                                                />
                                                <span>
                                                    {t(
                                                        "ownerAppsChecklistRule1",
                                                    )}
                                                </span>
                                            </label>

                                            <label className="admin-owner-check-item">
                                                <input
                                                    type="checkbox"
                                                    checked={checklist.idCard}
                                                    onChange={(e) =>
                                                        setChecklist(
                                                            (prev) => ({
                                                                ...prev,
                                                                idCard: e.target
                                                                    .checked,
                                                            }),
                                                        )
                                                    }
                                                />
                                                <span>
                                                    {t(
                                                        "ownerAppsChecklistRule2",
                                                    )}
                                                </span>
                                                {activeApp.idCardNumber &&
                                                    findProfileByCccd(
                                                        activeApp.idCardNumber,
                                                    ) && (
                                                        <span className="admin-vneid-inline-chip">
                                                            ✓ Đã đối soát VNeID
                                                        </span>
                                                    )}
                                            </label>

                                            <label className="admin-owner-check-item">
                                                <input
                                                    type="checkbox"
                                                    checked={
                                                        checklist.fireSafety
                                                    }
                                                    onChange={(e) =>
                                                        setChecklist(
                                                            (prev) => ({
                                                                ...prev,
                                                                fireSafety:
                                                                    e.target
                                                                        .checked,
                                                            }),
                                                        )
                                                    }
                                                />
                                                <span>
                                                    {t(
                                                        "ownerAppsChecklistRule3",
                                                    )}
                                                </span>
                                            </label>

                                            <label className="admin-owner-check-item">
                                                <input
                                                    type="checkbox"
                                                    checked={checklist.lease}
                                                    onChange={(e) =>
                                                        setChecklist(
                                                            (prev) => ({
                                                                ...prev,
                                                                lease: e.target
                                                                    .checked,
                                                            }),
                                                        )
                                                    }
                                                />
                                                <span>
                                                    {t(
                                                        "ownerAppsChecklistRule4",
                                                    )}
                                                </span>
                                            </label>
                                        </div>

                                        {!isAllVerified && (
                                            <p className="admin-owner-checklist-warning">
                                                {t("ownerAppsChecklistWarning")}
                                            </p>
                                        )}
                                    </section>
                                )}
                            </div>

                            {/* Modal Footer */}
                            <footer className="admin-owner-modal-footer">
                                {modalAction === "approve" ? (
                                    <>
                                        <button
                                            type="button"
                                            className="admin-btn admin-btn-sm admin-btn-danger-ghost"
                                            onClick={handleRejectMissingDocs}
                                            disabled={actionBusy}
                                        >
                                            {t("ownerAppsRejectMissingDocsBtn")}
                                        </button>
                                        <div className="admin-owner-modal-footer-right">
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-ghost"
                                                onClick={() =>
                                                    setModalAction(null)
                                                }
                                                disabled={actionBusy}
                                            >
                                                {t("cancel")}
                                            </button>
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-lime"
                                                onClick={handleConfirmApprove}
                                                disabled={
                                                    actionBusy || !isAllVerified
                                                }
                                                title={
                                                    isAllVerified
                                                        ? t(
                                                              "ownerAppsConfirmApprove",
                                                          )
                                                        : t(
                                                              "ownerAppsChecklistWarning",
                                                          )
                                                }
                                            >
                                                {actionBusy
                                                    ? t("saving")
                                                    : t(
                                                          "ownerAppsConfirmApprove",
                                                      )}
                                            </button>
                                        </div>
                                    </>
                                ) : (
                                    <>
                                        {activeApp.status === "Pending" ? (
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-danger-ghost"
                                                onClick={() =>
                                                    setModalAction("reject")
                                                }
                                                disabled={actionBusy}
                                            >
                                                <AdminIcon
                                                    name="close"
                                                    size={14}
                                                />
                                                <span>
                                                    {t("ownerAppsReject")}
                                                </span>
                                            </button>
                                        ) : (
                                            <span />
                                        )}
                                        <div className="admin-owner-modal-footer-right">
                                            <button
                                                type="button"
                                                className="admin-btn admin-btn-sm admin-btn-ghost"
                                                onClick={() =>
                                                    setModalAction(null)
                                                }
                                            >
                                                {t("close")}
                                            </button>
                                            {activeApp.status === "Pending" && (
                                                <button
                                                    type="button"
                                                    className="admin-btn admin-btn-sm admin-btn-lime"
                                                    onClick={() =>
                                                        setModalAction(
                                                            "approve",
                                                        )
                                                    }
                                                >
                                                    <AdminIcon
                                                        name="check"
                                                        size={14}
                                                    />
                                                    <span>
                                                        {t(
                                                            "ownerAppsVerifyAndApprove",
                                                        )}
                                                    </span>
                                                </button>
                                            )}
                                        </div>
                                    </>
                                )}
                            </footer>
                        </div>
                    </div>
                )}

            {/* 7. Modal Từ chối hồ sơ */}
            {modalAction === "reject" && activeApp && (
                <div
                    className="admin-modal-backdrop"
                    onClick={() => !actionBusy && setModalAction(null)}
                >
                    <div
                        className="admin-modal admin-owner-reject-modal"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="owner-reject-modal-title"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="admin-owner-reject-top">
                            <span className="admin-modal-icon reject">
                                <AdminIcon name="close" size={20} />
                            </span>
                            <StatusPill status={activeApp.status} />
                        </div>

                        <h2 id="owner-reject-modal-title">
                            {t("ownerAppsRejectConfirmTitle")}
                        </h2>
                        <p className="admin-owner-reject-desc">
                            {t("ownerAppsRejectConfirmDesc")}{" "}
                            <strong>
                                {activeApp.fullName || activeApp.email}
                            </strong>
                            .
                        </p>

                        <div className="admin-owner-reject-summary">
                            <div>
                                <span>{isVi ? "Chủ bãi:" : "Applicant:"}</span>
                                <strong>
                                    {activeApp.fullName ||
                                        t("ownerAppsNotUpdated")}
                                </strong>
                            </div>
                            <div>
                                <span>
                                    {isVi ? "Doanh nghiệp:" : "Business:"}
                                </span>
                                <strong>
                                    {activeApp.businessName ||
                                        t("ownerAppsNotUpdated")}
                                </strong>
                            </div>
                        </div>

                        <label className="admin-field admin-owner-reject-field">
                            <span>{t("ownerAppsRejectReasonLabel")}</span>
                            <textarea
                                rows={4}
                                autoFocus
                                value={rejectReason}
                                onChange={(e) =>
                                    setRejectReason(e.target.value)
                                }
                                placeholder={t(
                                    "ownerAppsRejectReasonPlaceholder",
                                )}
                            />
                        </label>

                        <div className="admin-modal-actions">
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
                                {actionBusy
                                    ? t("saving")
                                    : t("ownerAppsConfirmRejectBtn")}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* 8. Lightbox / Preview tài liệu */}
            {previewDoc && (
                <div
                    className="admin-modal-backdrop admin-preview-backdrop"
                    onClick={closePreview}
                >
                    <div
                        className="admin-modal admin-owner-preview-modal"
                        role="dialog"
                        aria-modal="true"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <header className="admin-owner-preview-header">
                            <strong>{previewDoc.name}</strong>
                            <button
                                type="button"
                                className="admin-owner-modal-close"
                                onClick={closePreview}
                                aria-label={t("close")}
                            >
                                ✕
                            </button>
                        </header>
                        <div className="admin-owner-preview-body">
                            {previewDoc.isImage ? (
                                // eslint-disable-next-line @next/next/no-img-element
                                <img
                                    src={previewDoc.url}
                                    alt={previewDoc.name}
                                />
                            ) : (
                                <iframe
                                    src={previewDoc.url}
                                    title={previewDoc.name}
                                />
                            )}
                        </div>
                        <footer className="admin-owner-preview-footer">
                            <button
                                type="button"
                                className="admin-btn admin-btn-sm admin-btn-ghost"
                                onClick={closePreview}
                            >
                                {t("close")}
                            </button>
                            <a
                                href={previewDoc.url}
                                target="_blank"
                                rel="noreferrer"
                                className="admin-btn admin-btn-sm admin-btn-lime"
                            >
                                {t("ownerAppsOpenNewTab")}
                            </a>
                        </footer>
                    </div>
                </div>
            )}

            {toast && (
                <div className="admin-toast" role="status">
                    {toast}
                </div>
            )}
        </div>
    );
}
