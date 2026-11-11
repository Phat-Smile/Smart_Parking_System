/**
 * Bộ phát chuông tín hiệu cổng bãi (Airport / Gate Audio Chime)
 * Sử dụng Web Audio API native, không cần tải bất kỳ file âm thanh nào từ bên ngoài.
 */

let sharedAudioContext: AudioContext | null = null;
const RESUME_TIMEOUT_MS = 1000;

function getAudioContext(): AudioContext | null {
    if (typeof window === "undefined") return null;

    if (!sharedAudioContext) {
        const AudioCtx =
            window.AudioContext ||
            (window as unknown as { webkitAudioContext: typeof AudioContext })
                .webkitAudioContext;
        if (AudioCtx) {
            sharedAudioContext = new AudioCtx();
        }
    }

    if (sharedAudioContext && sharedAudioContext.state === "suspended") {
        try {
            Promise.resolve(sharedAudioContext.resume()).catch(() => {});
        } catch {
            // Bỏ qua nếu autoplay bị chặn
        }
    }

    return sharedAudioContext;
}

async function ensureContextRunning(ctx: AudioContext): Promise<boolean> {
    if (ctx.state === "running") return true;
    if (ctx.state === "closed") return false;

    let timerId: ReturnType<typeof setTimeout> | null = null;
    try {
        return await Promise.race<boolean>([
            Promise.resolve(ctx.resume())
                .then(() => (ctx.state as string) === "running")
                .catch(() => false),
            new Promise<boolean>((resolve) => {
                timerId = setTimeout(() => resolve(false), RESUME_TIMEOUT_MS);
            }),
        ]);
    } catch {
        return false;
    } finally {
        if (timerId !== null) {
            clearTimeout(timerId);
        }
    }
}

/**
 * Phát chuông 2 nốt báo hiệu (Two-tone Chime: D5 -> A5)
 * Tạo sự tập trung cho tài xế trước khi giọng trợ lý bắt đầu nói.
 */
export async function playGateChime(volume = 0.4): Promise<void> {
    const ctx = getAudioContext();
    if (!ctx) return;

    try {
        if (!(await ensureContextRunning(ctx))) {
            return;
        }

        const now = ctx.currentTime;

        // Note 1: D5 (587.33 Hz)
        const osc1 = ctx.createOscillator();
        const gain1 = ctx.createGain();
        osc1.type = "sine";
        osc1.frequency.setValueAtTime(587.33, now);

        gain1.gain.setValueAtTime(0, now);
        gain1.gain.linearRampToValueAtTime(volume * 0.7, now + 0.03);
        gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.25);

        osc1.connect(gain1);
        gain1.connect(ctx.destination);

        osc1.start(now);
        osc1.stop(now + 0.25);

        // Note 2: A5 (880.00 Hz)
        const osc2 = ctx.createOscillator();
        const gain2 = ctx.createGain();
        osc2.type = "sine";
        osc2.frequency.setValueAtTime(880.0, now + 0.16);

        gain2.gain.setValueAtTime(0, now + 0.16);
        gain2.gain.linearRampToValueAtTime(volume, now + 0.19);
        gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.55);

        osc2.connect(gain2);
        gain2.connect(ctx.destination);

        osc2.start(now + 0.16);
        osc2.stop(now + 0.55);

        // Chờ nốt nhạc thứ 2 kết thúc
        await new Promise((resolve) => setTimeout(resolve, 520));
    } catch {
        // Trình duyệt có thể chặn autoplay nếu chưa có user gesture
    }
}

/**
 * Phát âm thanh cảnh báo ngắn (Warning Alert Beep)
 */
export async function playWarningBeep(volume = 0.5): Promise<void> {
    const ctx = getAudioContext();
    if (!ctx) return;

    try {
        if (!(await ensureContextRunning(ctx))) {
            return;
        }

        const now = ctx.currentTime;
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();

        osc.type = "sawtooth";
        osc.frequency.setValueAtTime(440, now);
        osc.frequency.setValueAtTime(330, now + 0.12);

        gain.gain.setValueAtTime(volume * 0.6, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.28);

        osc.connect(gain);
        gain.connect(ctx.destination);

        osc.start(now);
        osc.stop(now + 0.28);

        await new Promise((resolve) => setTimeout(resolve, 300));
    } catch {
        // Fallback im lặng nếu autoplay bị chặn
    }
}
