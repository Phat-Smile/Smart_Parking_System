/**
 * Dịch vụ Trợ lý Giọng nói Cổng bãi Thông minh (Gate Voice Assistant Service)
 * Hoạt động 100% offline trên trình duyệt thông qua Web Speech API (SpeechSynthesis)
 * và Web Audio API. Zero cloud API cost.
 */

import { playGateChime, playWarningBeep } from "./chimeGenerator";
import {
    buildCheckInSpeech,
    buildCheckOutSpeech,
    buildWarningSpeech,
    buildUnpaidExitSpeech,
    UNPAID_EXIT_WARNING_MESSAGE,
} from "./phoneticHelper";
import { pickVoice } from "./voiceSelection";

export interface VoiceLogItem {
    id: string;
    timestamp: Date;
    type: "checkin" | "checkout" | "warning" | "system";
    text: string;
}

export interface GateVoiceSettings {
    enabled: boolean;
    volume: number; // 0.0 - 1.0
    rate: number; // 0.8 - 1.5
    pitch: number; // 0.8 - 1.2
    voiceURI: string | null;
    playChime: boolean;
}

type VoiceListener = () => void;

class GateVoiceService {
    private static instance: GateVoiceService | null = null;

    private synth: SpeechSynthesis | null = null;
    private availableVoices: SpeechSynthesisVoice[] = [];
    private selectedVoice: SpeechSynthesisVoice | null = null;
    private isSpeakingState = false;
    private queue: Array<{
        text: string;
        type: VoiceLogItem["type"];
        urgent?: boolean;
        playChime?: boolean;
    }> = [];
    private processingQueue = false;
    private queueRunId = 0;

    // Giữ tham chiếu utterance tránh V8 Garbage Collection bug trên Chrome
    private activeUtterances: Set<SpeechSynthesisUtterance> = new Set();

    private listeners: Set<VoiceListener> = new Set();
    private logs: VoiceLogItem[] = [];

    private settings: GateVoiceSettings = {
        enabled: true,
        volume: 1.0,
        rate: 1.0,
        pitch: 1.0,
        voiceURI: null,
        playChime: true,
    };

    private constructor() {
        if (typeof window !== "undefined") {
            this.loadStoredSettings();
            this.initSpeechSynth();
        }
    }

    public static getInstance(): GateVoiceService {
        if (!GateVoiceService.instance) {
            GateVoiceService.instance = new GateVoiceService();
        }
        return GateVoiceService.instance;
    }

    private loadStoredSettings() {
        try {
            const raw = localStorage.getItem("parkwise_gate_voice_settings");
            if (raw) {
                const parsed = JSON.parse(raw);
                this.settings = { ...this.settings, ...parsed };
            }
        } catch {
            // fallback mặc định
        }
    }

    private saveSettings() {
        if (typeof window === "undefined") return;
        try {
            localStorage.setItem(
                "parkwise_gate_voice_settings",
                JSON.stringify(this.settings),
            );
        } catch {
            // bỏ qua
        }
    }

    private getSynth(): SpeechSynthesis | null {
        try {
            if (typeof window === "undefined" || !("speechSynthesis" in window)) {
                return null;
            }
            return (this.synth = window.speechSynthesis);
        } catch {
            return null;
        }
    }

    private initSpeechSynth() {
        try {
            const synth = this.getSynth();
            if (!synth) return;

            this.loadVoices();

            if (synth.onvoiceschanged !== undefined) {
                synth.onvoiceschanged = () => {
                    this.loadVoices();
                };
            }

            // Khắc phục bug Chrome treo speech synthesis sau khi chạy ngầm
            window.setInterval(() => {
                try {
                    const currentSynth = this.getSynth();
                    if (currentSynth && currentSynth.paused) {
                        currentSynth.resume();
                    }
                } catch {
                    // Bỏ qua nếu trình duyệt chặn resume khi chưa có thao tác người dùng
                }
            }, 10000);
        } catch {
            this.synth = null;
        }
    }

    private loadVoices() {
        const synth = this.getSynth();
        if (!synth) return;
        try {
            const all = synth.getVoices();
            if (!all || all.length === 0) return;

            const pick = pickVoice(all, this.settings.voiceURI);
            this.availableVoices = pick.options;
            this.selectedVoice = pick.voice;
            if (pick.voice) {
                this.settings.voiceURI = pick.voice.voiceURI;
            }
            this.notify();
        } catch {
            // Silently fallback if getVoices throws
        }
    }

    public subscribe(listener: VoiceListener): () => void {
        this.listeners.add(listener);
        return () => {
            this.listeners.delete(listener);
        };
    }

    private notify() {
        this.listeners.forEach((l) => l());
    }

    // ================== PUBLIC GETTERS ==================

    public getSettings(): GateVoiceSettings {
        return { ...this.settings };
    }

    public getAvailableVoices(): SpeechSynthesisVoice[] {
        return [...this.availableVoices];
    }

    public getSelectedVoice(): SpeechSynthesisVoice | null {
        return this.selectedVoice;
    }

    public isSpeaking(): boolean {
        return this.isSpeakingState;
    }

    public getQueueLength(): number {
        return this.queue.length;
    }

    public clearQueue() {
        this.queue = [];
        this.notify();
    }

    public isEnabled(): boolean {
        return this.settings.enabled;
    }

    public isSupported(): boolean {
        if (typeof window === "undefined") return false;
        try {
            return (
                "speechSynthesis" in window &&
                typeof SpeechSynthesisUtterance !== "undefined" &&
                !!window.speechSynthesis
            );
        } catch {
            return false;
        }
    }

    public getLogs(): VoiceLogItem[] {
        return [...this.logs];
    }

    // ================== SETTINGS ACTIONS ==================

    public setEnabled(enabled: boolean) {
        this.settings.enabled = enabled;
        if (!enabled) {
            this.stop();
        }
        this.saveSettings();
        this.notify();
    }

    public toggleEnabled(): boolean {
        this.setEnabled(!this.settings.enabled);
        return this.settings.enabled;
    }

    public setVolume(volume: number) {
        this.settings.volume = Math.max(0, Math.min(1, volume));
        this.saveSettings();
        this.notify();
    }

    public setRate(rate: number) {
        this.settings.rate = Math.max(0.5, Math.min(2, rate));
        this.saveSettings();
        this.notify();
    }

    public setVoiceURI(uri: string) {
        const found = this.availableVoices.find((v) => v.voiceURI === uri);
        if (found) {
            this.selectedVoice = found;
            this.settings.voiceURI = uri;
            this.saveSettings();
            this.notify();
        }
    }

    public setPlayChime(play: boolean) {
        this.settings.playChime = play;
        this.saveSettings();
        this.notify();
    }

    // ================== PHÁT ÂM VÀ HÀNG ĐỢI ==================

    public stop() {
        const synth = this.getSynth();
        if (synth) {
            try {
                synth.cancel();
            } catch {
                // Bỏ qua lỗi khi trình duyệt chặn audio
            }
        }
        this.queue = [];
        this.activeUtterances.clear();
        this.isSpeakingState = false;
        this.queueRunId++;
        this.processingQueue = false;
        this.notify();
    }

    public async speak(options: {
        text: string;
        type?: VoiceLogItem["type"];
        urgent?: boolean;
        playChime?: boolean;
    }) {
        if (!this.settings.enabled || !options.text.trim()) {
            return;
        }

        const type = options.type || "system";
        const urgent = !!options.urgent;
        const playChime =
            options.playChime !== undefined
                ? options.playChime
                : this.settings.playChime;

        // Ghi log
        this.logs.unshift({
            id: Math.random().toString(36).substring(2, 9),
            timestamp: new Date(),
            type,
            text: options.text,
        });
        if (this.logs.length > 20) {
            this.logs.pop();
        }

        if (urgent) {
            // Cảnh báo khẩn cấp: ngắt lời thoại hiện tại để phát ngay
            this.stop();
            this.queue.unshift({
                text: options.text,
                type,
                urgent: true,
                playChime,
            });
        } else {
            // Giữ hàng đợi gọn gàng, tránh dồn ứ lời thoại cũ khi sự kiện cổng dồn dập
            while (this.queue.length >= 5) {
                this.queue.shift();
            }
            this.queue.push({
                text: options.text,
                type,
                urgent: false,
                playChime,
            });
        }

        return this.processQueue();
    }

    private async processQueue() {
        if (this.processingQueue || this.queue.length === 0) return;
        this.processingQueue = true;
        const runId = ++this.queueRunId;

        try {
            while (this.queueRunId === runId && this.queue.length > 0) {
                const item = this.queue.shift();
                if (!item) break;

                try {
                    await this.speakUtterance(item);
                } catch {
                    // Bỏ qua lỗi phát âm hoặc autoplay not-allowed để không kẹt hàng đợi
                }
            }
        } finally {
            if (this.queueRunId === runId) {
                this.processingQueue = false;
            }
        }
    }

    private async speakUtterance(item: {
        text: string;
        type: VoiceLogItem["type"];
        urgent?: boolean;
        playChime?: boolean;
    }): Promise<void> {
        const synth = this.getSynth();
        if (
            !synth ||
            !this.settings.enabled ||
            typeof SpeechSynthesisUtterance === "undefined"
        ) {
            return;
        }

        // Phát chuông báo hiệu trước khi nói
        if (item.playChime) {
            try {
                if (item.type === "warning") {
                    await playWarningBeep(this.settings.volume * 0.7);
                } else {
                    await playGateChime(this.settings.volume * 0.5);
                }
            } catch {
                // Không chặn lời thoại nếu chuông Web Audio thất bại
            }
        }

        return new Promise((resolve) => {
            let utterance: SpeechSynthesisUtterance;
            try {
                utterance = new SpeechSynthesisUtterance(item.text);
            } catch {
                resolve();
                return;
            }

            try {
                if (this.selectedVoice) {
                    utterance.voice = this.selectedVoice;
                }
                utterance.lang = this.selectedVoice?.lang || "vi-VN";
                utterance.rate = this.settings.rate;
                utterance.pitch = this.settings.pitch;
                utterance.volume = this.settings.volume;
            } catch {
                // Bỏ qua nếu việc gán thuộc tính bị từ chối
            }

            this.activeUtterances.add(utterance);
            this.isSpeakingState = true;
            this.notify();

            let hasFinished = false;
            let timeoutId: ReturnType<typeof setTimeout> | null = null;

            const cleanup = () => {
                if (hasFinished) return;
                hasFinished = true;
                if (timeoutId !== null) {
                    clearTimeout(timeoutId);
                    timeoutId = null;
                }
                this.activeUtterances.delete(utterance);
                this.isSpeakingState = false;
                this.notify();
                resolve();
            };

            utterance.onend = () => {
                cleanup();
            };

            utterance.onerror = () => {
                cleanup();
            };

            // Timeout bảo vệ nếu trình duyệt bị drop sự kiện onend
            const maxWait = Math.max(4000, item.text.length * 150);
            timeoutId = setTimeout(() => {
                cleanup();
            }, maxWait);

            try {
                synth.speak(utterance);
            } catch {
                cleanup();
            }
        });
    }

    // ================== DOMAIN CONVENIENCE TRIGGERS ==================

    /**
     * Thông báo xe Check-in thành công
     */
    public announceCheckIn(params: {
        plateNumber: string;
        slotCode?: string;
        lotName?: string;
    }) {
        const text = buildCheckInSpeech(params);
        return this.speak({
            text,
            type: "checkin",
            playChime: true,
        });
    }

    /**
     * Thông báo xe Check-out thành công
     */
    public announceCheckOut(params?: {
        plateNumber?: string;
        lotName?: string;
        amountPaid?: number;
    }) {
        const text = buildCheckOutSpeech(params);
        return this.speak({
            text,
            type: "checkout",
            playChime: true,
        });
    }

    /**
     * Thông báo cảnh báo lỗi tại cổng
     */
    public announceWarning(reason?: string) {
        const text = buildWarningSpeech(reason);
        return this.speak({
            text,
            type: "warning",
            urgent: true,
            playChime: true,
        });
    }

    /**
     * Cảnh báo phiên đỗ chưa thanh toán ở cổng ra
     */
    public announceUnpaidExit() {
        const text = buildUnpaidExitSpeech();
        return this.speak({
            text,
            type: "warning",
            urgent: true,
            playChime: true,
        });
    }

    /**
     * Thử âm thanh kiểm tra loa
     */
    public testVoice(
        sampleType: "checkin" | "checkout" | "warning" = "checkin",
    ) {
        if (sampleType === "checkin") {
            return this.announceCheckIn({
                plateNumber: "51F-123.45",
                slotCode: "A-01",
                lotName: "Khu A",
            });
        }
        if (sampleType === "checkout") {
            return this.announceCheckOut({
                plateNumber: "51F-123.45",
                amountPaid: 30000,
            });
        }
        return this.announceWarning(
            "Vé không hợp lệ hoặc xe chưa thanh toán phí đỗ",
        );
    }
}

export const gateVoiceService = GateVoiceService.getInstance();
