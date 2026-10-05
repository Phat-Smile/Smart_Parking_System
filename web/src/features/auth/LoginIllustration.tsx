import Image from "next/image";
import { LoginIcon } from "./LoginIcon";

export function LoginIllustration() {
    return (
        <div className="relative flex min-h-[360px] items-center justify-center overflow-hidden rounded-3xl border border-[var(--auth-panel-border)] bg-[var(--auth-visual)] shadow-xl sm:min-h-[500px] lg:col-span-7 lg:min-h-[640px]">
            <Image
                src="/images/guest/hero-isometric.png"
                alt="Isometric parking structure at dusk"
                fill
                loading="eager"
                sizes="(max-width: 1023px) 100vw, 55vw"
                className="object-cover object-center opacity-0 transition-opacity duration-500 dark:opacity-100"
            />
            <Image
                src="/images/auth/parking-daylight.png"
                alt="Isometric parking structure in daylight"
                fill
                loading="eager"
                sizes="(max-width: 1023px) 100vw, 55vw"
                className="object-cover object-center opacity-100 transition-opacity duration-500 dark:opacity-0"
            />
            <div className="pointer-events-none absolute inset-0 bg-[image:var(--auth-vignette)]" />

            <div
                className="absolute top-6 left-6 z-10 flex items-center gap-3.5 rounded-2xl border border-[var(--auth-panel-border)] bg-[var(--auth-panel)] p-4 shadow-2xl backdrop-blur-xl"
                aria-label="Design preview: 156 spaces available"
            >
                <span className="relative flex h-3.5 w-3.5 items-center justify-center">
                    <span className="absolute h-3.5 w-3.5 animate-ping rounded-full bg-[var(--auth-live)] opacity-75" />
                    <span className="relative h-2.5 w-2.5 rounded-full bg-[var(--auth-live)]" />
                </span>
                <span className="flex flex-col">
                    <span className="flex items-baseline gap-1.5">
                        <span className="font-heading text-xl font-bold tracking-tight text-[var(--auth-text)]">
                            156
                        </span>
                        <span className="font-mono text-[10px] font-semibold tracking-wider text-[var(--auth-live)] uppercase">
                            Live
                        </span>
                    </span>
                    <span className="font-mono text-xs text-[var(--auth-text-muted)]">
                        spaces available
                    </span>
                </span>
            </div>

            <div className="absolute bottom-6 left-6 z-10 hidden items-center gap-2 rounded-lg border border-[var(--auth-panel-border)] bg-[var(--auth-panel)] px-3 py-1.5 backdrop-blur-md sm:flex">
                <LoginIcon
                    name="sensor"
                    className="h-4 w-4 text-[var(--auth-accent)]"
                />
                <span className="font-mono text-[10px] text-[var(--auth-text-muted)]">
                    IoT Telemetry Node // Sector 4A Active
                </span>
            </div>
        </div>
    );
}
