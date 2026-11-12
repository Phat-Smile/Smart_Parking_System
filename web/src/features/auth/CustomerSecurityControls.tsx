"use client";

import { useEffect, useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import QRCode from "react-qr-code";
import { clearAuthSession } from "./authSession";
import {
    confirmMfa,
    disableMfa,
    getAccountSessions,
    getMfaEnabled,
    regenerateMfaRecoveryCodes,
    revokeAccountSession,
    setupMfa,
    type AccountSession,
} from "./customerSecurityApi";

export function CustomerSecurityControls() {
    const router = useRouter();
    const [sessions, setSessions] = useState<AccountSession[]>([]);
    const [mfaEnabled, setMfaEnabled] = useState<boolean | null>(null);
    const [setup, setSetup] = useState<{
        secret: string;
        otpauthUri: string;
    } | null>(null);
    const [code, setCode] = useState("");
    const [password, setPassword] = useState("");
    const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);
    const [mustSignInAgain, setMustSignInAgain] = useState(false);
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState("");

    useEffect(() => {
        let active = true;
        void Promise.all([getAccountSessions(), getMfaEnabled()])
            .then(([list, account]) => {
                if (active) {
                    setSessions(list.sessions);
                    setMfaEnabled(account.mfaEnabled);
                }
            })
            .catch(() => {
                if (active) setMessage("Could not load security settings.");
            });
        return () => {
            active = false;
        };
    }, []);

    async function startSetup() {
        setBusy(true);
        setMessage("");
        try {
            setSetup(await setupMfa());
        } catch {
            setMessage("Could not start two factor setup.");
        } finally {
            setBusy(false);
        }
    }

    async function submitMfa(
        e: FormEvent,
        action: "confirm" | "disable" | "recovery",
    ) {
        e.preventDefault();
        setBusy(true);
        setMessage("");
        try {
            if (action === "confirm") {
                const result = await confirmMfa(code.trim());
                setRecoveryCodes(result.recoveryCodes);
                setSetup(null);
                setMfaEnabled(true);
                setCode("");
                // Confirming MFA revokes the server session. Keep this view until the
                // one-time recovery codes have been saved by the customer.
                setMustSignInAgain(true);
            } else if (action === "disable") {
                await disableMfa(password, code.trim());
                setMfaEnabled(false);
                setPassword("");
                setCode("");
                setRecoveryCodes([]);
                setMessage("Two factor authentication disabled.");
            } else {
                const result = await regenerateMfaRecoveryCodes(code.trim());
                setRecoveryCodes(result.recoveryCodes);
                setCode("");
            }
        } catch {
            setMessage("Security change failed. Check your code and retry.");
        } finally {
            setBusy(false);
        }
    }

    async function revoke(id: string) {
        setBusy(true);
        setMessage("");
        try {
            await revokeAccountSession(id);
            setSessions((current) => current.filter((item) => item.id !== id));
            setMessage("Device signed out.");
        } catch {
            setMessage("Could not revoke that session.");
        } finally {
            setBusy(false);
        }
    }

    return (
        <div style={{ display: "grid", gap: 24, marginTop: 32 }}>
            <section aria-labelledby="account-sessions-title">
                <h2 id="account-sessions-title">Signed in devices</h2>
                {sessions.map((item) => (
                    <div
                        key={item.id}
                        style={{
                            paddingBlock: 10,
                            borderBottom: "1px solid #ddd",
                        }}
                    >
                        <strong>{item.device || "Unknown device"}</strong>{" "}
                        {item.isCurrent && "(this device)"}
                        <small style={{ display: "block" }}>
                            Last active:{" "}
                            {new Date(item.lastActivityAtUtc).toLocaleString(
                                "vi-VN",
                            )}
                        </small>
                        {!item.isCurrent && (
                            <button
                                type="button"
                                disabled={busy}
                                onClick={() => void revoke(item.id)}
                            >
                                Sign out device
                            </button>
                        )}
                    </div>
                ))}
            </section>
            <section aria-labelledby="mfa-settings-title">
                <h2 id="mfa-settings-title">Two factor authentication</h2>
                {mfaEnabled === false && !setup && (
                    <button
                        type="button"
                        disabled={busy}
                        onClick={() => void startSetup()}
                    >
                        Set up authenticator
                    </button>
                )}
                {setup && (
                    <div>
                        <p>
                            Scan this QR in your authenticator app, then enter
                            its six digit code.
                        </p>
                        <div
                            style={{
                                background: "white",
                                padding: 12,
                                width: "fit-content",
                            }}
                        >
                            <QRCode value={setup.otpauthUri} size={150} />
                        </div>
                        <p>
                            Manual setup key: <code>{setup.secret}</code>
                        </p>
                        <form onSubmit={(e) => void submitMfa(e, "confirm")}>
                            <label>
                                Authenticator code{" "}
                                <input
                                    value={code}
                                    onChange={(e) => setCode(e.target.value)}
                                    inputMode="numeric"
                                    autoComplete="one-time-code"
                                    required
                                />
                            </label>
                            <button type="submit" disabled={busy}>
                                Enable two factor authentication
                            </button>
                        </form>
                    </div>
                )}
                {mfaEnabled && !setup && recoveryCodes.length === 0 && (
                    <>
                        <p>Two factor authentication is enabled.</p>
                        <form onSubmit={(e) => void submitMfa(e, "recovery")}>
                            <label>
                                Authenticator code{" "}
                                <input
                                    value={code}
                                    onChange={(e) => setCode(e.target.value)}
                                    inputMode="numeric"
                                    autoComplete="one-time-code"
                                    required
                                />
                            </label>
                            <button type="submit" disabled={busy}>
                                Generate new recovery codes
                            </button>
                        </form>
                        <form onSubmit={(e) => void submitMfa(e, "disable")}>
                            <label>
                                Current password{" "}
                                <input
                                    type="password"
                                    value={password}
                                    onChange={(e) =>
                                        setPassword(e.target.value)
                                    }
                                    autoComplete="current-password"
                                />
                            </label>
                            <label>
                                Authenticator code{" "}
                                <input
                                    value={code}
                                    onChange={(e) => setCode(e.target.value)}
                                    inputMode="numeric"
                                    autoComplete="one-time-code"
                                    required
                                />
                            </label>
                            <button type="submit" disabled={busy}>
                                Disable two factor authentication
                            </button>
                        </form>
                    </>
                )}
                {recoveryCodes.length > 0 && (
                    <div role="status">
                        <p>
                            Save these recovery codes now. They will only be
                            shown once.
                        </p>
                        <ul>
                            {recoveryCodes.map((item) => (
                                <li key={item}>
                                    <code>{item}</code>
                                </li>
                            ))}
                        </ul>
                        <button
                            type="button"
                            onClick={() => {
                                setRecoveryCodes([]);
                                if (mustSignInAgain) {
                                    clearAuthSession();
                                    router.replace("/login");
                                }
                            }}
                        >
                            I have saved the codes
                        </button>
                    </div>
                )}
            </section>
            {message && <p role="status">{message}</p>}
        </div>
    );
}
