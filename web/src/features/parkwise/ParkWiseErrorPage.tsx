import Image from "next/image";
import Link from "next/link";
import { Manrope } from "next/font/google";
import { ParkWiseFooter } from "./ParkWiseFooter";
import { ParkWiseNavbar } from "./ParkWiseNavbar";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

type ErrorPageKind = "forbidden" | "not-found" | "unexpected";

const content: Record<
    ErrorPageKind,
    {
        code: string;
        eyebrow: string;
        title: string;
        description: string;
        secondaryLabel: string;
        secondaryHref: string;
    }
> = {
    forbidden: {
        code: "403",
        eyebrow: "ACCESS RESTRICTED",
        title: "This space isn't available to you.",
        description:
            "You don't have access to this page. If you have another ParkWise account, sign in with that account and try again.",
        secondaryLabel: "Sign in",
        secondaryHref: "/login",
    },
    "not-found": {
        code: "404",
        eyebrow: "WRONG TURN",
        title: "We couldn't find this page.",
        description:
            "The link may be outdated or the address may have changed. Let's get you back on the right route.",
        secondaryLabel: "How ParkWise works",
        secondaryHref: "/#how-it-works",
    },
    unexpected: {
        code: "!",
        eyebrow: "TEMPORARY DETOUR",
        title: "Something went wrong.",
        description:
            "This page couldn't load right now. Please try again, or return to the ParkWise home page.",
        secondaryLabel: "Back to home",
        secondaryHref: "/",
    },
};

export function ParkWiseErrorPage({
    kind,
    onRetry,
}: {
    kind: ErrorPageKind;
    onRetry?: () => void;
}) {
    const page = content[kind];

    return (
        <div
            className={`parkwise-page parkwise-error-page ${manrope.className}`}
        >
            <ParkWiseNavbar variant="login" />
            <main className="parkwise-error-main">
                <div className="parkwise-container parkwise-error-layout">
                    <div className="parkwise-error-copy">
                        <p className="parkwise-error-eyebrow">
                            <span
                                className="parkwise-error-eyebrow-line"
                                aria-hidden="true"
                            />
                            {page.eyebrow} <span aria-hidden="true">/</span>{" "}
                            {page.code}
                        </p>
                        <h1>{page.title}</h1>
                        <p className="parkwise-error-description">
                            {page.description}
                        </p>
                        <div className="parkwise-error-actions">
                            {onRetry ? (
                                <button
                                    className="parkwise-error-primary"
                                    type="button"
                                    onClick={onRetry}
                                >
                                    Try again
                                    <span aria-hidden="true">↗</span>
                                </button>
                            ) : (
                                <Link
                                    className="parkwise-error-primary"
                                    href="/"
                                >
                                    Back to home
                                    <span aria-hidden="true">↗</span>
                                </Link>
                            )}
                            <Link
                                className="parkwise-error-secondary"
                                href={page.secondaryHref}
                            >
                                {page.secondaryLabel}
                            </Link>
                        </div>
                    </div>

                    <div className="parkwise-error-visual" aria-hidden="true">
                        <Image
                            src="/images/parkwise/hero-parking-garage.png"
                            alt=""
                            fill
                            sizes="(max-width: 767px) 100vw, 50vw"
                            className="parkwise-error-image"
                        />
                        <div className="parkwise-error-visual-shade" />
                        <span className="parkwise-error-visual-number">
                            {page.code}
                        </span>
                        <div className="parkwise-error-visual-caption">
                            <span className="parkwise-error-visual-marker" />
                            <span>Find your way back with ParkWise</span>
                        </div>
                    </div>
                </div>
            </main>
            <ParkWiseFooter />
        </div>
    );
}
