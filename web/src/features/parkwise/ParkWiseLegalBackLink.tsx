"use client";

import { useRouter } from "next/navigation";

export function ParkWiseLegalBackLink({
    fromProfile,
    className,
}: {
    fromProfile: boolean;
    className: string;
}) {
    const router = useRouter();
    function returnToPreviousPage() {
        if (fromProfile) {
            // Complete Profile opens legal documents in a new tab to keep the form mounted.
            window.close();
            window.setTimeout(() => {
                if (window.closed) return;
                if (window.history.length > 1) router.back();
                else router.push("/customer/complete-profile");
            }, 100);
            return;
        }

        if (window.history.length > 1) router.back();
        else router.push("/");
    }

    return (
        <button
            type="button"
            className={className}
            onClick={returnToPreviousPage}
        >
            <span aria-hidden="true">←</span> Back to previous page
        </button>
    );
}
