import Link from "next/link";

export default function NotFound() {
    return (
        <main className="mx-auto flex min-h-screen max-w-4xl flex-col justify-center gap-4 px-6">
            <h1 className="text-3xl font-semibold">Page not found</h1>
            <p className="text-muted">The requested page does not exist.</p>
            <Link className="text-primary underline" href="/">
                Return home
            </Link>
        </main>
    );
}
