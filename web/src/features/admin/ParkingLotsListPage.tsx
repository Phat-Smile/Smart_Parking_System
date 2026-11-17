"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
    AdminEmptyState,
    AdminErrorState,
    AdminIcon,
    StatusPill,
    formatDate,
    formatWaitingDuration,
} from "./AdminUi";
import {
    approveParkingLot,
    describeReviewError,
    getAdminParkingLotDetail,
    getAdminParkingLots,
    isParkingLotId,
    rejectParkingLot,
} from "./reviewParkingLot";
import { countByStatus, refreshAdminLots, useAdminLots } from "./adminLotsData";
import type { AdminParkingLot, ApprovalStatus } from "./types";
import { ApiError } from "@/shared/errors/api-error";
import { useLanguage } from "@/shared/i18n/LanguageContext";

type Filter = ApprovalStatus | "All";
type SortOrder = "oldest" | "newest" | "name";
type SubmittedFilter = "all" | "today" | "7days" | "30days";

const PAGE_SIZE = 10;

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
    const { t, language } = useLanguage();
    const isVi = language === "vi";

    const [status, setStatus] = useState<Filter>(
        mode === "pending" ? "Pending" : initialStatus,
    );
    const [lots, setLots] = useState<AdminParkingLot[]>([]);
    const [loading, setLoading] = useState(true);
    const [rawError, setRawError] = useState<unknown>(null);

    // Global store snapshot (loaded from GET /api/parking/admin/lots by AdminShell)
    const globalLots = useAdminLots();

    // Client-side Search, Filter & Sort Toolbar state
    const [search, setSearch] = useState("");
    const [typeFilter, setTypeFilter] = useState<string>("All");
    const [submittedFilter, setSubmittedFilter] =
        useState<SubmittedFilter>("all");
    const [sortOrder, setSortOrder] = useState<SortOrder>("oldest");
    const [currentPage, setCurrentPage] = useState(1);
    const [referenceNowMs, setReferenceNowMs] = useState<number>(() =>
        new Date().getTime(),
    );

    // UUID Lookup Card state
    const [lookupId, setLookupId] = useState("");
    const [lookupError, setLookupError] = useState("");
    const [lookupBusy, setLookupBusy] = useState(false);

    const errorMsg = useMemo(() => {
        if (!rawError) return "";
        if (typeof rawError === "string") return rawError;
        return describeReviewError(rawError, language);
    }, [rawError, language]);

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
        setRawError(null);
        try {
            const data = await getAdminParkingLots(
                mode === "pending" ? "Pending" : status,
            );
            setLots(data);
            setReferenceNowMs(new Date().getTime());
        } catch (err) {
            setRawError(err);
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
                    setReferenceNowMs(new Date().getTime());
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
    }, [mode, status]);

    useEffect(() => {
        if (!toast) return;
        const timer = setTimeout(() => setToast(null), 4000);
        return () => clearTimeout(timer);
    }, [toast]);

    const counts = useMemo(() => {
        const localCounts = {
            All: lots.length,
            Pending: lots.filter((l) => l.status === "Pending").length,
            Approved: lots.filter((l) => l.status === "Approved").length,
            Rejected: lots.filter((l) => l.status === "Rejected").length,
        };
        if (status === "All" || globalLots.length === 0) {
            return localCounts;
        }
        const globalByStatus = countByStatus(globalLots);
        return {
            All: Math.max(globalLots.length, localCounts.All),
            Pending:
                status === "Pending"
                    ? localCounts.Pending
                    : globalByStatus.Pending,
            Approved:
                status === "Approved"
                    ? localCounts.Approved
                    : globalByStatus.Approved,
            Rejected:
                status === "Rejected"
                    ? localCounts.Rejected
                    : globalByStatus.Rejected,
        };
    }, [lots, globalLots, status]);

    // Extract distinct lot types available in current dataset
    const availableTypes = useMemo(() => {
        const set = new Set<string>();
        lots.forEach((lot) => {
            if (lot.type && lot.type.trim()) {
                set.add(lot.type.trim());
            }
        });
        return Array.from(set);
    }, [lots]);

    const hasSubmittedDates = useMemo(
        () => lots.some((l) => Boolean(l.submittedAtUtc)),
        [lots],
    );

    const hasActiveFilters = Boolean(
        search.trim() ||
        typeFilter !== "All" ||
        submittedFilter !== "all" ||
        sortOrder !== "oldest" ||
        (mode === "all" && status !== "All"),
    );

    function handleStatusTabChange(nextStatus: Filter) {
        if (nextStatus === status) return;
        setLoading(true);
        setRawError(null);
        setStatus(nextStatus);
        setCurrentPage(1);
    }

    function handleClearFilters() {
        setSearch("");
        setTypeFilter("All");
        setSubmittedFilter("all");
        setSortOrder("oldest");
        setCurrentPage(1);
        if (mode === "all" && status !== "All") {
            setLoading(true);
            setRawError(null);
            setStatus("All");
        }
    }

    const rows = useMemo(() => {
        const term = search.trim().toLowerCase();

        return lots
            .filter((lot) => status === "All" || lot.status === status)
            .filter((lot) => typeFilter === "All" || lot.type === typeFilter)
            .filter((lot) => {
                if (submittedFilter === "all" || !lot.submittedAtUtc) {
                    return submittedFilter === "all";
                }
                const ts = new Date(lot.submittedAtUtc).getTime();
                if (Number.isNaN(ts)) return false;
                const diffDays =
                    Math.max(0, referenceNowMs - ts) / (1000 * 60 * 60 * 24);
                if (submittedFilter === "today") return diffDays <= 1;
                if (submittedFilter === "7days") return diffDays <= 7;
                if (submittedFilter === "30days") return diffDays <= 30;
                return true;
            })
            .filter(
                (lot) =>
                    !term ||
                    [
                        lot.name,
                        lot.ownerName,
                        lot.ownerEmail,
                        lot.ownerUserId,
                        lot.address,
                        lot.id,
                    ].some((v) => (v ?? "").toLowerCase().includes(term)),
            )
            .sort((a, b) => {
                if (sortOrder === "name") {
                    return a.name.localeCompare(b.name, isVi ? "vi" : "en");
                }
                const dateA = a.submittedAtUtc ?? "";
                const dateB = b.submittedAtUtc ?? "";
                if (dateA && dateB && dateA !== dateB) {
                    return sortOrder === "newest"
                        ? dateB.localeCompare(dateA)
                        : dateA.localeCompare(dateB);
                }
                return sortOrder === "newest"
                    ? b.name.localeCompare(a.name)
                    : a.name.localeCompare(b.name);
            });
    }, [
        lots,
        search,
        status,
        typeFilter,
        submittedFilter,
        sortOrder,
        isVi,
        referenceNowMs,
    ]);

    const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
    const safePage = Math.min(currentPage, totalPages);
    const pagedRows = useMemo(() => {
        const start = (safePage - 1) * PAGE_SIZE;
        return rows.slice(start, start + PAGE_SIZE);
    }, [rows, safePage]);

    async function handlePasteUuid() {
        if (typeof navigator === "undefined" || !navigator.clipboard?.readText)
            return;
        try {
            const text = await navigator.clipboard.readText();
            const trimmed = text.trim();
            setLookupId(trimmed);
            if (trimmed && !isParkingLotId(trimmed)) {
                setLookupError(
                    isVi
                        ? "Định dạng UUID vừa dán không hợp lệ (ví dụ: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)."
                        : "Pasted text is not a valid UUID format.",
                );
            } else {
                setLookupError("");
            }
        } catch {
            // Clipboard permission denied or unavailable
        }
    }

    async function openById(event: React.FormEvent) {
        event.preventDefault();
        const trimmed = lookupId.trim();
        if (!isParkingLotId(trimmed)) {
            setLookupError(
                isVi
                    ? "Vui lòng nhập mã UUID bãi xe hợp lệ (36 ký tự chuẩn UUID)."
                    : "Enter a valid parking lot UUID.",
            );
            return;
        }

        // If already in the loaded list, navigate immediately
        if (lots.some((l) => l.id.toLowerCase() === trimmed.toLowerCase())) {
            setLookupError("");
            router.push(`/admin/parking-lots/${trimmed}`);
            return;
        }

        setLookupBusy(true);
        setLookupError("");
        try {
            await getAdminParkingLotDetail(trimmed);
            router.push(`/admin/parking-lots/${trimmed}`);
        } catch (err) {
            if (err instanceof ApiError && err.status === 404) {
                setLookupError(
                    isVi
                        ? `Không tìm thấy hồ sơ bãi đỗ xe với mã UUID "${trimmed}". Vui lòng kiểm tra lại.`
                        : `No parking lot found with UUID "${trimmed}".`,
                );
            } else if (err instanceof ApiError) {
                setLookupError(describeReviewError(err, language));
            } else {
                router.push(`/admin/parking-lots/${trimmed}`);
            }
        } finally {
            setLookupBusy(false);
        }
    }

    async function handleQuickApprove() {
        if (!selectedLot) return;
        setActionBusy(true);
        try {
            await approveParkingLot(selectedLot.id);
            setToast(
                isVi
                    ? `Đã duyệt bãi đỗ xe "${selectedLot.name}".`
                    : `Approved parking lot "${selectedLot.name}".`,
            );
            setSelectedLot(null);
            setReviewAction(null);
            await Promise.all([
                loadLots(),
                refreshAdminLots().catch(() => undefined),
            ]);
        } catch (err) {
            setRawError(err);
        } finally {
            setActionBusy(false);
        }
    }

    async function handleQuickReject() {
        if (!selectedLot || !rejectReason.trim()) return;
        setActionBusy(true);
        try {
            await rejectParkingLot(selectedLot.id, rejectReason.trim());
            setToast(
                isVi
                    ? `Đã từ chối bãi đỗ xe "${selectedLot.name}".`
                    : `Rejected parking lot "${selectedLot.name}".`,
            );
            setSelectedLot(null);
            setReviewAction(null);
            setRejectReason("");
            await Promise.all([
                loadLots(),
                refreshAdminLots().catch(() => undefined),
            ]);
        } catch (err) {
            setRawError(err);
        } finally {
            setActionBusy(false);
        }
    }

    const tabs: Filter[] = ["All", "Pending", "Approved", "Rejected"];
    const hasError = Boolean(rawError);
    const notUpdatedText = isVi ? "Chưa cập nhật" : "Not updated";

    return (
        <>
            {/* Page Heading */}
            <header className="admin-heading admin-dash-heading">
                <div className="admin-dash-heading-left">
                    <p className="admin-eyebrow">
                        {mode === "pending"
                            ? isVi
                                ? "Hàng đợi xét duyệt"
                                : "Review Queue"
                            : t("parkingLotsEyebrow")}
                    </p>
                    <h1>
                        {mode === "pending"
                            ? t("adminPendingLots")
                            : t("parkingLotsTitle")}
                    </h1>
                    <p className="admin-heading-desc">
                        {mode === "pending"
                            ? isVi
                                ? "Kiểm tra thông tin pháp lý, vị trí tọa độ, sơ đồ chỗ đỗ và biểu phí của các hồ sơ bãi xe mới gửi trước khi phê duyệt đưa vào vận hành."
                                : "Verify location, layout capacity, and pricing of newly submitted parking lot applications before approving them for public operation."
                            : isVi
                              ? "Quản lý toàn bộ danh mục bãi đỗ xe trên hệ thống ParkWise theo trạng thái xét duyệt và phân công vận hành."
                              : "Manage all parking lots across ParkWise by approval and operational status."}
                    </p>
                </div>
                <div className="admin-dash-heading-actions">
                    <button
                        type="button"
                        onClick={loadLots}
                        className="admin-btn admin-btn-ghost"
                        disabled={loading || actionBusy}
                        title={
                            isVi
                                ? "Tải lại danh sách bãi đỗ xe từ hệ thống"
                                : "Reload parking lots from server"
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

            {/* E. Compact Workflow Navigation Bar */}
            <nav
                className="admin-workflow-bar"
                aria-label={
                    isVi
                        ? "Quy trình xét duyệt và vận hành"
                        : "Approval and operations workflow"
                }
            >
                <span className="admin-workflow-label">
                    {isVi ? "Quy trình nghiệp vụ:" : "Workflow:"}
                </span>
                <div className="admin-workflow-links">
                    {mode === "all" && (
                        <Link
                            href="/admin/parking-lots"
                            className="admin-workflow-pill is-active"
                            aria-current="page"
                        >
                            <span>{t("adminParkingLots")}</span>
                            {counts.All > 0 && (
                                <b className="admin-workflow-count">
                                    {counts.All}
                                </b>
                            )}
                        </Link>
                    )}
                    <Link
                        href="/admin/parking-lots/pending"
                        className={`admin-workflow-pill ${mode === "pending" ? "is-active" : ""}`}
                        aria-current={mode === "pending" ? "page" : undefined}
                    >
                        <span>{t("parkingLotsPendingTab")}</span>
                        {counts.Pending > 0 && (
                            <b className="admin-workflow-count is-pending">
                                {counts.Pending}
                            </b>
                        )}
                    </Link>
                    <Link
                        href="/admin/owner-applications"
                        className="admin-workflow-pill"
                    >
                        <span>{t("parkingLotsOwnersTab")}</span>
                    </Link>
                </div>
            </nav>

            {/* C & D. Status Tabs + Search & Filter Toolbar */}
            <section
                className="admin-toolbar-card"
                aria-label={
                    isVi
                        ? "Thanh tìm kiếm và bộ lọc hồ sơ"
                        : "Search and filter toolbar"
                }
            >
                {mode === "all" && (
                    <div className="admin-toolbar-top">
                        <div
                            className="admin-tabs admin-status-tabs"
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
                                        id={`lots-tab-${tabKey}`}
                                        className={`admin-status-tab tab-${tabKey} ${isSelected ? "is-selected" : ""}`}
                                        aria-selected={isSelected}
                                        onClick={() =>
                                            handleStatusTabChange(tab)
                                        }
                                    >
                                        {tab !== "All" && (
                                            <i
                                                className={`admin-tab-dot dot-${tabKey}`}
                                                aria-hidden="true"
                                            />
                                        )}
                                        <span className="admin-tab-label">
                                            {tab === "All"
                                                ? t("all")
                                                : tab === "Pending"
                                                  ? t("pending")
                                                  : tab === "Approved"
                                                    ? t("approved")
                                                    : t("rejected")}
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

                        {!loading && !hasError && (
                            <div className="admin-toolbar-result-badge">
                                {isVi ? (
                                    <>
                                        Kết quả: <strong>{rows.length}</strong>{" "}
                                        / <strong>{lots.length}</strong> bãi xe
                                    </>
                                ) : (
                                    <>
                                        Results: <strong>{rows.length}</strong>{" "}
                                        / <strong>{lots.length}</strong> lots
                                    </>
                                )}
                            </div>
                        )}
                    </div>
                )}

                <div className="admin-toolbar-controls">
                    <label className="admin-search admin-toolbar-search">
                        <AdminIcon name="search" size={16} />
                        <input
                            id="lots-search"
                            value={search}
                            onChange={(e) => {
                                setSearch(e.target.value);
                                setCurrentPage(1);
                            }}
                            placeholder={
                                isVi
                                    ? "Tìm theo tên bãi xe, chủ bãi hoặc địa chỉ..."
                                    : t("searchLotsPlaceholder")
                            }
                        />
                        {search && (
                            <button
                                type="button"
                                className="admin-search-clear"
                                onClick={() => {
                                    setSearch("");
                                    setCurrentPage(1);
                                }}
                                title={
                                    isVi
                                        ? "Xóa từ khóa tìm kiếm"
                                        : "Clear search keyword"
                                }
                                aria-label={
                                    isVi ? "Xóa từ khóa" : "Clear search"
                                }
                            >
                                <AdminIcon name="close" size={13} />
                            </button>
                        )}
                    </label>

                    <div className="admin-toolbar-filters">
                        {/* Lot Type Filter */}
                        <div
                            className="admin-filter-group"
                            title={
                                isVi
                                    ? "Lọc danh sách theo loại hình bãi đỗ xe"
                                    : "Filter by parking lot type"
                            }
                        >
                            <AdminIcon name="filter" size={14} />
                            <select
                                className="admin-select admin-toolbar-select"
                                value={typeFilter}
                                onChange={(e) => {
                                    setTypeFilter(e.target.value);
                                    setCurrentPage(1);
                                }}
                                aria-label={
                                    isVi ? "Lọc theo loại bãi xe" : "Lot type"
                                }
                            >
                                <option value="All">
                                    {isVi ? "Tất cả loại bãi" : "All lot types"}
                                </option>
                                {availableTypes.map((type) => (
                                    <option key={type} value={type}>
                                        {type}
                                    </option>
                                ))}
                            </select>
                        </div>

                        {/* Submitted Date Filter */}
                        {hasSubmittedDates && (
                            <div
                                className="admin-filter-group"
                                title={
                                    isVi
                                        ? "Lọc theo thời gian nộp hồ sơ"
                                        : "Filter by submitted date"
                                }
                            >
                                <AdminIcon name="calendar" size={14} />
                                <select
                                    className="admin-select admin-toolbar-select"
                                    value={submittedFilter}
                                    onChange={(e) => {
                                        setSubmittedFilter(
                                            e.target.value as SubmittedFilter,
                                        );
                                        setCurrentPage(1);
                                    }}
                                    aria-label={
                                        isVi
                                            ? "Lọc theo ngày gửi"
                                            : "Submitted date"
                                    }
                                >
                                    <option value="all">
                                        {isVi
                                            ? "Mọi ngày gửi"
                                            : "Any submitted date"}
                                    </option>
                                    <option value="today">
                                        {isVi ? "Trong 24 giờ qua" : "Last 24h"}
                                    </option>
                                    <option value="7days">
                                        {isVi
                                            ? "7 ngày gần đây"
                                            : "Last 7 days"}
                                    </option>
                                    <option value="30days">
                                        {isVi
                                            ? "30 ngày gần đây"
                                            : "Last 30 days"}
                                    </option>
                                </select>
                            </div>
                        )}

                        {/* Sort Order Selector */}
                        <div
                            className="admin-filter-group"
                            title={
                                isVi
                                    ? "Thứ tự sắp xếp danh sách bãi đỗ xe"
                                    : "Sort parking lots order"
                            }
                        >
                            <AdminIcon name="sort" size={14} />
                            <select
                                className="admin-select admin-toolbar-select"
                                value={sortOrder}
                                onChange={(e) => {
                                    setSortOrder(e.target.value as SortOrder);
                                    setCurrentPage(1);
                                }}
                                aria-label={
                                    isVi ? "Sắp xếp hồ sơ" : "Sort records"
                                }
                            >
                                <option value="oldest">
                                    {isVi
                                        ? "Cũ nhất / Ưu tiên duyệt"
                                        : "Oldest first"}
                                </option>
                                <option value="newest">
                                    {isVi ? "Mới nhất trước" : "Newest first"}
                                </option>
                                <option value="name">
                                    {isVi ? "Tên bãi xe (A → Z)" : "Name (A–Z)"}
                                </option>
                            </select>
                        </div>

                        {hasActiveFilters && (
                            <button
                                type="button"
                                onClick={handleClearFilters}
                                className="admin-btn admin-btn-sm admin-btn-ghost admin-clear-filter-btn"
                                title={
                                    isVi
                                        ? "Đặt lại tất cả từ khóa tìm kiếm và bộ lọc"
                                        : "Reset all search keywords and filters"
                                }
                            >
                                <AdminIcon name="close" size={13} />
                                <span>
                                    {isVi ? "Xóa bộ lọc" : "Clear filters"}
                                </span>
                            </button>
                        )}
                    </div>
                </div>

                {mode === "pending" &&
                    !loading &&
                    !hasError &&
                    lots.length > 0 && (
                        <div className="admin-toolbar-summary">
                            <span>
                                {isVi ? (
                                    <>
                                        Hiển thị <strong>{rows.length}</strong>{" "}
                                        / <strong>{lots.length}</strong> hồ sơ
                                        bãi xe
                                    </>
                                ) : (
                                    <>
                                        Showing <strong>{rows.length}</strong>{" "}
                                        of <strong>{lots.length}</strong>{" "}
                                        parking lots
                                    </>
                                )}
                            </span>
                        </div>
                    )}
            </section>

            {/* A & B. Data Table Card with 4 Distinct UX States */}
            <section className="admin-card admin-table-card">
                {/* STATE 1: ERROR STATE */}
                {hasError && !loading && (
                    <div className="admin-table-state-pad">
                        <AdminErrorState
                            title={
                                isVi
                                    ? "Không thể tải danh sách hồ sơ bãi xe"
                                    : "Unable to load parking lot applications"
                            }
                            message={errorMsg}
                            onRetry={loadLots}
                            retryLabel={isVi ? "Thử lại" : "Retry"}
                        />
                    </div>
                )}

                {/* STATE 2: LOADING STATE */}
                {loading && (
                    <>
                        <div className="admin-table-scroll">
                            <table className="admin-table admin-lots-table">
                                <thead>
                                    <tr>
                                        <th className="col-lot-name">
                                            {t("lotNameHeader")}
                                        </th>
                                        <th className="col-lot-owner">
                                            {t("lotOwnerHeader")}
                                        </th>
                                        <th className="col-lot-address">
                                            {isVi ? "Địa chỉ" : "Address"}
                                        </th>
                                        <th className="col-lot-date">
                                            {isVi
                                                ? "Ngày nộp"
                                                : "Submitted Date"}
                                        </th>
                                        <th className="col-lot-capacity">
                                            {t("lotCapacityHeader")}
                                        </th>
                                        <th className="col-lot-status">
                                            {t("status")}
                                        </th>
                                        <th
                                            className="col-lot-actions"
                                            aria-label="Actions"
                                            style={{ textAlign: "right" }}
                                        >
                                            {t("actions")}
                                        </th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {[0, 1, 2, 3].map((idx) => (
                                        <tr
                                            key={idx}
                                            className="admin-table-skel-row"
                                        >
                                            <td>
                                                <div className="admin-skel-line admin-skel-md" />
                                                <div
                                                    className="admin-skel-line admin-skel-sm"
                                                    style={{ marginTop: 6 }}
                                                />
                                            </td>
                                            <td>
                                                <div className="admin-skel-line admin-skel-md" />
                                            </td>
                                            <td>
                                                <div className="admin-skel-line admin-skel-md" />
                                            </td>
                                            <td>
                                                <div className="admin-skel-line admin-skel-sm" />
                                            </td>
                                            <td>
                                                <div className="admin-skel-line admin-skel-sm" />
                                            </td>
                                            <td>
                                                <div className="admin-skel-pill" />
                                            </td>
                                            <td style={{ textAlign: "right" }}>
                                                <div
                                                    className="admin-skel-pill"
                                                    style={{
                                                        marginLeft: "auto",
                                                    }}
                                                />
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                        <p className="admin-sr-only" role="status">
                            {t("loading")}
                        </p>
                    </>
                )}

                {/* STATE 3: SUCCESS + DATA — 7 Standardized Columns without Duplicate Address or Raw GPS */}
                {!loading && !hasError && rows.length > 0 && (
                    <>
                        <div className="admin-table-scroll">
                            <table className="admin-table admin-lots-table">
                                <thead>
                                    <tr>
                                        <th className="col-lot-name">
                                            {t("lotNameHeader")}
                                        </th>
                                        <th className="col-lot-owner">
                                            {t("lotOwnerHeader")}
                                        </th>
                                        <th className="col-lot-address">
                                            {isVi ? "Địa chỉ" : "Address"}
                                        </th>
                                        <th className="col-lot-date">
                                            {isVi
                                                ? "Ngày nộp"
                                                : "Submitted Date"}
                                        </th>
                                        <th className="col-lot-capacity">
                                            {t("lotCapacityHeader")}
                                        </th>
                                        <th className="col-lot-status">
                                            {t("status")}
                                        </th>
                                        <th
                                            className="col-lot-actions"
                                            aria-label="Actions"
                                            style={{ textAlign: "right" }}
                                        >
                                            {t("actions")}
                                        </th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {pagedRows.map((lot) => {
                                        const waitingText =
                                            formatWaitingDuration(
                                                lot.submittedAtUtc,
                                                language,
                                            );
                                        const hasCoords =
                                            typeof lot.latitude === "number" &&
                                            typeof lot.longitude === "number" &&
                                            (lot.latitude !== 0 ||
                                                lot.longitude !== 0);
                                        const ownerText =
                                            lot.ownerName?.trim() ||
                                            lot.ownerUserId?.trim() ||
                                            null;
                                        const addressText =
                                            lot.address?.trim() || null;

                                        return (
                                            <tr key={lot.id}>
                                                {/* 1. Tên bãi xe: Chỉ hiển thị tên bãi và loại bãi (không lặp lại địa chỉ) */}
                                                <td className="admin-lot-cell-primary">
                                                    <div className="admin-lot-name-stack">
                                                        <Link
                                                            href={`/admin/parking-lots/${lot.id}`}
                                                            className="admin-lot-primary-link"
                                                            title={lot.name}
                                                        >
                                                            <strong>
                                                                {lot.name}
                                                            </strong>
                                                        </Link>
                                                        {lot.type ? (
                                                            <span className="admin-lot-type-tag">
                                                                {lot.type}
                                                            </span>
                                                        ) : (
                                                            <span className="admin-cell-unupdated">
                                                                {notUpdatedText}
                                                            </span>
                                                        )}
                                                    </div>
                                                </td>

                                                {/* 2. Chủ bãi */}
                                                <td className="admin-lot-cell-owner">
                                                    {ownerText ? (
                                                        <div className="admin-owner-cell">
                                                            <span
                                                                className="admin-owner-primary"
                                                                title={
                                                                    ownerText
                                                                }
                                                            >
                                                                {ownerText}
                                                            </span>
                                                            {lot.ownerEmail && (
                                                                <small
                                                                    title={
                                                                        lot.ownerEmail
                                                                    }
                                                                >
                                                                    {
                                                                        lot.ownerEmail
                                                                    }
                                                                </small>
                                                            )}
                                                        </div>
                                                    ) : (
                                                        <span className="admin-cell-unupdated">
                                                            {notUpdatedText}
                                                        </span>
                                                    )}
                                                </td>

                                                {/* 3. Địa chỉ: Chỉ hiển thị địa chỉ, tọa độ GPS chuyển vào tooltip */}
                                                <td className="admin-address-cell">
                                                    {addressText ? (
                                                        <span
                                                            className="admin-address-line"
                                                            title={
                                                                hasCoords
                                                                    ? `${addressText} • GPS: ${lot.latitude?.toFixed(6)}, ${lot.longitude?.toFixed(6)}`
                                                                    : addressText
                                                            }
                                                        >
                                                            {addressText}
                                                        </span>
                                                    ) : (
                                                        <span className="admin-cell-unupdated">
                                                            {notUpdatedText}
                                                        </span>
                                                    )}
                                                </td>

                                                {/* 4. Ngày nộp */}
                                                <td className="admin-lot-cell-date">
                                                    {lot.submittedAtUtc ? (
                                                        <div className="admin-date-cell">
                                                            <span>
                                                                {formatDate(
                                                                    lot.submittedAtUtc,
                                                                )}
                                                            </span>
                                                            {waitingText && (
                                                                <small className="admin-waiting-inline">
                                                                    {
                                                                        waitingText
                                                                    }
                                                                </small>
                                                            )}
                                                        </div>
                                                    ) : (
                                                        <span className="admin-cell-unupdated">
                                                            {notUpdatedText}
                                                        </span>
                                                    )}
                                                </td>

                                                {/* 5. Sức chứa */}
                                                <td className="admin-lot-cell-capacity">
                                                    {lot.totalCapacity !=
                                                    null ? (
                                                        <span className="admin-capacity-badge">
                                                            <strong>
                                                                {
                                                                    lot.totalCapacity
                                                                }
                                                            </strong>{" "}
                                                            {isVi
                                                                ? "chỗ"
                                                                : "slots"}
                                                        </span>
                                                    ) : (
                                                        <span
                                                            className="admin-cell-unupdated"
                                                            title={
                                                                isVi
                                                                    ? "Mở trang Xem chi tiết để kiểm tra sơ đồ sức chứa từng tầng"
                                                                    : "Open lot details to inspect floor layout capacity"
                                                            }
                                                        >
                                                            {notUpdatedText}
                                                        </span>
                                                    )}
                                                </td>

                                                {/* 6. Trạng thái */}
                                                <td className="admin-lot-cell-status">
                                                    <StatusPill
                                                        status={lot.status}
                                                    />
                                                </td>

                                                {/* 7. Thao tác: Phân biệt rõ nút chính (Duyệt / Phân công) và nút phụ (Xem) */}
                                                <td className="admin-table-action">
                                                    <div className="admin-row-actions">
                                                        {lot.status ===
                                                            "Pending" && (
                                                            <>
                                                                <button
                                                                    type="button"
                                                                    className="admin-btn admin-btn-sm admin-btn-lime"
                                                                    disabled={
                                                                        actionBusy
                                                                    }
                                                                    title={
                                                                        isVi
                                                                            ? "Phê duyệt hồ sơ bãi đỗ xe này"
                                                                            : "Approve this parking lot"
                                                                    }
                                                                    onClick={() => {
                                                                        setSelectedLot(
                                                                            lot,
                                                                        );
                                                                        setReviewAction(
                                                                            "approve",
                                                                        );
                                                                    }}
                                                                >
                                                                    <AdminIcon
                                                                        name="check"
                                                                        size={
                                                                            14
                                                                        }
                                                                    />
                                                                    <span>
                                                                        {t(
                                                                            "approveLot",
                                                                        )}
                                                                    </span>
                                                                </button>
                                                                <button
                                                                    type="button"
                                                                    className="admin-btn admin-btn-sm admin-btn-danger-ghost"
                                                                    disabled={
                                                                        actionBusy
                                                                    }
                                                                    title={
                                                                        isVi
                                                                            ? "Từ chối hồ sơ bãi đỗ xe và gửi lý do"
                                                                            : "Reject this parking lot"
                                                                    }
                                                                    onClick={() => {
                                                                        setSelectedLot(
                                                                            lot,
                                                                        );
                                                                        setReviewAction(
                                                                            "reject",
                                                                        );
                                                                    }}
                                                                >
                                                                    <span>
                                                                        {t(
                                                                            "rejectLot",
                                                                        )}
                                                                    </span>
                                                                </button>
                                                            </>
                                                        )}

                                                        <Link
                                                            href={`/admin/parking-lots/${lot.id}`}
                                                            className="admin-btn admin-btn-sm admin-btn-secondary-view"
                                                            title={
                                                                isVi
                                                                    ? "Xem chi tiết hồ sơ, bản đồ tọa độ, sơ đồ tầng và bảng giá"
                                                                    : "View lot details, coordinates, floor layout, and pricing"
                                                            }
                                                        >
                                                            <AdminIcon
                                                                name="eye"
                                                                size={14}
                                                            />
                                                            <span>
                                                                {lot.status ===
                                                                "Pending"
                                                                    ? t(
                                                                          "details",
                                                                      )
                                                                    : t("view")}
                                                            </span>
                                                        </Link>
                                                    </div>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>

                        {totalPages > 1 && (
                            <div className="admin-pager">
                                <span>
                                    {isVi
                                        ? `Trang ${safePage} / ${totalPages} (${rows.length} hồ sơ)`
                                        : `Page ${safePage} of ${totalPages} (${rows.length} lots)`}
                                </span>
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-sm admin-btn-ghost"
                                    disabled={safePage <= 1}
                                    onClick={() =>
                                        setCurrentPage((p) =>
                                            Math.max(1, p - 1),
                                        )
                                    }
                                >
                                    <AdminIcon name="chevronLeft" size={14} />
                                    {isVi ? "Trước" : "Previous"}
                                </button>
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-sm admin-btn-ghost"
                                    disabled={safePage >= totalPages}
                                    onClick={() =>
                                        setCurrentPage((p) =>
                                            Math.min(totalPages, p + 1),
                                        )
                                    }
                                >
                                    {isVi ? "Tiếp" : "Next"}
                                    <AdminIcon name="chevronRight" size={14} />
                                </button>
                            </div>
                        )}
                    </>
                )}

                {/* STATE 4: SUCCESS + EMPTY STATE */}
                {!loading && !hasError && rows.length === 0 && (
                    <div className="admin-table-state-pad">
                        {lots.length > 0 && hasActiveFilters ? (
                            <AdminEmptyState
                                icon="search"
                                title={
                                    isVi
                                        ? "Không tìm thấy hồ sơ bãi xe phù hợp"
                                        : "No matching parking lots found"
                                }
                                description={
                                    isVi
                                        ? "Không có bãi đỗ xe nào khớp với từ khóa hoặc bộ lọc hiện tại. Hãy thử xóa bộ lọc để xem toàn bộ danh sách."
                                        : "No parking lots match your current search or filter criteria."
                                }
                                action={
                                    <button
                                        type="button"
                                        onClick={handleClearFilters}
                                        className="admin-btn admin-btn-sm admin-btn-dark"
                                    >
                                        <AdminIcon name="close" size={14} />
                                        <span>
                                            {isVi
                                                ? "Xóa bộ lọc"
                                                : "Clear filters"}
                                        </span>
                                    </button>
                                }
                            />
                        ) : (
                            <AdminEmptyState
                                icon="inbox"
                                title={
                                    isVi
                                        ? "Hiện chưa có hồ sơ chờ duyệt"
                                        : "No parking lots pending review"
                                }
                                description={
                                    isVi
                                        ? "Các hồ sơ mới gửi sẽ xuất hiện tại đây"
                                        : "Newly submitted parking lot applications will appear here"
                                }
                                action={
                                    <button
                                        type="button"
                                        onClick={loadLots}
                                        className="admin-btn admin-btn-sm admin-btn-ghost"
                                    >
                                        <AdminIcon name="refresh" size={14} />
                                        <span>{t("refresh")}</span>
                                    </button>
                                }
                            />
                        )}
                    </div>
                )}
            </section>

            {/* UUID Lookup Card */}
            <section
                className="admin-card admin-lookup-card"
                aria-labelledby="uuid-lookup-heading"
            >
                <div className="admin-lookup-card-head">
                    <span className="admin-lookup-icon">
                        <AdminIcon name="key" size={19} />
                    </span>
                    <div className="admin-lookup-copy">
                        <strong id="uuid-lookup-heading">
                            {isVi
                                ? "Tra cứu bãi đỗ xe theo ID"
                                : "Lookup Parking Lot by ID"}
                        </strong>
                        <small>
                            {isVi
                                ? "Dán mã UUID của bãi đỗ xe từ đơn đăng ký để trực tiếp kiểm tra và xét duyệt hồ sơ."
                                : "Paste parking lot UUID from application to directly inspect and review."}
                        </small>
                    </div>
                </div>

                <form
                    className="admin-lookup-form"
                    onSubmit={openById}
                    noValidate
                >
                    <div className="admin-lookup-input-wrap">
                        <input
                            id="lots-lookup-id"
                            value={lookupId}
                            onChange={(e) => {
                                setLookupId(e.target.value);
                                if (lookupError) setLookupError("");
                            }}
                            onPaste={(e) => {
                                const pasted = e.clipboardData
                                    ?.getData("text")
                                    ?.trim();
                                if (pasted) {
                                    e.preventDefault();
                                    setLookupId(pasted);
                                    if (!isParkingLotId(pasted)) {
                                        setLookupError(
                                            isVi
                                                ? "Định dạng mã UUID không hợp lệ (ví dụ: 123e4567-e89b-12d3-a456-426614174000)."
                                                : "Invalid UUID format.",
                                        );
                                    } else {
                                        setLookupError("");
                                    }
                                }
                            }}
                            placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                            aria-invalid={Boolean(lookupError)}
                            disabled={lookupBusy}
                        />
                        <button
                            type="button"
                            className="admin-lookup-paste-btn"
                            onClick={handlePasteUuid}
                            title={
                                isVi
                                    ? "Dán UUID từ bộ nhớ tạm"
                                    : "Paste UUID from clipboard"
                            }
                            disabled={lookupBusy}
                        >
                            <AdminIcon name="clipboard" size={14} />
                            <span>{isVi ? "Dán UUID" : "Paste"}</span>
                        </button>
                    </div>

                    <button
                        type="submit"
                        className="admin-btn admin-btn-dark"
                        id="lots-lookup-open"
                        disabled={lookupBusy}
                    >
                        {lookupBusy ? (
                            <>
                                <AdminIcon
                                    name="refresh"
                                    size={14}
                                    className="admin-spin"
                                />
                                <span>
                                    {isVi ? "Đang tra cứu..." : "Looking up..."}
                                </span>
                            </>
                        ) : (
                            <>
                                <AdminIcon name="search" size={14} />
                                <span>{t("lookupBtn")}</span>
                            </>
                        )}
                    </button>
                </form>

                {lookupError && (
                    <p className="admin-field-error" role="alert">
                        <AdminIcon name="alert" size={14} />
                        <span>{lookupError}</span>
                    </p>
                )}
            </section>

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
                                ? isVi
                                    ? `Duyệt bãi đỗ xe ${selectedLot.name}?`
                                    : `Approve parking lot ${selectedLot.name}?`
                                : isVi
                                  ? `Từ chối bãi đỗ xe ${selectedLot.name}?`
                                  : `Reject parking lot ${selectedLot.name}?`}
                        </h2>
                        <p>
                            {reviewAction === "approve"
                                ? isVi
                                    ? `Bãi đỗ xe sẽ được chuyển sang trạng thái Hoạt động (Approved) và xuất hiện trên danh mục công khai cho khách hàng.`
                                    : `Parking lot will be set to Approved and publicly visible to customers.`
                                : isVi
                                  ? `Vui lòng nhập lý do từ chối để chủ bãi có thể điều chỉnh lại hồ sơ.`
                                  : `Please specify the rejection reason so the owner can correct their application.`}
                        </p>

                        {reviewAction === "reject" && (
                            <label
                                className="admin-field"
                                style={{ marginTop: "1rem" }}
                            >
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
                                {t("cancel")}
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
                                    ? t("saving")
                                    : reviewAction === "approve"
                                      ? isVi
                                          ? "Xác nhận duyệt"
                                          : "Confirm Approval"
                                      : isVi
                                        ? "Xác nhận từ chối"
                                        : "Confirm Rejection"}
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
