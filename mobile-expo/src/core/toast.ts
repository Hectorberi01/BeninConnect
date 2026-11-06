import { create } from 'zustand';

export type ToastKind = 'info' | 'success' | 'error';

interface ToastStore {
  message: string | null;
  kind: ToastKind;
  show: (message: string, kind?: ToastKind) => void;
}

let timer: ReturnType<typeof setTimeout> | undefined;

export const useToast = create<ToastStore>((set) => ({
  message: null,
  kind: 'info',
  show: (message, kind = 'info') => {
    clearTimeout(timer);
    set({ message, kind });
    timer = setTimeout(() => set({ message: null }), 2600);
  },
}));

export const toast = (message: string, kind: ToastKind = 'info') => useToast.getState().show(message, kind);
