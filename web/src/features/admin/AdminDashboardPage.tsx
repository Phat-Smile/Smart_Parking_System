"use client";

import Link from "next/link";
import { AdminIcon, SampleDataNote, StatusPill, formatDate } from "./AdminUi";
import { countByStatus, useAdminLots } from "./adminLotsData";

export function AdminDashboardPage() {
    const lots = useAdminLots();
    const counts = countByStatus(lots);
    const total = lots.length || 1;
    const capacity = lots
        .filter((lot) => lot.status === "Approved")
        .reduce((sum, lot) => sum + lot.totalCapacity, 0);
    const queue = lots
        .filter((lot) => lot.status === "Pending")
        .sort((a, b) => a.submittedAtUtc.localeCompare(b.submittedAtUtc));
    const recent = lots
        .filter((lot) => lot.reviewedAtUtc)
        .sort((a, b) =>
            (b.reviewedAtUtc ?? "").localeCompare(a.reviewedAtUtc ?? ""),
        )
        .slice(0, 4);

    const cards = [
        {
            key: "Pending",
            label: "Waiting for review",
            value: counts.Pending,
            href: "/admin/parking-lots/pending",
        },
        {
            key: "Approved",
            label: "Active parking lots",
            value: counts.Approved,
            href: "/admin/parking-lots?status=Approved",
        },
        {
            key: "Rejected",
            label: "Rejected applications",
            value: counts.Rejected,
            href: "/admin/parking-lots?status=Rejected",
        },
    ] as const;

    return (
        <>
            <header className="admin-heading">
                <div>
                    <p className="admin-eyebrow">System overview</p>
                    <h1>Dashboard</h1>
                </div>
                <Link
                    href="/admin/parking-lots/pending"
                    className="admin-btn admin-btn-dark"
                    id="dashboard-review-queue"
                >
                    Review queue <AdminIcon name="arrow" size={16} />
                </Link>
            </header>
            <SampleDataNote />

            <section className="admin-stats" aria-label="Parking lot summary">
                {cards.map((card) => (
                    <Link
                        key={card.key}
                        href={card.href}
                        className={`admin-stat admin-stat-${card.key.toLowerCase()}`}
                    >
                        <span>{card.label}</span>
                        <strong>{card.value}</strong>
                        <div className="admin-meter">
                            <i
                                style={{
                                    width: `${(card.value / total) * 100}%`,
                                }}
                            />
                        </div>
                    </Link>
                ))}
                <div className="admin-stat admin-stat-capacity">
                    <span>Approved capacity</span>
                    <strong>{capacity.toLocaleString("en-US")}</strong>
                    <small>slots open to drivers</small>
                </div>
            </section>

            <div className="admin-columns">
                <section className="admin-card">
                    <div className="admin-card-head">
                        <h2>Oldest pending first</h2>
                        <Link href="/admin/parking-lots/pending">See all</Link>
                    </div>
                    {queue.length === 0 ? (
                        <p className="admin-empty">
                            Nothing to review. Nice work.
                        </p>
                    ) : (
                        <ul className="admin-list">
                            {queue.slice(0, 5).map((lot) => (
                                <li key={lot.id}>
                                    <div>
                                        <strong>{lot.name}</strong>
                                        <small>
                                            {lot.ownerName} · submitted{" "}
                                            {formatDate(lot.submittedAtUtc)}
                                        </small>
                                    </div>
                                    <Link
                                        href={`/admin/parking-lots/${lot.id}`}
                                        className="admin-btn admin-btn-lime admin-btn-sm"
                                    >
                                        Review
                                    </Link>
                                </li>
                            ))}
                        </ul>
                    )}
                </section>
                <section className="admin-card">
                    <div className="admin-card-head">
                        <h2>Recent decisions</h2>
                    </div>
                    {recent.length === 0 ? (
                        <p className="admin-empty">No decisions yet.</p>
                    ) : (
                        <ul className="admin-list">
                            {recent.map((lot) => (
                                <li key={lot.id}>
                                    <div>
                                        <strong>{lot.name}</strong>
                                        <small>
                                            {formatDate(lot.reviewedAtUtc)}
                                        </small>
                                    </div>
                                    <StatusPill status={lot.status} />
                                </li>
                            ))}
                        </ul>
                    )}
                </section>
            </div>
        </>
    );
}
