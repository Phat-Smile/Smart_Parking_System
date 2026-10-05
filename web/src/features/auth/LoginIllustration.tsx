import Image from "next/image";
import { AuthGlyph } from "./AuthGlyph";

export function LoginIllustration() {
    return (
        <div className="relative h-[320px] overflow-hidden rounded-2xl border border-[var(--login-border)] bg-[var(--login-visual)] shadow-lg sm:h-[420px] lg:col-span-7 lg:h-full lg:min-h-[553px]">
            <Image
                src="/images/guest/hero-isometric.png"
                alt="Isometric smart parking structure at dusk"
                fill
                sizes="(max-width: 1023px) 100vw, 60vw"
                className="object-cover object-center opacity-0 transition-opacity duration-500 dark:opacity-100"
            />
            <Image
                src="/images/auth/parking-daylight.png"
                alt="Isometric smart parking structure in daylight"
                fill
                sizes="(max-width: 1023px) 100vw, 60vw"
                className="object-cover object-center opacity-100 transition-opacity duration-500 dark:opacity-0"
            />
            <div className="pointer-events-none absolute inset-0 bg-[image:var(--login-vignette)]" />

            <div
                className="absolute top-4 left-4 flex items-center gap-3 rounded-xl border border-[var(--login-badge-border)] bg-[var(--login-badge-bg)] px-3.5 py-2 shadow-xl backdrop-blur-xl"
                aria-label="Design preview: 156 spaces available"
            >
                <span className="relative flex h-3 w-3 items-center justify-center">
                    <span className="absolute h-3 w-3 animate-ping rounded-full bg-[var(--login-live)] opacity-75" />
                    <span className="relative h-2 w-2 rounded-full bg-[var(--login-live)]" />
                </span>
                <span className="flex flex-col">
                    <span className="flex items-baseline gap-1.5 leading-none">
                        <span className="font-heading text-base font-bold tracking-tight">
                            156
                        </span>
                        <span className="font-mono text-[10px] font-semibold text-[var(--login-live)] uppercase">
                            Live
                        </span>
                    </span>
                    <span className="mt-1 font-mono text-[11px] leading-none text-[var(--login-muted)]">
                        spaces available
                    </span>
                </span>
            </div>

            <div className="absolute bottom-4 left-4 flex items-center gap-2 rounded-lg border border-[var(--login-border)] bg-[var(--login-badge-bg)] px-3 py-1 font-mono text-[11px] text-[var(--login-muted)] backdrop-blur-md">
                <AuthGlyph
                    name="sensor"
                    className="h-[15px] w-[15px] text-[var(--login-accent)]"
                />
                IoT Telemetry Node // Sector 4A Active
            </div>
        </div>
    );
}
