import { QueryClient } from '@tanstack/react-query';

// Pas de nouvelle tentative automatique : les erreurs (réseau, simulées) s'affichent avec « Réessayer ».
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: 30_000, refetchOnWindowFocus: false },
    mutations: { retry: false },
  },
});
