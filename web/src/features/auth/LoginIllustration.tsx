import Image from "next/image";

export function LoginIllustration() {
    return (
        <div className="relative h-[320px] overflow-hidden rounded-2xl border border-[var(--login-border)] bg-[var(--login-visual)] shadow-lg sm:h-[420px] lg:col-span-7 lg:h-full">
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
        </div>
    );
}
