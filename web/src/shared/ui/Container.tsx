import React from "react";

export interface ContainerProps extends React.HTMLAttributes<HTMLDivElement> {
    children: React.ReactNode;
    as?: React.ElementType;
}

export function Container({
    children,
    as: Component = "div",
    className = "",
    ...props
}: ContainerProps) {
    return React.createElement(
        Component,
        {
            className:
                `max-w-[1240px] mx-auto px-4 sm:px-6 lg:px-8 ${className}`.trim(),
            ...props,
        },
        children,
    );
}
