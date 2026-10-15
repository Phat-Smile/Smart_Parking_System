import { Fragment, type ReactNode } from "react";

export function getLegalDocumentInfo(markdown: string) {
    return {
        effectiveDate:
            markdown.match(/\*\*Effective Date:\*\*\s*([^\n]+)/)?.[1]?.trim() ??
            "",
        lastUpdated:
            markdown.match(/\*\*Last Updated:\*\*\s*([^\n]+)/)?.[1]?.trim() ??
            "",
        sections: [...markdown.matchAll(/^### (\d+)\. (.+)$/gm)].map(
            ([, number, title]) => ({ number, title, id: `section-${number}` }),
        ),
    };
}

function renderInline(text: string): ReactNode[] {
    return text
        .split(/(\*\*[^*]+\*\*)/g)
        .map((part, index) =>
            part.startsWith("**") && part.endsWith("**") ? (
                <strong key={index}>{part.slice(2, -2)}</strong>
            ) : (
                part
            ),
        );
}

function renderParagraph(block: string) {
    const lines = block.split(/ {2,}\n/);

    return (
        <p
            className={
                block.startsWith("**Operator:**")
                    ? "parkwise-legal-contact"
                    : undefined
            }
        >
            {lines.map((line, index) => (
                <Fragment key={index}>
                    {index > 0 && <br />}
                    {renderInline(line.replace(/\n/g, " ").trim())}
                </Fragment>
            ))}
        </p>
    );
}

function renderTable(block: string) {
    const rows = block.split("\n").map((line) =>
        line
            .split("|")
            .slice(1, -1)
            .map((cell) => cell.trim()),
    );
    const [headings, , ...body] = rows;

    return (
        <div className="parkwise-legal-table-wrap">
            <table>
                <thead>
                    <tr>
                        {headings.map((heading) => (
                            <th key={heading} scope="col">
                                {renderInline(heading)}
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {body.map((row, rowIndex) => (
                        <tr key={rowIndex}>
                            {row.map((cell, cellIndex) => (
                                <td key={cellIndex}>{renderInline(cell)}</td>
                            ))}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function renderBlock(block: string, index: number) {
    if (block.startsWith("# ")) return null;
    if (block.startsWith("**Effective Date:**")) return null;

    if (block.startsWith("### ")) {
        const heading = block.slice(4).trim();
        const sectionNumber = heading.match(/^(\d+)\. (.+)$/);
        return (
            <h2
                key={index}
                id={sectionNumber ? `section-${sectionNumber[1]}` : undefined}
            >
                {sectionNumber ? (
                    <>
                        <span className="parkwise-legal-section-number">
                            {sectionNumber[1].padStart(2, "0")}
                        </span>
                        <span>{sectionNumber[2]}</span>
                    </>
                ) : (
                    heading
                )}
            </h2>
        );
    }

    if (block.startsWith("- ")) {
        return (
            <ul key={index}>
                {block.split("\n").map((line, itemIndex) => (
                    <li key={itemIndex}>{renderInline(line.slice(2))}</li>
                ))}
            </ul>
        );
    }

    if (block.startsWith("|"))
        return <Fragment key={index}>{renderTable(block)}</Fragment>;

    if (/^\*\*\d+\.\d+\..+\*\*$/.test(block)) {
        return <h3 key={index}>{block.slice(2, -2)}</h3>;
    }

    return <Fragment key={index}>{renderParagraph(block)}</Fragment>;
}

export function LegalDocument({ markdown }: { markdown: string }) {
    const blocks = markdown
        .replace(/\r\n/g, "\n")
        .trim()
        .split(/\n\s*\n/);

    return (
        <article className="parkwise-legal-document">
            {blocks.map(renderBlock)}
        </article>
    );
}
