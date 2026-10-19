"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { AdminIcon, formatDate } from "./AdminUi";
import {
    activateAccount,
    describeAccountError,
    searchAccounts,
    suspendAccount,
    unlockAccount,
    type AccountRole,
    type AccountStatus,
    type AccountSummary,
} from "./adminAccounts";

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
const roleLabel: Record<AccountRole, string> = {
    Customer: "Customer",
    ParkingLotOwner: "Owner",
    Staff: "Staff",
    SystemAdmin: "Admin",
};
const PAGE_SIZE = 20;

/** Only actions Identity actually exposes: list, suspend, activate, unlock. */
export function AdminUsersPage() {
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
            setMessage({
                kind: "ok",
                text: `${account.email}: ${action === "unlock" ? "unlocked" : action === "suspend" ? "suspended" : "activated"}.`,
            });
        } catch (error) {
            setMessage({ kind: "error", text: describeAccountError(error) });
        } finally {
            setBusyId("");
            await client.invalidateQueries({ queryKey: ["admin-accounts"] });
        }
    }

    const data = query.data;
    const pages = data
        ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
        : 1;

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">Identity</p>
                    <h1>Users & owners</h1>
                </div>
            </header>

            <div className="admin-toolbar">
                <label className="admin-search">
                    <AdminIcon name="search" size={16} />
                    <input
                        id="users-search"
                        value={searchInput}
                        onChange={(e) => setSearchInput(e.target.value)}
                        placeholder="Search email or name"
                    />
                </label>
                <select
                    id="users-role"
                    className="admin-select"
                    value={role}
                    onChange={(e) => {
                        setRole(e.target.value as AccountRole | "");
                        setPage(1);
                    }}
                >
                    <option value="">All roles</option>
                    {roles.map((r) => (
                        <option key={r} value={r}>
                            {roleLabel[r]}
                        </option>
                    ))}
                </select>
                <select
                    id="users-status"
                    className="admin-select"
                    value={status}
                    onChange={(e) => {
                        setStatus(e.target.value as AccountStatus | "");
                        setPage(1);
                    }}
                >
                    <option value="">All statuses</option>
                    {statuses.map((s) => (
                        <option key={s} value={s}>
                            {s}
                        </option>
                    ))}
                </select>
            </div>

            {message && (
                <p className={`admin-inline-msg ${message.kind}`} role="status">
                    {message.text}
                </p>
            )}

            <section className="admin-card admin-table-card">
                {!session ? (
                    <p className="admin-empty">
                        Sign in as a SystemAdmin to load accounts.
                    </p>
                ) : query.isPending ? (
                    <p className="admin-empty">Loading accounts…</p>
                ) : query.isError || !data ? (
                    <p className="admin-empty">
                        Couldn&apos;t load accounts.{" "}
                        {describeAccountError(query.error)}{" "}
                        <button
                            type="button"
                            className="admin-link"
                            onClick={() => query.refetch()}
                        >
                            Retry
                        </button>
                    </p>
                ) : (
                    <>
                        <table className="admin-table">
                            <thead>
                                <tr>
                                    <th>Account</th>
                                    <th>Role</th>
                                    <th>Status</th>
                                    <th>Created</th>
                                    <th aria-label="Actions" />
                                </tr>
                            </thead>
                            <tbody>
                                {data.items.map((account) => {
                                    const self =
                                        account.id === session.account.id;
                                    const admin =
                                        account.role === "SystemAdmin";
                                    return (
                                        <tr key={account.id}>
                                            <td>
                                                <strong>
                                                    {account.fullName || "—"}
                                                </strong>
                                                <small>{account.email}</small>
                                            </td>
                                            <td>
                                                <span
                                                    className={`admin-role admin-role-${account.role.toLowerCase()}`}
                                                >
                                                    {roleLabel[account.role]}
                                                </span>
                                            </td>
                                            <td>
                                                {account.status}
                                                {account.isLocked && (
                                                    <small className="admin-locked">
                                                        Locked until{" "}
                                                        {formatDate(
                                                            account.lockoutEndUtc,
                                                        )}
                                                    </small>
                                                )}
                                            </td>
                                            <td>
                                                {formatDate(
                                                    account.createdAtUtc,
                                                )}
                                            </td>
                                            <td className="admin-table-action">
                                                {account.isLocked && (
                                                    <button
                                                        type="button"
                                                        className="admin-btn admin-btn-ghost admin-btn-sm"
                                                        disabled={
                                                            busyId ===
                                                            account.id
                                                        }
                                                        onClick={() =>
                                                            run(
                                                                account,
                                                                "unlock",
                                                            )
                                                        }
                                                    >
                                                        Unlock
                                                    </button>
                                                )}
                                                {!self &&
                                                    !admin &&
                                                    account.status ===
                                                        "EmailVerified" && (
                                                        <button
                                                            type="button"
                                                            className="admin-btn admin-btn-danger-ghost admin-btn-sm"
                                                            disabled={
                                                                busyId ===
                                                                account.id
                                                            }
                                                            onClick={() =>
                                                                run(
                                                                    account,
                                                                    "suspend",
                                                                )
                                                            }
                                                        >
                                                            Suspend
                                                        </button>
                                                    )}
                                                {!self &&
                                                    account.status ===
                                                        "Suspended" && (
                                                        <button
                                                            type="button"
                                                            className="admin-btn admin-btn-ghost admin-btn-sm"
                                                            disabled={
                                                                busyId ===
                                                                account.id
                                                            }
                                                            onClick={() =>
                                                                run(
                                                                    account,
                                                                    "activate",
                                                                )
                                                            }
                                                        >
                                                            Activate
                                                        </button>
                                                    )}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                        {data.items.length === 0 && (
                            <p className="admin-empty">
                                No accounts match your filters.
                            </p>
                        )}
                        <div className="admin-pager">
                            <span>{data.totalCount} accounts</span>
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost admin-btn-sm"
                                disabled={page <= 1}
                                onClick={() => setPage(page - 1)}
                            >
                                Previous
                            </button>
                            <span>
                                {page} / {pages}
                            </span>
                            <button
                                type="button"
                                className="admin-btn admin-btn-ghost admin-btn-sm"
                                disabled={page >= pages}
                                onClick={() => setPage(page + 1)}
                            >
                                Next
                            </button>
                        </div>
                    </>
                )}
            </section>
        </>
    );
}
