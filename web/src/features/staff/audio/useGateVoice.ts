"use client";

import { useEffect, useState, useTransition } from "react";
import {
    gateVoiceService,
    type GateVoiceSettings,
    type VoiceLogItem,
} from "./GateVoiceService";

export function useGateVoice() {
    const [, startTransition] = useTransition();

    const [settings, setSettings] = useState<GateVoiceSettings>(() =>
        gateVoiceService.getSettings(),
    );
    const [isSpeaking, setIsSpeaking] = useState<boolean>(() =>
        gateVoiceService.isSpeaking(),
    );
    const [availableVoices, setAvailableVoices] = useState<
        SpeechSynthesisVoice[]
    >(() => gateVoiceService.getAvailableVoices());
    const [selectedVoice, setSelectedVoice] =
        useState<SpeechSynthesisVoice | null>(() =>
            gateVoiceService.getSelectedVoice(),
        );
    const [logs, setLogs] = useState<VoiceLogItem[]>(() =>
        gateVoiceService.getLogs(),
    );

    useEffect(() => {
        const unsubscribe = gateVoiceService.subscribe(() => {
            startTransition(() => {
                setSettings(gateVoiceService.getSettings());
                setIsSpeaking(gateVoiceService.isSpeaking());
                setAvailableVoices(gateVoiceService.getAvailableVoices());
                setSelectedVoice(gateVoiceService.getSelectedVoice());
                setLogs(gateVoiceService.getLogs());
            });
        });

        return () => {
            unsubscribe();
        };
    }, []);

    return {
        // States
        isEnabled: settings.enabled,
        isSpeaking,
        volume: settings.volume,
        rate: settings.rate,
        pitch: settings.pitch,
        playChime: settings.playChime,
        availableVoices,
        selectedVoice,
        logs,

        // Actions
        toggleEnabled: () => gateVoiceService.toggleEnabled(),
        setEnabled: (enabled: boolean) => gateVoiceService.setEnabled(enabled),
        setVolume: (volume: number) => gateVoiceService.setVolume(volume),
        setRate: (rate: number) => gateVoiceService.setRate(rate),
        setVoiceURI: (uri: string) => gateVoiceService.setVoiceURI(uri),
        setPlayChime: (play: boolean) => gateVoiceService.setPlayChime(play),
        stop: () => gateVoiceService.stop(),
        clearQueue: () => gateVoiceService.clearQueue(),
        getQueueLength: () => gateVoiceService.getQueueLength(),

        // Triggers
        announceCheckIn: (params: {
            plateNumber: string;
            slotCode?: string;
            lotName?: string;
        }) => gateVoiceService.announceCheckIn(params),

        announceCheckOut: (params?: {
            plateNumber?: string;
            lotName?: string;
            amountPaid?: number;
        }) => gateVoiceService.announceCheckOut(params),

        announceWarning: (reason?: string) =>
            gateVoiceService.announceWarning(reason),

        announceUnpaidExit: () => gateVoiceService.announceUnpaidExit(),

        testVoice: (
            sampleType: "checkin" | "checkout" | "warning" = "checkin",
        ) => gateVoiceService.testVoice(sampleType),
    };
}
