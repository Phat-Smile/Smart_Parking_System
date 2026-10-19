"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AdminIcon, SampleDataNote, StatusPill, formatDate } from "./AdminUi";
import { countByStatus, useAdminLots } from "./adminLotsData";
import { isParkingLotId } from "./reviewParkingLot";
import type { ApprovalStatus } from "./types";

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
    const lots = useAdminLots();
    const counts = countByStatus(lots);
    const [status, setStatus] = useState<Filter>(
        mode === "pending" ? "Pending" : initialStatus,
    );
    const [search, setSearch] = useState("");
    const [lookupId, setLookupId] = useState("");
    const [lookupError, setLookupError] = useState("");

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

    const tabs: Filter[] = ["All", "Pending", "Approved", "Rejected"];

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">
                        {mode === "pending" ? "Review queue" : "Management"}
                    </p>
                    <h1>
                        {mode === "pending"
                            ? "Pending parking lots"
                            : "Parking lots"}
                    </h1>
                </div>
            </header>
            <SampleDataNote />

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
                                <span>
                                    {tab === "All" ? lots.length : counts[tab]}
                                </span>
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
                        placeholder="Search name, owner or address"
                    />
                </label>
            </div>

            <section className="admin-card admin-table-card">
                <table className="admin-table">
                    <thead>
                        <tr>
                            <th>Parking lot</th>
                            <th>Owner</th>
                            <th>Submitted</th>
                            <th>Capacity</th>
                            <th>Status</th>
                            <th aria-label="Actions" />
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
                                <td>{lot.totalCapacity} slots</td>
                                <td>
                                    <StatusPill status={lot.status} />
                                </td>
                                <td className="admin-table-action">
                                    <Link
                                        href={`/admin/parking-lots/${lot.id}`}
                                        className={`admin-btn admin-btn-sm ${lot.status === "Pending" ? "admin-btn-lime" : "admin-btn-ghost"}`}
                                    >
                                        {lot.status === "Pending"
                                            ? "Review"
                                            : "View"}
                                    </Link>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {rows.length === 0 && (
                    <p className="admin-empty">
                        {mode === "pending"
                            ? "No parking lots are waiting for review."
                            : "No parking lots match your filters."}
                    </p>
                )}
            </section>

            <form className="admin-lookup" onSubmit={openById}>
                <div>
                    <strong>Review a lot by ID</strong>
                    <small>
                        Paste the UUID from the owner&apos;s submission to
                        review it against the backend.
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
                    Open
                </button>
                {lookupError && (
                    <p className="admin-field-error" role="alert">
                        {lookupError}
                    </p>
                )}
            </form>
        </>
    );
}
