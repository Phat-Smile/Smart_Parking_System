type IconName =
    | "parking"
    | "sun"
    | "moon"
    | "person"
    | "mail"
    | "lock"
    | "eye"
    | "eyeOff"
    | "arrow"
    | "bolt"
    | "shield"
    | "car"
    | "signal"
    | "sparkle";

export function RegisterIcon({
    name,
    className = "h-4 w-4",
}: {
    name: IconName;
    className?: string;
}) {
    const paths: Record<IconName, React.ReactNode> = {
        parking: <path d="M6 20V4h7a5 5 0 0 1 0 10H6m0-4h7" />,
        sun: (
            <>
                <circle cx="12" cy="12" r="4" />
                <path d="M12 2v2m0 16v2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M2 12h2m16 0h2M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42" />
            </>
        ),
        moon: (
            <path d="M20.5 15.5A8.5 8.5 0 0 1 8.5 3.5 8.5 8.5 0 1 0 20.5 15.5Z" />
        ),
        person: (
            <>
                <circle cx="12" cy="8" r="3.5" />
                <path d="M5 21a7 7 0 0 1 14 0" />
            </>
        ),
        mail: (
            <>
                <rect x="3" y="5" width="18" height="14" rx="2" />
                <path d="m3 7 9 6 9-6" />
            </>
        ),
        lock: (
            <>
                <rect x="5" y="10" width="14" height="11" rx="2" />
                <path d="M8 10V7a4 4 0 1 1 8 0v3" />
            </>
        ),
        eye: (
            <>
                <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6-10-6-10-6Z" />
                <circle cx="12" cy="12" r="2.5" />
            </>
        ),
        eyeOff: (
            <>
                <path d="m3 3 18 18M10.6 6.1A11 11 0 0 1 12 6c6.5 0 10 6 10 6a15 15 0 0 1-3.1 3.6M6.1 8.1C3.5 9.7 2 12 2 12s3.5 6 10 6a10.5 10.5 0 0 0 3.7-.7" />
            </>
        ),
        arrow: (
            <>
                <path d="M4 12h16m-6-6 6 6-6 6" />
            </>
        ),
        bolt: <path d="m13 2-8 11h6l-1 9 9-12h-6l0-8Z" />,
        shield: (
            <>
                <path d="m12 2 8 4v6c0 5-3.5 8-8 10-4.5-2-8-5-8-10V6l8-4Z" />
                <path d="m9 12 2 2 4-4" />
            </>
        ),
        car: (
            <>
                <path d="m5 11 2-5h10l2 5M5 11h14l2 3v5h-2v-2H5v2H3v-5l2-3Z" />
                <path d="M7 14h1m8 0h1" />
            </>
        ),
        signal: (
            <>
                <path d="M3 18h2v3H3zm5-4h2v7H8zm5-4h2v11h-2zm5-7h2v18h-2z" />
            </>
        ),
        sparkle: (
            <>
                <path d="m12 2 2 7 7 3-7 3-2 7-2-7-7-3 7-3 2-7Z" />
            </>
        ),
    };

    return (
        <svg
            className={className}
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            {paths[name]}
        </svg>
    );
}
