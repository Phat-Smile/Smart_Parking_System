"use client";

import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { AdminIcon, formatDate } from "./AdminUi";
import {
    activateAccount,
    changeAccountRole,
    describeAccountError,
    searchAccounts,
    suspendAccount,
    unlockAccount,
    type AccountRole,
    type AccountStatus,
    type AccountSummary,
} from "./adminAccounts";
import { useLanguage } from "@/shared/i18n/LanguageContext";

const roles: AccountRole[] = [
    "Customer",
    "ParkingLotOwner",
    "Staff",
    "SystemAdmin",
];

const statuses: AccountStatus[] = [
    "PendingEmailVerification",
    "EmailVerified",
    "Suspended",
    "Disabled",
];

const PAGE_SIZE = 20;

interface PendingConfirmAction {
    type: "suspend" | "activate" | "unlock" | "role";
    account: AccountSummary;
    newRole?: AccountRole;
}

function getInitials(name?: string | null, email?: string | null): string {
    const source = (name || email || "U").trim();
    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
        return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
    }
    return source.slice(0, 2).toUpperCase();
}

/** Only actions Identity actually exposes: list, suspend, activate, unlock, change role. */
export function AdminUsersPage() {
    const { t, language } = useLanguage();
    const isVi = language === "vi";
    const client = useQueryClient();

    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");
    const [role, setRole] = useState<AccountRole | "">("");
    const [status, setStatus] = useState<AccountStatus | "">("");
    const [page, setPage] = useState(1);
    const [busyId, setBusyId] = useState("");
    const [pendingAction, setPendingAction] =
        useState<PendingConfirmAction | null>(null);
    const [message, setMessage] = useState<{
        kind: "ok" | "error";
        text: string;
    } | null>(null);

    useEffect(() => {
        const timer = window.setTimeout(() => {
            setSearch(searchInput);
            setPage(1);
        }, 350);
        return () => window.clearTimeout(timer);
    }, [searchInput]);

    const query = useQuery({
        queryKey: ["admin-accounts", search, role, status, page],
        queryFn: () =>
            searchAccounts({ search, role, status, page, pageSize: PAGE_SIZE }),
        enabled: Boolean(session),
    });

    const roleDisplay = useMemo<Record<AccountRole, string>>(
        () => ({
            Customer: isVi ? "Khách hàng" : "Customer",
            ParkingLotOwner: isVi ? "Chủ bãi" : "Owner",
            Staff: isVi ? "Nhân viên" : "Staff",
            SystemAdmin: isVi ? "Quản trị viên" : "Admin",
        }),
        [isVi],
    );

    const statusDisplay = useMemo<
        Record<
            AccountStatus,
            {
                label: string;
                tone: "green" | "amber" | "red" | "neutral";
                groupLabel: string;
            }
        >
    >(
        () => ({
            EmailVerified: {
                label: isVi ? "Đã xác minh" : "Email Verified",
                tone: "green",
                groupLabel: isVi ? "Xác minh email" : "Email verification",
            },
            PendingEmailVerification: {
                label: isVi
                    ? "Chờ xác minh email"
                    : "Pending Email Verification",
                tone: "amber",
                groupLabel: isVi ? "Xác minh email" : "Email verification",
            },
            Suspended: {
                label: isVi ? "Đang bị đình chỉ" : "Suspended",
                tone: "red",
                groupLabel: isVi ? "Trạng thái tài khoản" : "Account status",
            },
            Disabled: {
                label: isVi ? "Đã vô hiệu hóa" : "Disabled",
                tone: "neutral",
                groupLabel: isVi ? "Trạng thái tài khoản" : "Account status",
            },
        }),
        [isVi],
    );

    const data = query.data;
    const pages = data
        ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
        : 1;

    const hasActiveFilters = Boolean(searchInput.trim() || role || status);

    // Only compute full breakdown statistics when all system accounts are present in the unfiltered response
    // (never extrapolate system-wide role counts from a single paginated slice).
    const completeSystemStats = useMemo(() => {
        if (!data) return null;
        const isUnfiltered = !search.trim() && !role && !status;
        const isCompleteDataset =
            isUnfiltered &&
            data.page === 1 &&
            data.totalCount === data.items.length;

        if (!isCompleteDataset) {
            return {
                isComplete: false as const,
                totalCount: data.totalCount,
            };
        }

        const customerCount = data.items.filter(
            (a) => a.role === "Customer",
        ).length;
        const ownerCount = data.items.filter(
            (a) => a.role === "ParkingLotOwner",
        ).length;
        const restrictedOrUnverifiedCount = data.items.filter(
            (a) =>
                a.isLocked ||
                a.status === "Suspended" ||
                a.status === "Disabled" ||
                a.status === "PendingEmailVerification",
        ).length;

        return {
            isComplete: true as const,
            totalCount: data.totalCount,
            customerCount,
            ownerCount,
            restrictedOrUnverifiedCount,
        };
    }, [data, role, search, status]);

    function handleClearFilters() {
        setSearchInput("");
        setSearch("");
        setRole("");
        setStatus("");
        setPage(1);
    }

    async function run(
        account: AccountSummary,
        action: "suspend" | "activate" | "unlock",
    ) {
        setBusyId(account.id);
        setMessage(null);
        try {
            if (action === "suspend") await suspendAccount(account.id);
            if (action === "activate") await activateAccount(account.id);
            if (action === "unlock") await unlockAccount(account.id);
            const actionText = isVi
                ? action === "unlock"
                    ? "Đã mở khóa đăng nhập"
                    : action === "suspend"
                      ? "Đã đình chỉ tài khoản"
                      : "Đã kích hoạt lại tài khoản"
                : action === "unlock"
                  ? "unlocked"
                  : action === "suspend"
                    ? "suspended"
                    : "activated";
            setMessage({
                kind: "ok",
                text: `${account.email}: ${actionText}.`,
            });
            setPendingAction(null);
        } catch (error) {
            setMessage({
                kind: "error",
                text: describeAccountError(error, language),
            });
            setPendingAction(null);
        } finally {
            setBusyId("");
            await client.invalidateQueries({ queryKey: ["admin-accounts"] });
        }
    }

    async function updateRole(account: AccountSummary, newRole: AccountRole) {
        if (newRole === account.role) return;
        setBusyId(account.id);
        setMessage(null);
        try {
            await changeAccountRole(account.id, newRole);
            setMessage({
                kind: "ok",
                text: isVi
                    ? `${account.email}: Đã cập nhật vai trò thành ${roleDisplay[newRole]}.`
                    : `${account.email}: role changed to ${roleDisplay[newRole]}.`,
            });
            setPendingAction(null);
        } catch (error) {
            setMessage({
                kind: "error",
                text: describeAccountError(error, language),
            });
            setPendingAction(null);
        } finally {
            setBusyId("");
            await client.invalidateQueries({ queryKey: ["admin-accounts"] });
        }
    }

    async function handleConfirmPendingAction() {
        if (!pendingAction) return;
        if (pendingAction.type === "role" && pendingAction.newRole) {
            await updateRole(pendingAction.account, pendingAction.newRole);
        } else if (pendingAction.type !== "role") {
            await run(pendingAction.account, pendingAction.type);
        }
    }

    const rangeStart =
        data && data.totalCount > 0 ? (page - 1) * data.pageSize + 1 : 0;
    const rangeEnd = data ? Math.min(page * data.pageSize, data.totalCount) : 0;

    return (
        <div className="admin-users-page">
            {/* A. Page Header */}
            <header className="admin-heading admin-users-header">
                <div className="admin-users-header-copy">
                    <p className="admin-eyebrow">{t("adminUsersEyebrow")}</p>
                    <h1>{t("adminUsersTitle")}</h1>
                    <p className="admin-users-subtitle">
                        {isVi
                            ? "Quản lý tài khoản, vai trò và trạng thái người dùng trong hệ thống ParkWise."
                            : "Manage user accounts, roles, and account statuses across the ParkWise system."}
                    </p>
                </div>

                <div className="admin-users-header-actions">
                    <Link
                        href="/admin/owner-applications"
                        className="admin-btn admin-btn-ghost"
                    >
                        <AdminIcon name="fileCheck" size={15} />
                        <span>{t("adminOwnerApplications")}</span>
                    </Link>
                    <button
                        type="button"
                        className="admin-btn admin-btn-ghost"
                        onClick={() => query.refetch()}
                        disabled={query.isFetching || !session}
                    >
                        <AdminIcon name="refresh" size={15} />
                        <span>{t("refresh")}</span>
                    </button>
                </div>
            </header>

            {/* B. Statistic Cards (Strictly from verified API data) */}
            {completeSystemStats && (
                <section
                    className={`admin-users-kpi-grid ${completeSystemStats.isComplete ? "is-four" : "is-single"}`}
                    aria-label={
                        isVi ? "Thống kê tài khoản" : "Account statistics"
                    }
                >
                    <article className="admin-card admin-users-kpi-card">
                        <div className="admin-users-kpi-icon is-neutral">
                            <AdminIcon name="users" size={18} />
                        </div>
                        <div className="admin-users-kpi-body">
                            <span className="admin-users-kpi-label">
                                {hasActiveFilters
                                    ? isVi
                                        ? "Tài khoản khớp bộ lọc"
                                        : "Matching Accounts"
                                    : isVi
                                      ? "Tổng tài khoản"
                                      : "Total Accounts"}
                            </span>
                            <strong className="admin-users-kpi-value">
                                {completeSystemStats.totalCount}
                            </strong>
                            <small className="admin-users-kpi-desc">
                                {hasActiveFilters
                                    ? isVi
                                        ? "Kết quả từ truy vấn hiện tại"
                                        : "Results from active filters"
                                    : isVi
                                      ? "Toàn bộ tài khoản trong hệ thống"
                                      : "All registered accounts"}
                            </small>
                        </div>
                    </article>

                    {completeSystemStats.isComplete && (
                        <>
                            <article className="admin-card admin-users-kpi-card">
                                <div className="admin-users-kpi-icon is-blue">
                                    <AdminIcon name="user" size={18} />
                                </div>
                                <div className="admin-users-kpi-body">
                                    <span className="admin-users-kpi-label">
                                        {isVi ? "Khách hàng" : "Customers"}
                                    </span>
                                    <strong className="admin-users-kpi-value is-blue">
                                        {completeSystemStats.customerCount}
                                    </strong>
                                    <small className="admin-users-kpi-desc">
                                        {isVi
                                            ? "Người dùng đặt chỗ & gửi xe"
                                            : "Drivers & booking users"}
                                    </small>
                                </div>
                            </article>

                            <article className="admin-card admin-users-kpi-card">
                                <div className="admin-users-kpi-icon is-green">
                                    <AdminIcon name="briefcase" size={18} />
                                </div>
                                <div className="admin-users-kpi-body">
                                    <span className="admin-users-kpi-label">
                                        {isVi ? "Chủ bãi" : "Parking Owners"}
                                    </span>
                                    <strong className="admin-users-kpi-value is-green">
                                        {completeSystemStats.ownerCount}
                                    </strong>
                                    <small className="admin-users-kpi-desc">
                                        {isVi
                                            ? "Đối tác vận hành bãi đỗ xe"
                                            : "Verified partner accounts"}
                                    </small>
                                </div>
                            </article>

                            <article className="admin-card admin-users-kpi-card">
                                <div className="admin-users-kpi-icon is-amber">
                                    <AdminIcon name="shield" size={18} />
                                </div>
                                <div className="admin-users-kpi-body">
                                    <span className="admin-users-kpi-label">
                                        {isVi
                                            ? "Bị khóa / Chờ xác minh"
                                            : "Locked / Unverified"}
                                    </span>
                                    <strong className="admin-users-kpi-value is-amber">
                                        {
                                            completeSystemStats.restrictedOrUnverifiedCount
                                        }
                                    </strong>
                                    <small className="admin-users-kpi-desc">
                                        {isVi
                                            ? "Đình chỉ, khóa hoặc chờ xác thực email"
                                            : "Suspended, locked or pending email"}
                                    </small>
                                </div>
                            </article>
                        </>
                    )}
                </section>
            )}

            {/* C. Search & Filter Toolbar */}
            <section
                className="admin-card admin-users-toolbar-card"
                aria-label={
                    isVi
                        ? "Thanh tìm kiếm và bộ lọc tài khoản"
                        : "Search and filter accounts"
                }
            >
                <div className="admin-users-toolbar-row">
                    <label
                        className="admin-users-search-box"
                        htmlFor="users-search"
                    >
                        <AdminIcon name="search" size={16} />
                        <input
                            id="users-search"
                            type="text"
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            placeholder={t("adminUsersSearchPlaceholder")}
                        />
                        {searchInput && (
                            <button
                                type="button"
                                className="admin-search-clear-btn"
                                onClick={() => setSearchInput("")}
                                title={isVi ? "Xóa từ khóa" : "Clear search"}
                            >
                                ✕
                            </button>
                        )}
                    </label>

                    <div className="admin-users-filter-controls">
                        <div className="admin-users-select-wrap">
                            <span className="admin-users-select-label">
                                {isVi ? "Vai trò:" : "Role:"}
                            </span>
                            <select
                                id="users-role"
                                className="admin-select"
                                value={role}
                                onChange={(e) => {
                                    setRole(e.target.value as AccountRole | "");
                                    setPage(1);
                                }}
                            >
                                <option value="">
                                    {t("adminUsersAllRoles")}
                                </option>
                                {roles.map((r) => (
                                    <option key={r} value={r}>
                                        {roleDisplay[r]}
                                    </option>
                                ))}
                            </select>
                        </div>

                        <div className="admin-users-select-wrap">
                            <span className="admin-users-select-label">
                                {isVi ? "Trạng thái:" : "Status:"}
                            </span>
                            <select
                                id="users-status"
                                className="admin-select"
                                value={status}
                                onChange={(e) => {
                                    setStatus(
                                        e.target.value as AccountStatus | "",
                                    );
                                    setPage(1);
                                }}
                            >
                                <option value="">
                                    {t("adminUsersAllStatuses")}
                                </option>
                                {statuses.map((s) => (
                                    <option key={s} value={s}>
                                        {statusDisplay[s].label}
                                    </option>
                                ))}
                            </select>
                        </div>

                        {hasActiveFilters && (
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost admin-clear-filter-btn"
                                onClick={handleClearFilters}
                            >
                                <span>
                                    {isVi ? "Xóa bộ lọc" : "Clear filters"}
                                </span>
                            </button>
                        )}
                    </div>
                </div>

                <div className="admin-users-toolbar-meta">
                    <span>
                        {data ? (
                            isVi ? (
                                <>
                                    Tìm thấy <strong>{data.totalCount}</strong>{" "}
                                    tài khoản
                                    {data.totalCount > 0 && (
                                        <>
                                            {" "}
                                            (hiển thị {rangeStart}–{rangeEnd})
                                        </>
                                    )}
                                </>
                            ) : (
                                <>
                                    Found <strong>{data.totalCount}</strong>{" "}
                                    accounts
                                    {data.totalCount > 0 && (
                                        <>
                                            {" "}
                                            (showing {rangeStart}–{rangeEnd})
                                        </>
                                    )}
                                </>
                            )
                        ) : isVi ? (
                            "Đang đồng bộ danh sách tài khoản..."
                        ) : (
                            "Syncing accounts..."
                        )}
                    </span>

                    {hasActiveFilters && (
                        <div className="admin-users-active-chips">
                            {search && (
                                <span className="admin-users-filter-chip">
                                    {isVi ? "Từ khóa" : "Query"}: &ldquo;
                                    {search}&rdquo;
                                </span>
                            )}
                            {role && (
                                <span className="admin-users-filter-chip">
                                    {isVi ? "Vai trò" : "Role"}:{" "}
                                    {roleDisplay[role]}
                                </span>
                            )}
                            {status && (
                                <span className="admin-users-filter-chip">
                                    {isVi ? "Trạng thái" : "Status"}:{" "}
                                    {statusDisplay[status].label}
                                </span>
                            )}
                        </div>
                    )}
                </div>
            </section>

            {/* Feedback Banner */}
            {message && (
                <div
                    className={`admin-users-alert ${message.kind}`}
                    role="status"
                >
                    <AdminIcon
                        name={message.kind === "ok" ? "checkCircle" : "alert"}
                        size={17}
                    />
                    <span>{message.text}</span>
                    <button
                        type="button"
                        className="admin-users-alert-close"
                        onClick={() => setMessage(null)}
                        aria-label={isVi ? "Đóng thông báo" : "Dismiss"}
                    >
                        ✕
                    </button>
                </div>
            )}

            {/* D. Users Data Table */}
            <section className="admin-card admin-table-card admin-users-table-card">
                {!session ? (
                    <div className="admin-assign-empty-box">
                        <AdminIcon name="shield" size={26} />
                        <strong>{t("notSignedIn")}</strong>
                    </div>
                ) : query.isPending ? (
                    <div className="admin-users-skeleton-wrap" aria-busy="true">
                        {[1, 2, 3, 4, 5].map((idx) => (
                            <div
                                key={idx}
                                className="admin-users-skeleton-row"
                            />
                        ))}
                        <p className="admin-empty">{t("loading")}</p>
                    </div>
                ) : query.isError || !data ? (
                    <div className="admin-assign-empty-box" role="alert">
                        <AdminIcon name="alert" size={26} />
                        <strong>
                            {isVi
                                ? "Không thể tải danh sách tài khoản"
                                : "Failed to load accounts"}
                        </strong>
                        <p>{describeAccountError(query.error, language)}</p>
                        <button
                            type="button"
                            className="admin-btn admin-btn-ghost admin-btn-sm"
                            onClick={() => query.refetch()}
                        >
                            <AdminIcon name="refresh" size={14} />
                            <span>{t("refresh")}</span>
                        </button>
                    </div>
                ) : (
                    <>
                        <div className="admin-users-table-wrap">
                            <table className="admin-table admin-users-table">
                                <thead>
                                    <tr>
                                        <th>{t("adminUsersColAccount")}</th>
                                        <th>{t("adminUsersColRole")}</th>
                                        <th>{t("adminUsersColStatus")}</th>
                                        <th>{t("adminUsersColCreated")}</th>
                                        <th
                                            style={{ textAlign: "right" }}
                                            aria-label={t(
                                                "adminUsersColActions",
                                            )}
                                        >
                                            {t("adminUsersColActions")}
                                        </th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {data.items.map((account) => {
                                        const self =
                                            account.id === session.account.id;
                                        const admin =
                                            account.role === "SystemAdmin";
                                        const isRowBusy = busyId === account.id;
                                        const stMeta = statusDisplay[
                                            account.status
                                        ] ?? {
                                            label: account.status,
                                            tone: "neutral" as const,
                                            groupLabel: "",
                                        };

                                        return (
                                            <tr
                                                key={account.id}
                                                className={
                                                    isRowBusy
                                                        ? "is-row-busy"
                                                        : undefined
                                                }
                                            >
                                                {/* 1. Account Info */}
                                                <td
                                                    data-label={t(
                                                        "adminUsersColAccount",
                                                    )}
                                                >
                                                    <div className="admin-users-account-cell">
                                                        <span
                                                            className={`admin-users-avatar role-${account.role.toLowerCase()}`}
                                                        >
                                                            {getInitials(
                                                                account.fullName,
                                                                account.email,
                                                            )}
                                                        </span>
                                                        <div className="admin-users-account-meta">
                                                            <div className="admin-users-name-row">
                                                                <strong>
                                                                    {account.fullName || (
                                                                        <span className="admin-cell-unupdated">
                                                                            {isVi
                                                                                ? "Chưa cập nhật họ tên"
                                                                                : "Name not updated"}
                                                                        </span>
                                                                    )}
                                                                </strong>
                                                                {self && (
                                                                    <span className="admin-users-self-tag">
                                                                        {isVi
                                                                            ? "Bạn"
                                                                            : "You"}
                                                                    </span>
                                                                )}
                                                            </div>
                                                            <span className="admin-users-email">
                                                                {account.email}
                                                            </span>
                                                            <small className="admin-users-id">
                                                                ID:{" "}
                                                                {account.id.slice(
                                                                    0,
                                                                    8,
                                                                )}
                                                            </small>
                                                        </div>
                                                    </div>
                                                </td>

                                                {/* 2. Role */}
                                                <td
                                                    data-label={t(
                                                        "adminUsersColRole",
                                                    )}
                                                >
                                                    <div className="admin-users-role-cell">
                                                        <span
                                                            className={`admin-users-role-pill role-${account.role.toLowerCase()}`}
                                                        >
                                                            <i className="admin-users-role-dot" />
                                                            <span>
                                                                {
                                                                    roleDisplay[
                                                                        account
                                                                            .role
                                                                    ]
                                                                }
                                                            </span>
                                                        </span>
                                                        {!self ? (
                                                            <select
                                                                className="admin-select admin-select-sm admin-users-role-select"
                                                                value={
                                                                    account.role
                                                                }
                                                                disabled={
                                                                    isRowBusy
                                                                }
                                                                onChange={(
                                                                    e,
                                                                ) => {
                                                                    const nextRole =
                                                                        e.target
                                                                            .value as AccountRole;
                                                                    if (
                                                                        nextRole !==
                                                                        account.role
                                                                    ) {
                                                                        setPendingAction(
                                                                            {
                                                                                type: "role",
                                                                                account,
                                                                                newRole:
                                                                                    nextRole,
                                                                            },
                                                                        );
                                                                    }
                                                                }}
                                                                aria-label={`Change role for ${account.email}`}
                                                            >
                                                                {roles.map(
                                                                    (r) => (
                                                                        <option
                                                                            key={
                                                                                r
                                                                            }
                                                                            value={
                                                                                r
                                                                            }
                                                                        >
                                                                            {
                                                                                roleDisplay[
                                                                                    r
                                                                                ]
                                                                            }
                                                                        </option>
                                                                    ),
                                                                )}
                                                            </select>
                                                        ) : (
                                                            <span className="admin-users-role-select is-self-locked">
                                                                {isVi
                                                                    ? "Mặc định (Bạn)"
                                                                    : "Locked (You)"}
                                                            </span>
                                                        )}
                                                    </div>
                                                </td>

                                                {/* 3. Status (Strictly separating Email Verification / Account Status from Lockout) */}
                                                <td
                                                    data-label={t(
                                                        "adminUsersColStatus",
                                                    )}
                                                >
                                                    <div className="admin-users-status-stack">
                                                        <span
                                                            className={`admin-users-status-badge tone-${stMeta.tone}`}
                                                        >
                                                            <i />
                                                            {stMeta.label}
                                                        </span>

                                                        {account.isLocked && (
                                                            <small className="admin-users-lockout-badge">
                                                                {isVi
                                                                    ? "Khóa đăng nhập đến "
                                                                    : "Locked until "}
                                                                {formatDate(
                                                                    account.lockoutEndUtc,
                                                                )}
                                                            </small>
                                                        )}
                                                    </div>
                                                </td>

                                                {/* 4. Created At */}
                                                <td
                                                    data-label={t(
                                                        "adminUsersColCreated",
                                                    )}
                                                >
                                                    <span className="admin-users-date">
                                                        {formatDate(
                                                            account.createdAtUtc,
                                                        )}
                                                    </span>
                                                </td>

                                                {/* 5. Actions */}
                                                <td
                                                    data-label={t(
                                                        "adminUsersColActions",
                                                    )}
                                                    className="admin-table-action"
                                                >
                                                    <div className="admin-row-actions">
                                                        {account.isLocked && (
                                                            <button
                                                                type="button"
                                                                className="admin-btn admin-btn-ghost admin-btn-xs"
                                                                disabled={
                                                                    isRowBusy
                                                                }
                                                                onClick={() =>
                                                                    setPendingAction(
                                                                        {
                                                                            type: "unlock",
                                                                            account,
                                                                        },
                                                                    )
                                                                }
                                                            >
                                                                {t(
                                                                    "adminUsersUnlock",
                                                                )}
                                                            </button>
                                                        )}
                                                        {!self &&
                                                            !admin &&
                                                            account.status ===
                                                                "EmailVerified" && (
                                                                <button
                                                                    type="button"
                                                                    className="admin-btn admin-btn-danger-ghost admin-btn-xs"
                                                                    disabled={
                                                                        isRowBusy
                                                                    }
                                                                    onClick={() =>
                                                                        setPendingAction(
                                                                            {
                                                                                type: "suspend",
                                                                                account,
                                                                            },
                                                                        )
                                                                    }
                                                                >
                                                                    {t(
                                                                        "adminUsersSuspend",
                                                                    )}
                                                                </button>
                                                            )}
                                                        {!self &&
                                                            account.status ===
                                                                "Suspended" && (
                                                                <button
                                                                    type="button"
                                                                    className="admin-btn admin-btn-lime admin-btn-xs"
                                                                    disabled={
                                                                        isRowBusy
                                                                    }
                                                                    onClick={() =>
                                                                        setPendingAction(
                                                                            {
                                                                                type: "activate",
                                                                                account,
                                                                            },
                                                                        )
                                                                    }
                                                                >
                                                                    {t(
                                                                        "adminUsersActivate",
                                                                    )}
                                                                </button>
                                                            )}
                                                        {self && (
                                                            <span className="admin-users-protected-note">
                                                                {isVi
                                                                    ? "Phiên hiện tại"
                                                                    : "Current user"}
                                                            </span>
                                                        )}
                                                        {!self &&
                                                            admin &&
                                                            !account.isLocked &&
                                                            account.status !==
                                                                "Suspended" && (
                                                                <span className="admin-users-protected-note">
                                                                    {isVi
                                                                        ? "Được bảo vệ"
                                                                        : "Protected"}
                                                                </span>
                                                            )}
                                                    </div>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>

                        {/* Empty / Search Empty State */}
                        {data.items.length === 0 && (
                            <div className="admin-assign-empty-box">
                                <AdminIcon name="users" size={26} />
                                <strong>{t("adminUsersEmpty")}</strong>
                                <p>
                                    {hasActiveFilters
                                        ? isVi
                                            ? "Không có tài khoản nào khớp với từ khóa hoặc điều kiện lọc hiện tại."
                                            : "No accounts match your current search or filter criteria."
                                        : isVi
                                          ? "Hệ thống hiện chưa có tài khoản nào được ghi nhận."
                                          : "No accounts are currently registered in the system."}
                                </p>
                                {hasActiveFilters && (
                                    <button
                                        type="button"
                                        className="admin-btn admin-btn-xs admin-btn-ghost"
                                        onClick={handleClearFilters}
                                    >
                                        {isVi ? "Xóa bộ lọc" : "Clear filters"}
                                    </button>
                                )}
                            </div>
                        )}

                        {/* F. Pagination */}
                        <div className="admin-pager admin-users-pager">
                            <span className="admin-users-pager-summary">
                                {isVi
                                    ? `Tổng số: ${data.totalCount} tài khoản`
                                    : `${data.totalCount} accounts`}
                            </span>
                            <div className="admin-users-pager-controls">
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-ghost admin-btn-sm"
                                    disabled={page <= 1 || query.isFetching}
                                    onClick={() => setPage(page - 1)}
                                >
                                    {t("adminUsersPrevious")}
                                </button>
                                <span className="admin-users-page-indicator">
                                    {t("adminUsersPageOf")} {page} / {pages}
                                </span>
                                <button
                                    type="button"
                                    className="admin-btn admin-btn-ghost admin-btn-sm"
                                    disabled={page >= pages || query.isFetching}
                                    onClick={() => setPage(page + 1)}
                                >
                                    {t("adminUsersNext")}
                                </button>
                            </div>
                        </div>
                    </>
                )}
            </section>

            {/* E. Confirmation Dialog for Account Actions */}
            {pendingAction && (
                <div
                    className="admin-modal-backdrop"
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="users-confirm-modal-title"
                    onClick={() => !busyId && setPendingAction(null)}
                >
                    <div
                        className="admin-modal admin-owner-reject-modal"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="admin-owner-reject-top">
                            <div
                                className={`admin-modal-icon ${
                                    pendingAction.type === "suspend"
                                        ? "danger"
                                        : ""
                                }`}
                            >
                                <AdminIcon
                                    name={
                                        pendingAction.type === "suspend"
                                            ? "alert"
                                            : "shield"
                                    }
                                    size={20}
                                />
                            </div>
                            <button
                                type="button"
                                className="admin-btn admin-btn-xs admin-btn-ghost"
                                onClick={() => setPendingAction(null)}
                                disabled={Boolean(busyId)}
                            >
                                ✕
                            </button>
                        </div>

                        <h2 id="users-confirm-modal-title">
                            {pendingAction.type === "suspend"
                                ? isVi
                                    ? "Xác nhận đình chỉ tài khoản"
                                    : "Confirm Account Suspension"
                                : pendingAction.type === "activate"
                                  ? isVi
                                      ? "Xác nhận kích hoạt tài khoản"
                                      : "Confirm Account Activation"
                                  : pendingAction.type === "unlock"
                                    ? isVi
                                        ? "Xác nhận mở khóa đăng nhập"
                                        : "Confirm Account Unlock"
                                    : isVi
                                      ? "Xác nhận thay đổi vai trò"
                                      : "Confirm Role Change"}
                        </h2>

                        <p className="admin-owner-reject-desc">
                            {pendingAction.type === "suspend"
                                ? isVi
                                    ? "Tài khoản sau khi bị đình chỉ sẽ không thể đăng nhập và thực hiện giao dịch trên hệ thống cho đến khi được kích hoạt lại."
                                    : "This account will not be able to sign in or perform actions until reactivated."
                                : pendingAction.type === "activate"
                                  ? isVi
                                      ? "Khôi phục quyền truy cập bình thường cho tài khoản này trên hệ thống ParkWise."
                                      : "Restore normal system access for this account."
                                  : pendingAction.type === "unlock"
                                    ? isVi
                                        ? "Gỡ bỏ trạng thái khóa tạm thời do đăng nhập sai nhiều lần cho tài khoản này."
                                        : "Clear the temporary login lockout for this account."
                                    : isVi
                                      ? `Bạn đang thay đổi vai trò của tài khoản từ "${roleDisplay[pendingAction.account.role]}" sang "${roleDisplay[pendingAction.newRole ?? pendingAction.account.role]}".`
                                      : `You are changing this account's role from "${roleDisplay[pendingAction.account.role]}" to "${roleDisplay[pendingAction.newRole ?? pendingAction.account.role]}".`}
                        </p>

                        <div className="admin-owner-reject-summary">
                            <div>
                                <span>{isVi ? "Họ và tên" : "Full Name"}</span>
                                <strong>
                                    {pendingAction.account.fullName ||
                                        (isVi
                                            ? "Chưa cập nhật"
                                            : "Not updated")}
                                </strong>
                            </div>
                            <div>
                                <span>Email</span>
                                <strong>{pendingAction.account.email}</strong>
                            </div>
                            <div>
                                <span>
                                    {isVi ? "Vai trò hiện tại" : "Current Role"}
                                </span>
                                <strong>
                                    {roleDisplay[pendingAction.account.role]}
                                </strong>
                            </div>
                            <div>
                                <span>
                                    {isVi
                                        ? "Hành động sắp thực hiện"
                                        : "Action"}
                                </span>
                                <strong>
                                    {pendingAction.type === "suspend"
                                        ? t("adminUsersSuspend")
                                        : pendingAction.type === "activate"
                                          ? t("adminUsersActivate")
                                          : pendingAction.type === "unlock"
                                            ? t("adminUsersUnlock")
                                            : `${roleDisplay[pendingAction.account.role]} → ${roleDisplay[pendingAction.newRole ?? pendingAction.account.role]}`}
                                </strong>
                            </div>
                        </div>

                        <div
                            className="admin-modal-actions"
                            style={{ marginTop: "18px" }}
                        >
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost"
                                onClick={() => setPendingAction(null)}
                                disabled={Boolean(busyId)}
                            >
                                {isVi ? "Hủy" : "Cancel"}
                            </button>
                            <button
                                type="button"
                                className={`admin-btn ${
                                    pendingAction.type === "suspend"
                                        ? "admin-btn-danger"
                                        : "admin-btn-lime"
                                }`}
                                onClick={handleConfirmPendingAction}
                                disabled={Boolean(busyId)}
                            >
                                {busyId
                                    ? isVi
                                        ? "Đang xử lý..."
                                        : "Processing..."
                                    : isVi
                                      ? "Xác nhận thực hiện"
                                      : "Confirm"}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
