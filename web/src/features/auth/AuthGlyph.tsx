import type { SVGProps } from "react";

type Glyph =
    | "parking"
    | "sun"
    | "moon"
    | "person"
    | "mail"
    | "lock"
    | "eye"
    | "eyeOff"
    | "arrow"
    | "car"
    | "shield"
    | "pin"
    | "sensor";

const artwork: Record<Glyph, React.ReactNode> = {
    parking: <path d="M7 21V3h7a5 5 0 0 1 0 10H7m0-5h7" />,
    sun: (
        <>
            <circle cx="12" cy="12" r="4" />
            <path d="M12 2v2m0 16v2M2 12h2m16 0h2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4m0-14.2-1.4 1.4M6.3 17.7l-1.4 1.4" />
        </>
    ),
    moon: <path d="M20 15.2A8.5 8.5 0 0 1 8.8 4 8.5 8.5 0 1 0 20 15.2Z" />,
    person: (
        <>
            <circle cx="12" cy="8" r="3.3" />
            <path d="M5.5 20a6.5 6.5 0 0 1 13 0" />
        </>
    ),
    mail: (
        <>
            <rect x="3" y="5" width="18" height="14" rx="2" />
            <path d="m4 7 8 6 8-6" />
        </>
    ),
    lock: (
        <>
            <rect x="5" y="10" width="14" height="11" rx="2" />
            <path d="M8 10V7a4 4 0 0 1 8 0v3" />
        </>
    ),
    eye: (
        <>
            <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
            <circle cx="12" cy="12" r="2.5" />
        </>
    ),
    eyeOff: (
        <>
            <path d="M3 3 21 21M10.6 6.1C11.1 6 11.5 6 12 6c6.5 0 10 6 10 6a17 17 0 0 1-3.3 3.7M6.3 8.3A18 18 0 0 0 2 12s3.5 6 10 6c1.3 0 2.5-.2 3.5-.7" />
            <path d="M10.6 10.6a2 2 0 0 0 2.8 2.8" />
        </>
    ),
    arrow: <path d="M4 12h16m-6-6 6 6-6 6" />,
    car: (
        <>
            <path d="m5 11 1.4-5h11.2l1.4 5M4 17v2m16-2v2M4 11h16v6H4z" />
            <path d="M7 14h2m6 0h2" />
        </>
    ),
    shield: (
        <>
            <path d="m12 2 8 4v5c0 5-3.5 8.5-8 11-4.5-2.5-8-6-8-11V6l8-4Z" />
            <path d="m9 12 2 2 4-4" />
        </>
    ),
    pin: (
        <>
            <path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z" />
            <circle cx="12" cy="10" r="2.5" />
        </>
    ),
    sensor: (
        <>
            <circle cx="12" cy="12" r="2" />
            <path d="M7 7a7 7 0 0 0 0 10m10-10a7 7 0 0 1 0 10M4 4a11 11 0 0 0 0 16m16-16a11 11 0 0 0 0 16" />
        </>
    ),
};

export function AuthGlyph({
    name,
    ...props
}: { name: Glyph } & SVGProps<SVGSVGElement>) {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
            {...props}
        >
            {artwork[name]}
        </svg>
    );
}
