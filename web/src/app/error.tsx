"use client";

import { ParkWiseErrorPage } from "@/features/parkwise/ParkWiseErrorPage";
import "./parkwise.css";
import "./error-pages.css";

export default function ErrorPage({ reset }: { reset: () => void }) {
    return <ParkWiseErrorPage kind="unexpected" onRetry={reset} />;
}
