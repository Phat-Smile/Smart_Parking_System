
"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";

export default function ApproveParkingLotPage() {
    const params = useParams<{ id: string }>();
    const router = useRouter();
    const id = decodeURIComponent(params.id);

    const [showConfirm, setShowConfirm] = useState(false);
    const [approved, setApproved] = useState(false);
    const [loading, setLoading] = useState(false);

    function handleApprove() {
        setLoading(true);

        // Demo only: replace with the actual backend approval API.
        setApproved(true);
        setShowConfirm(false);
        setLoading(false);
    }

    return (
        <main className="min-h-screen bg-slate-50 p-6 text-slate-900">
            <div className="mx-auto max-w-3xl space-y-6">
                <Link
                    href={`/admin/parking-lots/${encodeURIComponent(id)}`}
                    className="text-sm font-medium text-blue-600 hover:underline"
                >
                    ← Back to Parking Lot Detail
                </Link>

                <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
                    <div className="flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100 text-3xl text-emerald-700">
                        ✓
                    </div>

                    <h1 className="mt-5 text-2xl font-bold">
                        Approve Parking Lot
                    </h1>

                    <p className="mt-2 text-sm leading-6 text-slate-500">
                        Review the application before approving it. Once approval
                        is confirmed, the parking lot should become Active.
                    </p>

                    <div className="mt-6 space-y-4 rounded-xl bg-slate-50 p-5">
                        <div className="flex justify-between gap-4">
                            <span className="text-sm text-slate-500">
                                Parking Lot ID
                            </span>
                            <span className="font-semibold">{id}</span>
                        </div>

                        <div className="flex justify-between gap-4">
                            <span className="text-sm text-slate-500">
                                Parking Lot
                            </span>
                            <span className="font-semibold">SmartPark Central</span>
                        </div>

                        <div className="flex justify-between gap-4">
                            <span className="text-sm text-slate-500">
                                Current status
                            </span>
                            <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800">
                                Pending Review
                            </span>
                        </div>

                        <div className="flex justify-between gap-4">
                            <span className="text-sm text-slate-500">
                                New status
                            </span>
                            <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800">
                                {approved ? "Active (Demo)" : "Active after approval"}
                            </span>
                        </div>
                    </div>

                    {approved ? (
                        <div
                            role="status"
                            className="mt-6 rounded-xl border border-emerald-200 bg-emerald-50 p-4"
                        >
                            <h2 className="font-semibold text-emerald-800">
                                Demo approval completed
                            </h2>
                            <p className="mt-1 text-sm text-emerald-700">
                                The interface has recorded a local demo result.
                                The backend has not been updated.
                            </p>

                            <button
                                onClick={() =>
                                    router.push("/admin/parking-lots/pending")
                                }
                                className="mt-4 rounded-lg bg-emerald-700 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-800"
                            >
                                Back to Parking Lots
                            </button>
                        </div>
                    ) : (
                        <div className="mt-6 flex flex-wrap justify-end gap-3">
                            <button
                                onClick={() =>
                                    router.push(`/admin/parking-lots/${encodeURIComponent(id)}`)
                                }
                                className="rounded-lg border border-slate-300 px-5 py-2.5 text-sm font-semibold hover:bg-slate-50"
                            >
                                Cancel
                            </button>

                            <button
                                onClick={() => setShowConfirm(true)}
                                className="rounded-lg bg-emerald-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-emerald-700"
                            >
                                Approve Parking Lot
                            </button>
                        </div>
                    )}
                </section>

                {showConfirm && (
                    <div
                        className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4"
                        role="presentation"
                    >
                        <section
                            role="dialog"
                            aria-modal="true"
                            aria-labelledby="confirm-title"
                            className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl"
                        >
                            <h2 id="confirm-title" className="text-xl font-bold">
                                Confirm approval
                            </h2>

                            <p className="mt-3 text-sm leading-6 text-slate-600">
                                Are you sure you want to approve parking lot {id}?
                                The intended status will change to Active.
                            </p>

                            <div className="mt-6 flex justify-end gap-3">
                                <button
                                    disabled={loading}
                                    onClick={() => setShowConfirm(false)}
                                    className="rounded-lg border border-slate-300 px-4 py-2.5 text-sm font-semibold"
                                >
                                    Go Back
                                </button>

                                <button
                                    disabled={loading}
                                    onClick={handleApprove}
                                    className="rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-50"
                                >
                                    {loading ? "Processing..." : "Confirm Approval"}
                                </button>
                            </div>
                        </section>
                    </div>
                )}
            </div>
        </main>
    );
}
