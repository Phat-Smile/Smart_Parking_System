import { create } from "zustand";

interface OwnerSearchState {
    query: string;
    setQuery: (q: string) => void;
    clearQuery: () => void;
}

export const useOwnerSearchStore = create<OwnerSearchState>((set) => ({
    query: "",
    setQuery: (query: string) => set({ query }),
    clearQuery: () => set({ query: "" }),
}));
