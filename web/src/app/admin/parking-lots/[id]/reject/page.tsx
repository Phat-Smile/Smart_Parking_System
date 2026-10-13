
"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";

export default function RejectParkingLotPage() {
    const params = useParams<{ id: string }>();
    const router = useRouter();
    const id = decodeURIComponent(params.id);

    const [reason, setReason] = useState("");
    const [error, setError] = useState("");
    const [showConfirm, setShowConfirm] = useState(false);
    const [rejected, setRejected] = useState(false);

    function handleContinue() {
        if (!reason.trim()) {
            setError("Please enter a reason for rejecting this parking lot.");
            return;
        }

        if (reason.trim().length < 10) {
            setError("Please enter at least 10 characters.");
            return;
        }

        setError("");
        setShowConfirm(true);
    }

    function handleConfirmReject() {
        // Demo only. Connect the real rejection API here.
        setRejected(true);
        setShowConfirm(false);
    }

    return (
        <main className="min-h-screen bg-slate-50 p-4 text-slate-900 sm:p-6">
            <div className="mx-auto max-w-3xl space-y-6">
                <Link
                    href={`/admin/parking-lots/${encodeURIComponent(id)}`}
                    className="inline-flex items-center gap-2 text-sm font-medium text-blue-600 hover:underline"
                >
                    ← Back to Parking Lot Detail
                </Link>

                <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                    <div className="border-b border-slate-100 p-6 sm:p-8">
                        <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-red-100 text-2xl text-red-700">
                            !
                        </div>

                        <h1 className="mt-4 text-2xl font-bold">
                            Reject Parking Lot
                        </h1>

                        <p className="mt-2 text-sm leading-6 text-slate-500">
                            Provide a clear reason for rejecting this application.
                            The owner should be able to see the rejection status and
                            reason after the backend records the decision.
                        </p>
                    </div>

                    <div className="space-y-6 p-6 sm:p-8">
                        <div className="grid gap-4 sm:grid-cols-2">
                            <div className="rounded-xl bg-slate-50 p-4">
                                <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Parking Lot ID
                                </p>
                                <p className="mt-2 font-semibold">{id}</p>
                            </div>

                            <div className="rounded-xl bg-slate-50 p-4">
                                <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                                    Current status
                                </p>
                                <p className="mt-2">
                                    <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800">
                                        Pending Review
                                    </span>
                                </p>
                            </div>
                        </div>

                        {!rejected ? (
                            <>
                                <div>
                                    <label
                                        htmlFor="rejection-reason"
                                        className="block text-sm font-semibold text-slate-800"
                                    >
                                        Rejection reason
                                        <span className="ml-1 text-red-600">*</span>
                                    </label>

                                    <p className="mt-1 text-sm text-slate-500">
                                        Explain what needs to be corrected or why the
                                        application does not meet the requirements.
                                    </p>

                                    <textarea
                                        id="rejection-reason"
                                        value={reason}
                                        onChange={(event) => {
                                            setReason(event.target.value);
                                            if (error) setError("");
                                        }}
                                        rows={6}
                                        maxLength={1000}
                                        placeholder="For example: The submitted parking layout does not match the declared capacity. Please provide a corrected layout..."
                                        aria-invalid={Boolean(error)}
                                        aria-describedby="reason-help"
                                        className="mt-3 w-full rounded-xl border border-slate-300 bg-white p-4 text-sm outline-none transition focus:border-red-400 focus:ring-2 focus:ring-red-100"
                                    />

                                    <div className="mt-2 flex justify-between gap-3">
                                        <p
                                            id="reason-help"
                                            className={`text-sm ${error ? "text-red-600" : "text-slate-500"}`}
                                        >
                                            {error || "Minimum 10 characters required."}
                                        </p>
                                        <span className="shrink-0 text-xs text-slate-400">
                                            {reason.length}/1000
                                        </span>
                                    </div>
                                </div>

                                <div className="rounded-xl border border-red-100 bg-red-50 p-4">
                                    <p className="text-sm font-semibold text-red-800">
                                        Before rejecting
                                    </p>
                                    <p className="mt-1 text-sm leading-6 text-red-700">
                                        Check the submitted information carefully. You will
                                        review the reason once more before confirming.
                                    </p>
                                </div>

                                <div className="flex flex-col-reverse justify-end gap-3 sm:flex-row">
                                    <button
                                        type="button"
                                        onClick={() =>
                                            router.push(`/admin/parking-lots/${encodeURIComponent(id)}`)
                                        }
                                        className="rounded-lg border border-slate-300 px-5 py-3 text-sm font-semibold hover:bg-slate-50"
                                    >
                                        Cancel
                                    </button>

                                    <button
                                        type="button"
                                        onClick={handleContinue}
                                        className="rounded-lg bg-red-600 px-5 py-3 text-sm font-semibold text-white hover:bg-red-700"
                                    >
                                        Review Rejection
                                    </button>
                                </div>
                            </>
                        ) : (
                            <div
                                role="status"
                                className="rounded-xl border border-amber-200 bg-amber-50 p-5"
                            >
                                <h2 className="font-bold text-amber-900">
                                    Demo rejection recorded locally
                                </h2>

                                <p className="mt-2 text-sm text-amber-800">
                                    The interface shows the intended Rejected result,
                                    but the backend has not been updated. The owner
                                    will not receive this reason until the real API
                                    and owner-facing status are connected.
                                </p>

                                <div className="mt-4 rounded-lg bg-white p-4">
                                    <p className="text-xs font-semibold uppercase text-slate-500">
                                        Rejection reason
                                    </p>
                                    <p className="mt-2 whitespace-pre-wrap text-sm text-slate-800">
                                        {reason.trim()}
                                    </p>
                                </div>

                                <button
                                    type="button"
                                    onClick={() =>
                                        router.push("/admin/parking-lots/pending")
                                    }
                                    className="mt-5 rounded-lg bg-slate-900 px-5 py-3 text-sm font-semibold text-white hover:bg-slate-700"
                                >
                                    Back to Pending Parking Lots
                                </button>
                            </div>
                        )}
                    </div>
                </section>

                {showConfirm && (
                    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4">
                        <section
                            role="dialog"
                            aria-modal="true"
                            aria-labelledby="confirm-reject-title"
                            className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl"
                        >
                            <div className="flex h-11 w-11 items-center justify-center rounded-full bg-red-100 text-xl text-red-700">
                                !
                            </div>

                            <h2
                                id="confirm-reject-title"
                                className="mt-4 text-xl font-bold"
                            >
                                Confirm rejection
                            </h2>

                            <p className="mt-2 text-sm leading-6 text-slate-600">
                                Are you sure you want to reject parking lot {id}?
                                Please verify the reason before continuing.
                            </p>

                            <div className="mt-4 rounded-lg bg-slate-50 p-4">
                                <p className="text-xs font-semibold uppercase text-slate-500">
                                    Rejection reason
                                </p>
                                <p className="mt-2 whitespace-pre-wrap text-sm">
                                    {reason.trim()}
                                </p>
                            </div>

                            <div className="mt-6 flex justify-end gap-3">
                                <button
                                    type="button"
                                    onClick={() => setShowConfirm(false)}
                                    className="rounded-lg border border-slate-300 px-4 py-2.5 text-sm font-semibold hover:bg-slate-50"
                                >
                                    Go Back
                                </button>

                                <button
                                    type="button"
                                    onClick={handleConfirmReject}
                                    className="rounded-lg bg-red-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-red-700"
                                >
                                    Confirm Rejection
                                </button>
                            </div>
                        </section>
                    </div>
                )}
            </div>
        </main>
    );
}
