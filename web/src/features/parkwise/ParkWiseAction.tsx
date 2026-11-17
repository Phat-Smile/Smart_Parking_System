import Link from "next/link";
import type { ButtonHTMLAttributes, ComponentProps } from "react";
import "./parkwise-controls.css";

type Appearance = {
    variant?: "primary" | "secondary";
    className?: string;
    iconOnly?: boolean;
};
type ActionProps = Appearance &
    (
        | (ButtonHTMLAttributes<HTMLButtonElement> & { href?: never })
        | ComponentProps<typeof Link>
    );

/** One treatment for ParkWise actions, whether navigation links or buttons. */
export function ParkWiseAction(props: ActionProps) {
    const {
        variant = "primary",
        className = "",
        iconOnly = false,
        ...rest
    } = props;
    const classes =
        `parkwise-action${iconOnly ? " parkwise-action-icon" : ""} ${className}`.trim();
    if ("href" in rest && rest.href !== undefined) {
        return (
            <Link
                {...(rest as ComponentProps<typeof Link>)}
                className={classes}
                data-variant={variant}
            />
        );
    }
    return (
        <button
            type="button"
            {...(rest as ButtonHTMLAttributes<HTMLButtonElement>)}
            className={classes}
            data-variant={variant}
        />
    );
}
