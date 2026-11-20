"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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
type ToggleableColumnKey = "owner" | "address" | "date" | "capacity" | "status";
type FilterFieldKey =
    "name" | "owner" | "address" | "type" | "status" | "date" | "capacity";

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
    const [nameFilter, setNameFilter] = useState("");
    const [ownerFilter, setOwnerFilter] = useState("");
    const [addressFilter, setAddressFilter] = useState("");
    const [minCapacity, setMinCapacity] = useState("");

    // Smartbase Filter Builder & Column Visibility UI state
    const [filterMenuOpen, setFilterMenuOpen] = useState(false);
    const [activeBuilderField, setActiveBuilderField] =
        useState<FilterFieldKey | null>(null);
    const [columnsMenuOpen, setColumnsMenuOpen] = useState(false);
    const filterPopoverRef = useRef<HTMLDivElement>(null);
    const columnsPopoverRef = useRef<HTMLDivElement>(null);
    const [visibleColumns, setVisibleColumns] = useState<
        Record<ToggleableColumnKey, boolean>
    >({
        owner: true,
        address: true,
        date: true,
        capacity: true,
        status: true,
    });

    // Close popovers when clicking outside or pressing Escape
    useEffect(() => {
        if (!filterMenuOpen && !columnsMenuOpen) return;
        function handleOutsidePointerDown(e: MouseEvent) {
            const target = e.target as Node;
            if (
                filterMenuOpen &&
                filterPopoverRef.current &&
                !filterPopoverRef.current.contains(target)
            ) {
                setFilterMenuOpen(false);
            }
            if (
                columnsMenuOpen &&
                columnsPopoverRef.current &&
                !columnsPopoverRef.current.contains(target)
            ) {
                setColumnsMenuOpen(false);
            }
        }
        function handleKeyDown(e: KeyboardEvent) {
            if (e.key === "Escape") {
                setFilterMenuOpen(false);
                setColumnsMenuOpen(false);
            }
        }
        document.addEventListener("mousedown", handleOutsidePointerDown);
        document.addEventListener("keydown", handleKeyDown);
        return () => {
            document.removeEventListener("mousedown", handleOutsidePointerDown);
            document.removeEventListener("keydown", handleKeyDown);
        };
    }, [filterMenuOpen, columnsMenuOpen]);

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

    const hasCapacities = useMemo(
        () => lots.some((l) => l.totalCapacity != null),
        [lots],
    );

    const visibleColumnCount = useMemo(
        () => 2 + Object.values(visibleColumns).filter(Boolean).length,
        [visibleColumns],
    );

    const hasActiveFilters = Boolean(
        search.trim() ||
        nameFilter.trim() ||
        ownerFilter.trim() ||
        addressFilter.trim() ||
        minCapacity.trim() ||
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
        setNameFilter("");
        setOwnerFilter("");
        setAddressFilter("");
        setMinCapacity("");
        setTypeFilter("All");
        setSubmittedFilter("all");
        setSortOrder("oldest");
        setActiveBuilderField(null);
        setFilterMenuOpen(false);
        setCurrentPage(1);
        if (mode === "all" && status !== "All") {
            setLoading(true);
            setRawError(null);
            setStatus("All");
        }
    }

    function toggleColumn(col: ToggleableColumnKey) {
        setVisibleColumns((prev) => ({
            ...prev,
            [col]: !prev[col],
        }));
    }

    function resetColumns() {
        setVisibleColumns({
            owner: true,
            address: true,
            date: true,
            capacity: true,
            status: true,
        });
    }

    const rows = useMemo(() => {
        const term = search.trim().toLowerCase();
        const nameTerm = nameFilter.trim().toLowerCase();
        const ownerTerm = ownerFilter.trim().toLowerCase();
        const addrTerm = addressFilter.trim().toLowerCase();
        const minCapValue = minCapacity.trim()
            ? Number(minCapacity.trim())
            : null;

        return lots
            .filter((lot) => status === "All" || lot.status === status)
            .filter((lot) => typeFilter === "All" || lot.type === typeFilter)
            .filter(
                (lot) =>
                    !nameTerm ||
                    (lot.name ?? "").toLowerCase().includes(nameTerm),
            )
            .filter(
                (lot) =>
                    !ownerTerm ||
                    [lot.ownerName, lot.ownerEmail, lot.ownerUserId].some((v) =>
                        (v ?? "").toLowerCase().includes(ownerTerm),
                    ),
            )
            .filter(
                (lot) =>
                    !addrTerm ||
                    (lot.address ?? "").toLowerCase().includes(addrTerm),
            )
            .filter((lot) => {
                if (minCapValue === null || Number.isNaN(minCapValue)) {
                    return true;
                }
                if (lot.totalCapacity == null) return false;
                return lot.totalCapacity >= minCapValue;
            })
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
        nameFilter,
        ownerFilter,
        addressFilter,
        minCapacity,
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
    const notUpdatedText = "—";

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

            {/* Unified Smartbase-Style Enterprise Data Grid Shell */}
            <section
                className="admin-card admin-datagrid-card admin-table-card"
                aria-label={
                    isVi
                        ? "Bảng dữ liệu và thanh công cụ quản lý bãi đỗ xe"
                        : "Parking lots data grid and toolbar"
                }
            >
                {/* STATUS FILTER TABS (Compact horizontal bar when mode === "all") */}
                {mode === "all" && (
                    <div className="admin-datagrid-status-bar">
                        <div
                            className="admin-tabs admin-status-tabs admin-datagrid-view-tabs"
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
                            <span className="admin-datagrid-result-meta">
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
                            </span>
                        )}
                    </div>
                )}

                {/* ONE COMPACT TOOLBAR ROW — Left: Wide Search Input | Right: Filter, Sort, Columns */}
                <div className="admin-datagrid-toolbar-row1">
                    {/* Left: Primary Search Input occupying most of the width */}
                    <label className="admin-search admin-toolbar-search admin-datagrid-search">
                        <AdminIcon name="search" size={15} />
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
                                <AdminIcon name="close" size={12} />
                            </button>
                        )}
                    </label>

                    {/* Right: Filter Button, Sort Control, Columns Button */}
                    <div className="admin-datagrid-toolbar-right">
                        {/* 1. Compact "+ Thêm bộ lọc" Button & Single-Column Anchored Popover */}
                        <div
                            className="admin-popover-anchor"
                            ref={filterPopoverRef}
                        >
                            <button
                                type="button"
                                className={`admin-btn admin-btn-ghost admin-datagrid-tool-btn admin-add-filter-btn ${filterMenuOpen ? "is-open" : ""}`}
                                onClick={() => {
                                    setFilterMenuOpen((prev) => !prev);
                                    setColumnsMenuOpen(false);
                                }}
                                aria-expanded={filterMenuOpen}
                            >
                                <AdminIcon name="filter" size={14} />
                                <span>
                                    {isVi ? "+ Thêm bộ lọc" : "+ Add filter"}
                                </span>
                            </button>

                            {filterMenuOpen && (
                                <div
                                    className="admin-datagrid-popover admin-filter-compact-popover"
                                    role="dialog"
                                    aria-label={
                                        isVi
                                            ? "Thêm điều kiện lọc"
                                            : "Add filter condition"
                                    }
                                >
                                    <div className="admin-popover-head">
                                        <strong>
                                            {isVi
                                                ? "Thêm bộ lọc"
                                                : "Add filter"}
                                        </strong>
                                        {hasActiveFilters && (
                                            <button
                                                type="button"
                                                className="admin-popover-reset"
                                                onClick={handleClearFilters}
                                            >
                                                {isVi ? "Đặt lại" : "Reset all"}
                                            </button>
                                        )}
                                    </div>

                                    <div className="admin-filter-compact-list">
                                        {(
                                            [
                                                {
                                                    key: "name" as const,
                                                    label: isVi
                                                        ? "Tên bãi xe"
                                                        : "Parking lot name",
                                                },
                                                {
                                                    key: "owner" as const,
                                                    label: isVi
                                                        ? "Chủ bãi"
                                                        : "Owner",
                                                },
                                                {
                                                    key: "address" as const,
                                                    label: isVi
                                                        ? "Địa chỉ"
                                                        : "Address",
                                                },
                                                {
                                                    key: "type" as const,
                                                    label: isVi
                                                        ? "Loại bãi"
                                                        : "Lot type",
                                                },
                                                ...(mode === "all"
                                                    ? [
                                                          {
                                                              key: "status" as const,
                                                              label: isVi
                                                                  ? "Trạng thái"
                                                                  : "Status",
                                                          },
                                                      ]
                                                    : []),
                                                {
                                                    key: "date" as const,
                                                    label: isVi
                                                        ? "Ngày nộp"
                                                        : "Submitted date",
                                                },
                                                {
                                                    key: "capacity" as const,
                                                    label: isVi
                                                        ? "Sức chứa"
                                                        : "Capacity",
                                                },
                                            ] as const
                                        ).map((field) => {
                                            const isExpanded =
                                                activeBuilderField ===
                                                field.key;
                                            return (
                                                <div
                                                    key={field.key}
                                                    className={`admin-filter-accordion-item ${isExpanded ? "is-expanded" : ""}`}
                                                >
                                                    <button
                                                        type="button"
                                                        className="admin-builder-field-btn"
                                                        onClick={() =>
                                                            setActiveBuilderField(
                                                                isExpanded
                                                                    ? null
                                                                    : field.key,
                                                            )
                                                        }
                                                    >
                                                        <span>
                                                            {field.label}
                                                        </span>
                                                        <AdminIcon
                                                            name={
                                                                isExpanded
                                                                    ? "chevronDown"
                                                                    : "chevronRight"
                                                            }
                                                            size={13}
                                                        />
                                                    </button>

                                                    {isExpanded && (
                                                        <div className="admin-filter-accordion-body">
                                                            {field.key ===
                                                                "name" && (
                                                                <input
                                                                    id="filter-builder-name"
                                                                    type="text"
                                                                    autoFocus
                                                                    value={
                                                                        nameFilter
                                                                    }
                                                                    onChange={(
                                                                        e,
                                                                    ) => {
                                                                        setNameFilter(
                                                                            e
                                                                                .target
                                                                                .value,
                                                                        );
                                                                        setCurrentPage(
                                                                            1,
                                                                        );
                                                                    }}
                                                                    placeholder={
                                                                        isVi
                                                                            ? "Nhập tên bãi xe..."
                                                                            : "Enter lot name..."
                                                                    }
                                                                />
                                                            )}

                                                            {field.key ===
                                                                "owner" && (
                                                                <input
                                                                    id="filter-builder-owner"
                                                                    type="text"
                                                                    autoFocus
                                                                    value={
                                                                        ownerFilter
                                                                    }
                                                                    onChange={(
                                                                        e,
                                                                    ) => {
                                                                        setOwnerFilter(
                                                                            e
                                                                                .target
                                                                                .value,
                                                                        );
                                                                        setCurrentPage(
                                                                            1,
                                                                        );
                                                                    }}
                                                                    placeholder={
                                                                        isVi
                                                                            ? "Nhập tên, email hoặc ID chủ bãi..."
                                                                            : "Enter owner name or ID..."
                                                                    }
                                                                />
                                                            )}

                                                            {field.key ===
                                                                "address" && (
                                                                <input
                                                                    id="filter-builder-address"
                                                                    type="text"
                                                                    autoFocus
                                                                    value={
                                                                        addressFilter
                                                                    }
                                                                    onChange={(
                                                                        e,
                                                                    ) => {
                                                                        setAddressFilter(
                                                                            e
                                                                                .target
                                                                                .value,
                                                                        );
                                                                        setCurrentPage(
                                                                            1,
                                                                        );
                                                                    }}
                                                                    placeholder={
                                                                        isVi
                                                                            ? "Nhập tên đường, quận/huyện..."
                                                                            : "Enter street or district..."
                                                                    }
                                                                />
                                                            )}

                                                            {field.key ===
                                                                "type" && (
                                                                <div className="admin-builder-options">
                                                                    <button
                                                                        type="button"
                                                                        className={`admin-builder-opt ${typeFilter === "All" ? "is-selected" : ""}`}
                                                                        onClick={() => {
                                                                            setTypeFilter(
                                                                                "All",
                                                                            );
                                                                            setCurrentPage(
                                                                                1,
                                                                            );
                                                                        }}
                                                                    >
                                                                        {isVi
                                                                            ? "Tất cả loại bãi"
                                                                            : "All types"}
                                                                    </button>
                                                                    {availableTypes.map(
                                                                        (
                                                                            type,
                                                                        ) => (
                                                                            <button
                                                                                key={
                                                                                    type
                                                                                }
                                                                                type="button"
                                                                                className={`admin-builder-opt ${typeFilter === type ? "is-selected" : ""}`}
                                                                                onClick={() => {
                                                                                    setTypeFilter(
                                                                                        type,
                                                                                    );
                                                                                    setCurrentPage(
                                                                                        1,
                                                                                    );
                                                                                }}
                                                                            >
                                                                                {
                                                                                    type
                                                                                }
                                                                            </button>
                                                                        ),
                                                                    )}
                                                                </div>
                                                            )}

                                                            {field.key ===
                                                                "status" &&
                                                                mode ===
                                                                    "all" && (
                                                                    <div className="admin-builder-options">
                                                                        {tabs.map(
                                                                            (
                                                                                tab,
                                                                            ) => (
                                                                                <button
                                                                                    key={
                                                                                        tab
                                                                                    }
                                                                                    type="button"
                                                                                    className={`admin-builder-opt ${status === tab ? "is-selected" : ""}`}
                                                                                    onClick={() =>
                                                                                        handleStatusTabChange(
                                                                                            tab,
                                                                                        )
                                                                                    }
                                                                                >
                                                                                    {tab ===
                                                                                    "All"
                                                                                        ? t(
                                                                                              "all",
                                                                                          )
                                                                                        : tab ===
                                                                                            "Pending"
                                                                                          ? t(
                                                                                                "pending",
                                                                                            )
                                                                                          : tab ===
                                                                                              "Approved"
                                                                                            ? t(
                                                                                                  "approved",
                                                                                              )
                                                                                            : t(
                                                                                                  "rejected",
                                                                                              )}
                                                                                </button>
                                                                            ),
                                                                        )}
                                                                    </div>
                                                                )}

                                                            {field.key ===
                                                                "date" &&
                                                                (hasSubmittedDates ? (
                                                                    <div className="admin-builder-options">
                                                                        {(
                                                                            [
                                                                                {
                                                                                    val: "all" as const,
                                                                                    label: isVi
                                                                                        ? "Mọi ngày gửi"
                                                                                        : "Any date",
                                                                                },
                                                                                {
                                                                                    val: "today" as const,
                                                                                    label: isVi
                                                                                        ? "Trong 24 giờ qua"
                                                                                        : "Last 24h",
                                                                                },
                                                                                {
                                                                                    val: "7days" as const,
                                                                                    label: isVi
                                                                                        ? "7 ngày gần đây"
                                                                                        : "Last 7 days",
                                                                                },
                                                                                {
                                                                                    val: "30days" as const,
                                                                                    label: isVi
                                                                                        ? "30 ngày gần đây"
                                                                                        : "Last 30 days",
                                                                                },
                                                                            ] as const
                                                                        ).map(
                                                                            (
                                                                                opt,
                                                                            ) => (
                                                                                <button
                                                                                    key={
                                                                                        opt.val
                                                                                    }
                                                                                    type="button"
                                                                                    className={`admin-builder-opt ${submittedFilter === opt.val ? "is-selected" : ""}`}
                                                                                    onClick={() => {
                                                                                        setSubmittedFilter(
                                                                                            opt.val,
                                                                                        );
                                                                                        setCurrentPage(
                                                                                            1,
                                                                                        );
                                                                                    }}
                                                                                >
                                                                                    {
                                                                                        opt.label
                                                                                    }
                                                                                </button>
                                                                            ),
                                                                        )}
                                                                    </div>
                                                                ) : (
                                                                    <p className="admin-builder-unavail">
                                                                        {isVi
                                                                            ? "API danh sách hiện chưa trả về ngày nộp."
                                                                            : "Submitted date not provided by list API."}
                                                                    </p>
                                                                ))}

                                                            {field.key ===
                                                                "capacity" &&
                                                                (hasCapacities ? (
                                                                    <input
                                                                        id="filter-builder-capacity"
                                                                        type="number"
                                                                        min={0}
                                                                        autoFocus
                                                                        value={
                                                                            minCapacity
                                                                        }
                                                                        onChange={(
                                                                            e,
                                                                        ) => {
                                                                            setMinCapacity(
                                                                                e
                                                                                    .target
                                                                                    .value,
                                                                            );
                                                                            setCurrentPage(
                                                                                1,
                                                                            );
                                                                        }}
                                                                        placeholder={
                                                                            isVi
                                                                                ? "Tối thiểu (VD: 50)"
                                                                                : "Min slots (e.g. 50)"
                                                                        }
                                                                    />
                                                                ) : (
                                                                    <p className="admin-builder-unavail">
                                                                        {isVi
                                                                            ? "API danh sách hiện chưa trả về sức chứa."
                                                                            : "Total capacity not provided by list API."}
                                                                    </p>
                                                                ))}
                                                        </div>
                                                    )}
                                                </div>
                                            );
                                        })}
                                    </div>
                                </div>
                            )}
                        </div>

                        {/* 2. Columns Visibility Button & Compact Popover */}
                        <div
                            className="admin-popover-anchor"
                            ref={columnsPopoverRef}
                        >
                            <button
                                type="button"
                                className={`admin-btn admin-btn-ghost admin-datagrid-tool-btn admin-datagrid-columns-btn ${columnsMenuOpen ? "is-open" : ""}`}
                                onClick={() => {
                                    setColumnsMenuOpen((prev) => !prev);
                                    setFilterMenuOpen(false);
                                }}
                                aria-expanded={columnsMenuOpen}
                                aria-label={
                                    isVi ? "Cột hiển thị" : "Toggle columns"
                                }
                            >
                                <AdminIcon name="grid" size={14} />
                                <span>{isVi ? "Cột hiển thị" : "Columns"}</span>
                                <span className="admin-columns-count">
                                    {visibleColumnCount}/7
                                </span>
                            </button>

                            {columnsMenuOpen && (
                                <div
                                    className="admin-datagrid-popover admin-columns-popover"
                                    role="dialog"
                                    aria-label={
                                        isVi
                                            ? "Tùy chỉnh cột hiển thị"
                                            : "Customize visible columns"
                                    }
                                >
                                    <div className="admin-popover-head">
                                        <strong>
                                            {isVi
                                                ? "Cột hiển thị"
                                                : "Visible columns"}
                                        </strong>
                                        {visibleColumnCount < 7 && (
                                            <button
                                                type="button"
                                                className="admin-popover-reset"
                                                onClick={resetColumns}
                                            >
                                                {isVi
                                                    ? "Hiện tất cả"
                                                    : "Reset all"}
                                            </button>
                                        )}
                                    </div>
                                    <div className="admin-columns-list">
                                        <label className="admin-column-item is-locked">
                                            <input
                                                type="checkbox"
                                                checked
                                                disabled
                                            />
                                            <span>{t("lotNameHeader")}</span>
                                        </label>
                                        {(
                                            [
                                                {
                                                    key: "owner" as const,
                                                    label: t("lotOwnerHeader"),
                                                },
                                                {
                                                    key: "address" as const,
                                                    label: isVi
                                                        ? "Địa chỉ"
                                                        : "Address",
                                                },
                                                {
                                                    key: "date" as const,
                                                    label: isVi
                                                        ? "Ngày nộp"
                                                        : "Submitted Date",
                                                },
                                                {
                                                    key: "capacity" as const,
                                                    label: t(
                                                        "lotCapacityHeader",
                                                    ),
                                                },
                                                {
                                                    key: "status" as const,
                                                    label: t("status"),
                                                },
                                            ] as const
                                        ).map((col) => (
                                            <label
                                                key={col.key}
                                                className="admin-column-item"
                                            >
                                                <input
                                                    type="checkbox"
                                                    checked={
                                                        visibleColumns[col.key]
                                                    }
                                                    onChange={() =>
                                                        toggleColumn(col.key)
                                                    }
                                                />
                                                <span>{col.label}</span>
                                            </label>
                                        ))}
                                        <label className="admin-column-item is-locked">
                                            <input
                                                type="checkbox"
                                                checked
                                                disabled
                                            />
                                            <span>{t("actions")}</span>
                                        </label>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                {/* CONDITIONAL ACTIVE FILTER CHIPS ROW — Only shown when filters are active */}
                {hasActiveFilters && (
                    <div className="admin-datagrid-filter-row">
                        <div className="admin-filter-chips-wrap">
                            {mode === "all" && status !== "All" && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Trạng thái:" : "Status:"}
                                    </span>
                                    <strong>
                                        {status === "Pending"
                                            ? t("pending")
                                            : status === "Approved"
                                              ? t("approved")
                                              : t("rejected")}
                                    </strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() =>
                                            handleStatusTabChange("All")
                                        }
                                        aria-label={
                                            isVi
                                                ? "Gỡ lọc trạng thái"
                                                : "Remove status filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}

                            {typeFilter !== "All" && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Loại bãi:" : "Type:"}
                                    </span>
                                    <strong>{typeFilter}</strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() => {
                                            setTypeFilter("All");
                                            setCurrentPage(1);
                                        }}
                                        aria-label={
                                            isVi
                                                ? "Gỡ lọc loại bãi"
                                                : "Remove type filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}

                            {nameFilter.trim() && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Tên bãi xe:" : "Name:"}
                                    </span>
                                    <strong>{nameFilter.trim()}</strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() => {
                                            setNameFilter("");
                                            setCurrentPage(1);
                                        }}
                                        aria-label={
                                            isVi
                                                ? "Gỡ lọc tên bãi xe"
                                                : "Remove name filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}

                            {ownerFilter.trim() && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Chủ bãi:" : "Owner:"}
                                    </span>
                                    <strong>{ownerFilter.trim()}</strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() => {
                                            setOwnerFilter("");
                                            setCurrentPage(1);
                                        }}
                                        aria-label={
                                            isVi
                                                ? "Gỡ lọc chủ bãi"
                                                : "Remove owner filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}

                            {addressFilter.trim() && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Địa chỉ:" : "Address:"}
                                    </span>
                                    <strong>{addressFilter.trim()}</strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() => {
                                            setAddressFilter("");
                                            setCurrentPage(1);
                                        }}
                                        aria-label={
                                            isVi
                                                ? "Gỡ lọc địa chỉ"
                                                : "Remove address filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}

                            {submittedFilter !== "all" && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Ngày nộp:" : "Submitted:"}
                                    </span>
                                    <strong>
                                        {submittedFilter === "today"
                                            ? isVi
                                                ? "Trong 24h qua"
                                                : "Last 24h"
                                            : submittedFilter === "7days"
                                              ? isVi
                                                  ? "7 ngày gần đây"
                                                  : "Last 7 days"
                                              : isVi
                                                ? "30 ngày gần đây"
                                                : "Last 30 days"}
                                    </strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() => {
                                            setSubmittedFilter("all");
                                            setCurrentPage(1);
                                        }}
                                        aria-label={
                                            isVi
                                                ? "Gỡ lọc ngày nộp"
                                                : "Remove date filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}

                            {minCapacity.trim() && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Sức chứa:" : "Capacity:"}
                                    </span>
                                    <strong>
                                        ≥ {minCapacity.trim()}{" "}
                                        {isVi ? "chỗ" : "slots"}
                                    </strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() => {
                                            setMinCapacity("");
                                            setCurrentPage(1);
                                        }}
                                        aria-label={
                                            isVi
                                                ? "Gỡ lọc sức chứa"
                                                : "Remove capacity filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}

                            {search.trim() && (
                                <span className="admin-filter-chip">
                                    <span className="admin-filter-chip-label">
                                        {isVi ? "Từ khóa:" : "Keyword:"}
                                    </span>
                                    <strong>
                                        &ldquo;{search.trim()}&rdquo;
                                    </strong>
                                    <button
                                        type="button"
                                        className="admin-filter-chip-remove"
                                        onClick={() => {
                                            setSearch("");
                                            setCurrentPage(1);
                                        }}
                                        aria-label={
                                            isVi
                                                ? "Gỡ từ khóa tìm kiếm"
                                                : "Remove keyword filter"
                                        }
                                    >
                                        <AdminIcon name="close" size={11} />
                                    </button>
                                </span>
                            )}
                        </div>
                    </div>
                )}

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
                            <table className="admin-table admin-lots-table admin-datagrid-table">
                                <thead>
                                    <tr>
                                        <th className="col-lot-name">
                                            {t("lotNameHeader")}
                                        </th>
                                        {visibleColumns.owner && (
                                            <th className="col-lot-owner">
                                                {t("lotOwnerHeader")}
                                            </th>
                                        )}
                                        {visibleColumns.address && (
                                            <th className="col-lot-address">
                                                {isVi ? "Địa chỉ" : "Address"}
                                            </th>
                                        )}
                                        {visibleColumns.date && (
                                            <th className="col-lot-date">
                                                {isVi
                                                    ? "Ngày nộp"
                                                    : "Submitted Date"}
                                            </th>
                                        )}
                                        {visibleColumns.capacity && (
                                            <th className="col-lot-capacity">
                                                {t("lotCapacityHeader")}
                                            </th>
                                        )}
                                        {visibleColumns.status && (
                                            <th className="col-lot-status">
                                                {t("status")}
                                            </th>
                                        )}
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
                                            {visibleColumns.owner && (
                                                <td>
                                                    <div className="admin-skel-line admin-skel-md" />
                                                </td>
                                            )}
                                            {visibleColumns.address && (
                                                <td>
                                                    <div className="admin-skel-line admin-skel-md" />
                                                </td>
                                            )}
                                            {visibleColumns.date && (
                                                <td>
                                                    <div className="admin-skel-line admin-skel-sm" />
                                                </td>
                                            )}
                                            {visibleColumns.capacity && (
                                                <td>
                                                    <div className="admin-skel-line admin-skel-sm" />
                                                </td>
                                            )}
                                            {visibleColumns.status && (
                                                <td>
                                                    <div className="admin-skel-pill" />
                                                </td>
                                            )}
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

                {/* STATE 3: SUCCESS + DATA — Compact Enterprise Data Grid */}
                {!loading && !hasError && rows.length > 0 && (
                    <>
                        <div className="admin-table-scroll">
                            <table className="admin-table admin-lots-table admin-datagrid-table">
                                <thead>
                                    <tr>
                                        <th className="col-lot-name">
                                            {t("lotNameHeader")}
                                        </th>
                                        {visibleColumns.owner && (
                                            <th className="col-lot-owner">
                                                {t("lotOwnerHeader")}
                                            </th>
                                        )}
                                        {visibleColumns.address && (
                                            <th className="col-lot-address">
                                                {isVi ? "Địa chỉ" : "Address"}
                                            </th>
                                        )}
                                        {visibleColumns.date && (
                                            <th className="col-lot-date">
                                                {isVi
                                                    ? "Ngày nộp"
                                                    : "Submitted Date"}
                                            </th>
                                        )}
                                        {visibleColumns.capacity && (
                                            <th className="col-lot-capacity">
                                                {t("lotCapacityHeader")}
                                            </th>
                                        )}
                                        {visibleColumns.status && (
                                            <th className="col-lot-status">
                                                {t("status")}
                                            </th>
                                        )}
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
                                                {/* 1. Tên bãi xe */}
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
                                                {visibleColumns.owner && (
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
                                                )}

                                                {/* 3. Địa chỉ */}
                                                {visibleColumns.address && (
                                                    <td className="admin-address-cell">
                                                        {addressText ? (
                                                            <div
                                                                className="admin-address-wrap"
                                                                title={
                                                                    hasCoords
                                                                        ? `${addressText} • GPS: ${lot.latitude?.toFixed(6)}, ${lot.longitude?.toFixed(6)}`
                                                                        : addressText
                                                                }
                                                            >
                                                                <AdminIcon
                                                                    name="pin"
                                                                    size={13}
                                                                    className="admin-cell-icon"
                                                                />
                                                                <span className="admin-address-line">
                                                                    {
                                                                        addressText
                                                                    }
                                                                </span>
                                                            </div>
                                                        ) : (
                                                            <span className="admin-cell-unupdated">
                                                                {notUpdatedText}
                                                            </span>
                                                        )}
                                                    </td>
                                                )}

                                                {/* 4. Ngày nộp */}
                                                {visibleColumns.date && (
                                                    <td className="admin-lot-cell-date">
                                                        {lot.submittedAtUtc ? (
                                                            <div className="admin-date-cell">
                                                                <span className="admin-date-line">
                                                                    <AdminIcon
                                                                        name="calendar"
                                                                        size={
                                                                            13
                                                                        }
                                                                        className="admin-cell-icon"
                                                                    />
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
                                                )}

                                                {/* 5. Sức chứa */}
                                                {visibleColumns.capacity && (
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
                                                )}

                                                {/* 6. Trạng thái */}
                                                {visibleColumns.status && (
                                                    <td className="admin-lot-cell-status">
                                                        <StatusPill
                                                            status={lot.status}
                                                        />
                                                    </td>
                                                )}

                                                {/* 7. Thao tác */}
                                                <td className="admin-table-action">
                                                    <div className="admin-row-actions">
                                                        {mode === "pending" &&
                                                            lot.status ===
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
                                                                {mode ===
                                                                    "pending" &&
                                                                lot.status ===
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

                        <div className="admin-pager admin-datagrid-pager">
                            <span className="admin-pager-meta">
                                {isVi
                                    ? `Hiển thị ${(safePage - 1) * PAGE_SIZE + 1}–${Math.min(safePage * PAGE_SIZE, rows.length)} trong tổng số ${rows.length} hồ sơ • Trang ${safePage} / ${totalPages}`
                                    : `Showing ${(safePage - 1) * PAGE_SIZE + 1}–${Math.min(safePage * PAGE_SIZE, rows.length)} of ${rows.length} lots • Page ${safePage} of ${totalPages}`}
                            </span>
                            {totalPages > 1 && (
                                <div className="admin-pager-controls">
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
                                        <AdminIcon
                                            name="chevronLeft"
                                            size={14}
                                        />
                                        {isVi ? "Trước" : "Previous"}
                                    </button>
                                    {Array.from(
                                        { length: totalPages },
                                        (_, i) => i + 1,
                                    ).map((pageNum) => (
                                        <button
                                            key={pageNum}
                                            type="button"
                                            className={`admin-pager-btn ${pageNum === safePage ? "is-active" : ""}`}
                                            onClick={() =>
                                                setCurrentPage(pageNum)
                                            }
                                        >
                                            {pageNum}
                                        </button>
                                    ))}
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
                                        <AdminIcon
                                            name="chevronRight"
                                            size={14}
                                        />
                                    </button>
                                </div>
                            )}
                        </div>
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
                <div className="admin-lookup-row">
                    <div className="admin-lookup-card-head">
                        <span className="admin-lookup-icon">
                            <AdminIcon name="key" size={18} />
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
                            className="admin-btn admin-btn-dark admin-lookup-submit-btn"
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
                                        {isVi
                                            ? "Đang tra cứu..."
                                            : "Looking up..."}
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
                </div>

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
