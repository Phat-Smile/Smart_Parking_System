import Link from "next/link";
import { Manrope } from "next/font/google";
import { ParkWiseFooter } from "./ParkWiseFooter";
import { ParkWiseNavbar } from "./ParkWiseNavbar";
import { getLegalDocumentInfo, LegalDocument } from "./legal/LegalDocument";

const manrope = Manrope({ subsets: ["latin"], display: "swap" });

export function ParkWiseLegalPage({
    title,
    markdown,
}: {
    title: string;
    markdown: string;
}) {
    const { effectiveDate, lastUpdated, sections } =
        getLegalDocumentInfo(markdown);

    return (
        <div className={`parkwise-page ${manrope.className}`}>
            <ParkWiseNavbar />
            <main className="parkwise-legal-main">
                <div className="parkwise-legal-hero">
                    <div className="parkwise-container parkwise-legal-hero-inner">
                        <div className="parkwise-legal-hero-copy">
                            <p className="parkwise-legal-eyebrow">
                                PARKWISE / LEGAL
                            </p>
                            <h1>{title}</h1>
                            <div className="parkwise-legal-dates">
                                <p>
                                    <span>Effective date</span>
                                    <strong>{effectiveDate}</strong>
                                </p>
                                <p>
                                    <span>Last updated</span>
                                    <strong>{lastUpdated}</strong>
                                </p>
                            </div>
                        </div>
                        <span
                            className="parkwise-legal-hero-mark"
                            aria-hidden="true"
                        >
                            P
                        </span>
                    </div>
                </div>
                <div className="parkwise-container parkwise-legal-content">
                    <aside className="parkwise-legal-sidebar">
                        <nav
                            className="parkwise-legal-desktop-toc"
                            aria-label={`${title} sections`}
                        >
                            <p className="parkwise-legal-sidebar-label">
                                ON THIS PAGE
                            </p>
                            <ol>
                                {sections.map(
                                    ({ number, title: sectionTitle, id }) => (
                                        <li key={id}>
                                            <a href={`#${id}`}>
                                                <span>
                                                    {number.padStart(2, "0")}
                                                </span>
                                                {sectionTitle}
                                            </a>
                                        </li>
                                    ),
                                )}
                            </ol>
                        </nav>
                        <details className="parkwise-legal-mobile-toc">
                            <summary>
                                <span>ON THIS PAGE</span>
                                <span>{sections.length} sections</span>
                            </summary>
                            <nav aria-label={`${title} sections`}>
                                <ol>
                                    {sections.map(
                                        ({
                                            number,
                                            title: sectionTitle,
                                            id,
                                        }) => (
                                            <li key={id}>
                                                <a href={`#${id}`}>
                                                    <span>
                                                        {number.padStart(
                                                            2,
                                                            "0",
                                                        )}
                                                    </span>
                                                    {sectionTitle}
                                                </a>
                                            </li>
                                        ),
                                    )}
                                </ol>
                            </nav>
                        </details>
                    </aside>
                    <div className="parkwise-legal-reading-column">
                        <LegalDocument markdown={markdown} />
                        <Link href="/" className="parkwise-legal-home-link">
                            <span aria-hidden="true">←</span> Back to home
                        </Link>
                    </div>
                </div>
            </main>
            <ParkWiseFooter />
        </div>
    );
}
