import React, { ReactNode } from "react";

interface PageHeaderProps {
    breadcrumb?: string;
    title: string;
    subtitle: string | ReactNode;
    actions?: ReactNode;
}

export function PageHeader({
    title,
    subtitle,
    actions,
}: PageHeaderProps) {
    return (
        <div className="ph">
            <div>
                <h2>{title}</h2>
                <p className="mut">{subtitle}</p>
            </div>
            {actions && <div>{actions}</div>}
        </div>
    );
}
