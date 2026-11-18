import { ParkWiseLogo } from "./ParkWiseLogo";
import Link from "next/link";
import type { ReactNode } from "react";

/** Shared brand/header structure for the landing page and Customer layouts. */
export function ParkWiseHeader({
    className,
    rowClassName,
    brandClassName = "parkwise-brand",
    brandHref = "/",
    onBrandClick,
    afterRow,
    children,
}: {
    className: string;
    rowClassName: string;
    brandClassName?: string;
    brandHref?: string;
    onBrandClick?: () => void;
    afterRow?: ReactNode;
    children: ReactNode;
}) {
    return (
        <header className={className}>
            <div className={rowClassName}>
                <Link
                    href={brandHref}
                    className={brandClassName}
                    aria-label="ParkWise home"
                    onClick={onBrandClick}
                >
                    <ParkWiseLogo priority />
                </Link>
                {children}
            </div>
            {afterRow}
        </header>
    );
}
