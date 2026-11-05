import { redirect } from "next/navigation";

export default async function SearchRedirect({
    searchParams,
}: {
    searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
    const params = await searchParams;
    const query = new URLSearchParams();
    for (const key of ["destination", "date", "time", "vehicle"]) {
        const value = params[key];
        if (typeof value === "string") query.set(key, value);
    }
    redirect(`/search${query.size ? `?${query}` : ""}`);
}
