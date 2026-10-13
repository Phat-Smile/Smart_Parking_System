"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import {
    PARKING_LOTS_DATA,
    type ParkingLotDetail,
} from "./[id]/mock-data";

type FilterStatus = "All" | "Pending" | "Approved" | "Rejected";

function getStatus(status: ParkingLotDetail["approvalStatus"]): FilterStatus {
    if (status === "Approved") return "Approved";
    if (status === "Rejected") return "Rejected";
    return "Pending";
}

function StatusBadge({ status }: { status: FilterStatus }) {
    const styles: Record<FilterStatus, string> = {
        All: "bg-gray-100 text-gray-700 border-gray-200",
        Pending: "bg-amber-50 text-amber-700 border-amber-200",
        Approved: "bg-emerald-50 text-emerald-700 border-emerald-200",
        Rejected: "bg-red-50 text-red-700 border-red-200",
    };

    const labels: Record<FilterStatus, string> = {
        All: "All statuses",
        Pending: "Pending",
        Approved: "Approved / Active",
        Rejected: "Rejected",
    };

    return (
        <span
            className={`inline-flex items-center rounded-full border px-3 py-1 text-xs font-bold ${styles[status]}`}
        >
            <span className="mr-2 h-1.5 w-1.5 rounded-full bg-current" />
            {labels[status]}
        </span>
    );
}

export default function ParkingLotManagementPage() {
    const [search, setSearch] = useState("");
    const [statusFilter, setStatusFilter] = useState<FilterStatus>("All");

    const parkingLots = useMemo(
        () => Object.values(PARKING_LOTS_DATA),
        []
    );

    const counts = useMemo(() => {
        return {
            all: parkingLots.length,
            pending: parkingLots.filter(
                (lot) => getStatus(lot.approvalStatus) === "Pending"
            ).length,
            approved: parkingLots.filter(
                (lot) => getStatus(lot.approvalStatus) === "Approved"
            ).length,
            rejected: parkingLots.filter(
                (lot) => getStatus(lot.approvalStatus) === "Rejected"
            ).length,
        };
    }, [parkingLots]);

    const filteredLots = useMemo(() => {
        const keyword = search.trim().toLowerCase();

        return parkingLots.filter((lot) => {
            const matchesSearch =
                !keyword ||
                lot.name.toLowerCase().includes(keyword) ||
                lot.id.toLowerCase().includes(keyword) ||
                lot.owner.name.toLowerCase().includes(keyword) ||
                lot.owner.email.toLowerCase().includes(keyword) ||
                lot.location.address.toLowerCase().includes(keyword) ||
                lot.businessName.toLowerCase().includes(keyword);

            const matchesStatus =
                statusFilter === "All" ||
                getStatus(lot.approvalStatus) === statusFilter;

            return matchesSearch && matchesStatus;
        });
    }, [parkingLots, search, statusFilter]);

    const summaryCards = [
        {
            title: "Total Parking Lots",
            value: counts.all,
            description: "All submitted parking lots",
            color: "text-gray-900",
            icon: "P",
        },
        {
            title: "Pending",
            value: counts.pending,
            description: "Awaiting approval",
            color: "text-amber-700",
            icon: "!",
        },
        {
            title: "Approved / Active",
            value: counts.approved,
            description: "Approved parking lots",
            color: "text-emerald-700",
            icon: "✓",
        },
        {
            title: "Rejected",
            value: counts.rejected,
            description: "Rejected applications",
            color: "text-red-700",
            icon: "×",
        },
    ];

    return (
        <div className="min-h-full space-y-7 pb-8 text-[#171717]">
            {/* Page heading */}
            <div className="flex flex-col justify-between gap-4 md:flex-row md:items-center">
                <div>
                    <div className="mb-2 flex items-center gap-2 text-sm font-medium text-[#737373]">
                        <Link href="/admin" className="hover:text-black">
                            Admin
                        </Link>
                        <span>/</span>
                        <span className="text-[#171717]">Parking Lots</span>
                    </div>

                    <h1 className="text-3xl font-extrabold tracking-tight">
                        Parking Lot Management
                    </h1>

                    <p className="mt-2 text-sm text-[#737373]">
                        Manage parking lot submissions and monitor their approval status.
                    </p>
                </div>

                <Link
                    href="/admin/parking-lots/pending"
                    className="inline-flex items-center justify-center gap-2 rounded-full bg-[#eafe89] px-5 py-3 text-sm font-bold text-[#171717] transition hover:bg-[#dff56d]"
                >
                    <span>Review pending</span>
                    <span aria-hidden="true">→</span>
                </Link>
            </div>

            {/* Summary cards */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {summaryCards.map((card) => (
                    <div
                        key={card.title}
                        className="rounded-2xl border border-[#e5e5e5] bg-white p-5 shadow-sm"
                    >
                        <div className="flex items-start justify-between gap-3">
                            <p className="text-sm font-semibold text-[#737373]">
                                {card.title}
                            </p>

                            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[#f5f5f5] text-lg font-bold">
                                {card.icon}
                            </span>
                        </div>

                        <p className={`mt-4 text-3xl font-extrabold ${card.color}`}>
                            {card.value}
                        </p>

                        <p className="mt-1 text-xs text-[#a3a3a3]">
                            {card.description}
                        </p>
                    </div>
                ))}
            </div>

            {/* List */}
            <section className="overflow-hidden rounded-3xl border border-[#e5e5e5] bg-white shadow-sm">
                <div className="flex flex-col gap-4 border-b border-[#e5e5e5] p-5 md:p-6">
                    <div>
                        <h2 className="text-lg font-extrabold">
                            All Parking Lots
                        </h2>
                        <p className="mt-1 text-sm text-[#737373]">
                            Search, filter and review parking lot information.
                        </p>
                    </div>

                    <div className="flex flex-col gap-3 lg:flex-row">
                        <div className="relative flex-1">
                            <svg
                                className="absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-[#a3a3a3]"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                aria-hidden="true"
                            >
                                <circle cx="11" cy="11" r="7" strokeWidth="2" />
                                <path
                                    d="m16 16 4 4"
                                    strokeWidth="2"
                                    strokeLinecap="round"
                                />
                            </svg>

                            <input
                                type="search"
                                value={search}
                                onChange={(event) => setSearch(event.target.value)}
                                placeholder="Search by name, ID, owner or address..."
                                aria-label="Search parking lots"
                                className="w-full rounded-full border border-[#e5e5e5] bg-[#fafafa] py-3 pl-12 pr-4 text-sm outline-none transition placeholder:text-[#a3a3a3] focus:border-[#a3a3a3] focus:bg-white"
                            />
                        </div>

                        <select
                            value={statusFilter}
                            onChange={(event) =>
                                setStatusFilter(event.target.value as FilterStatus)
                            }
                            aria-label="Filter by approval status"
                            className="rounded-full border border-[#e5e5e5] bg-white px-4 py-3 text-sm font-semibold outline-none focus:border-[#a3a3a3]"
                        >
                            <option value="All">All statuses</option>
                            <option value="Pending">Pending</option>
                            <option value="Approved">Approved / Active</option>
                            <option value="Rejected">Rejected</option>
                        </select>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                        <button
                            type="button"
                            onClick={() => setStatusFilter("All")}
                            className={`rounded-full border px-4 py-2 text-xs font-bold transition ${statusFilter === "All"
                                ? "border-[#171717] bg-[#171717] text-white"
                                : "border-[#e5e5e5] bg-white text-[#525252] hover:bg-[#f5f5f5]"
                                }`}
                        >
                            All ({counts.all})
                        </button>

                        <button
                            type="button"
                            onClick={() => setStatusFilter("Pending")}
                            className={`rounded-full border px-4 py-2 text-xs font-bold transition ${statusFilter === "Pending"
                                ? "border-amber-600 bg-amber-600 text-white"
                                : "border-[#e5e5e5] bg-white text-[#525252] hover:bg-[#f5f5f5]"
                                }`}
                        >
                            Pending ({counts.pending})
                        </button>

                        <button
                            type="button"
                            onClick={() => setStatusFilter("Approved")}
                            className={`rounded-full border px-4 py-2 text-xs font-bold transition ${statusFilter === "Approved"
                                ? "border-emerald-700 bg-emerald-700 text-white"
                                : "border-[#e5e5e5] bg-white text-[#525252] hover:bg-[#f5f5f5]"
                                }`}
                        >
                            Approved / Active ({counts.approved})
                        </button>

                        <button
                            type="button"
                            onClick={() => setStatusFilter("Rejected")}
                            className={`rounded-full border px-4 py-2 text-xs font-bold transition ${statusFilter === "Rejected"
                                ? "border-red-700 bg-red-700 text-white"
                                : "border-[#e5e5e5] bg-white text-[#525252] hover:bg-[#f5f5f5]"
                                }`}
                        >
                            Rejected ({counts.rejected})
                        </button>
                    </div>
                </div>

                {/* Table */}
                <div className="overflow-x-auto">
                    <table className="w-full min-w-[900px] border-collapse text-left">
                        <thead>
                            <tr className="border-b border-[#e5e5e5] bg-[#fafafa]">
                                <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-[#737373]">
                                    Parking Lot
                                </th>
                                <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-[#737373]">
                                    Owner
                                </th>
                                <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-[#737373]">
                                    Location
                                </th>
                                <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-[#737373]">
                                    Category
                                </th>
                                <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-[#737373]">
                                    Status
                                </th>
                                <th className="px-6 py-4 text-right text-xs font-bold uppercase tracking-wider text-[#737373]">
                                    Action
                                </th>
                            </tr>
                        </thead>

                        <tbody className="divide-y divide-[#eeeeee]">
                            {filteredLots.map((lot) => {
                                const status = getStatus(lot.approvalStatus);

                                return (
                                    <tr
                                        key={lot.id}
                                        className="transition hover:bg-[#fcfcfc]"
                                    >
                                        <td className="px-6 py-5">
                                            <div className="flex items-center gap-3">
                                                <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-[#e5e5e5] bg-[#f7f7f7]">
                                                    <svg
                                                        className="h-5 w-5 text-[#525252]"
                                                        viewBox="0 0 24 24"
                                                        fill="none"
                                                        stroke="currentColor"
                                                        aria-hidden="true"
                                                    >
                                                        <path
                                                            d="M3 21h18M5 21V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16M9 7h1m4 0h1M9 11h1m4 0h1M9 15h1m4 0h1"
                                                            strokeWidth="1.7"
                                                            strokeLinecap="round"
                                                            strokeLinejoin="round"
                                                        />
                                                    </svg>
                                                </div>

                                                <div>
                                                    <p className="font-bold text-[#171717]">
                                                        {lot.name}
                                                    </p>
                                                    <p className="mt-1 text-xs font-medium text-[#a3a3a3]">
                                                        {lot.id}
                                                    </p>
                                                </div>
                                            </div>
                                        </td>

                                        <td className="px-6 py-5">
                                            <p className="text-sm font-semibold">
                                                {lot.owner.name}
                                            </p>
                                            <p className="mt-1 text-xs text-[#737373]">
                                                {lot.owner.email}
                                            </p>
                                        </td>

                                        <td className="max-w-[260px] px-6 py-5">
                                            <p className="text-sm text-[#525252]">
                                                {lot.location.address}
                                            </p>
                                            <p className="mt-1 text-xs text-[#a3a3a3]">
                                                {lot.location.district},{" "}
                                                {lot.location.city}
                                            </p>
                                        </td>

                                        <td className="px-6 py-5">
                                            <span className="text-sm text-[#525252]">
                                                {lot.category}
                                            </span>
                                        </td>

                                        <td className="px-6 py-5">
                                            <div className="flex flex-col items-start gap-1.5">
                                                <StatusBadge status={status} />
                                                {status === "Pending" && (
                                                    <span className="text-xs text-[#a3a3a3]">
                                                        {lot.approvalStatus}
                                                    </span>
                                                )}
                                            </div>
                                        </td>

                                        <td className="px-6 py-5 text-right">
                                            <Link
                                                href={`/admin/parking-lots/${encodeURIComponent(lot.id)}`}
                                                className="inline-flex items-center gap-2 rounded-full border border-[#e5e5e5] px-4 py-2 text-sm font-bold transition hover:border-[#171717] hover:bg-[#171717] hover:text-white"
                                            >
                                                View details
                                                <span aria-hidden="true">→</span>
                                            </Link>
                                        </td>
                                    </tr>
                                );
                            })}

                            {filteredLots.length === 0 && (
                                <tr>
                                    <td colSpan={6} className="px-6 py-16 text-center">
                                        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-[#f5f5f5] text-xl">
                                            ⌕
                                        </div>
                                        <p className="mt-4 font-bold text-[#171717]">
                                            No parking lots found
                                        </p>
                                        <p className="mt-1 text-sm text-[#737373]">
                                            Try another keyword or change the status filter.
                                        </p>
                                        <button
                                            type="button"
                                            onClick={() => {
                                                setSearch("");
                                                setStatusFilter("All");
                                            }}
                                            className="mt-4 text-sm font-bold underline underline-offset-4"
                                        >
                                            Clear filters
                                        </button>
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>

                <div className="flex flex-col justify-between gap-2 border-t border-[#e5e5e5] bg-[#fafafa] px-6 py-4 text-xs text-[#737373] sm:flex-row sm:items-center">
                    <p>
                        Showing{" "}
                        <span className="font-bold text-[#171717]">
                            {filteredLots.length}
                        </span>{" "}
                        of{" "}
                        <span className="font-bold text-[#171717]">
                            {parkingLots.length}
                        </span>{" "}
                        parking lots
                    </p>
                    <p>Parking Lot Management · Admin</p>
                </div>
            </section>
        </div>
    );
}