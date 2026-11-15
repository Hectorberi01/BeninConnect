import { useCallback, useRef, useState } from 'react';

/**
 * Tirer pour actualiser : la roue ne tourne que pendant un geste du membre. Avec isRefetching,
 * chaque actualisation en arrière-plan affichait la roue et iOS pouvait la laisser bloquée en haut de la liste.
 */
export function usePullRefresh(refresh: () => unknown) {
  const latest = useRef(refresh);
  latest.current = refresh;
  const [refreshing, setRefreshing] = useState(false);
  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    try {
      await latest.current();
    } finally {
      setRefreshing(false);
    }
  }, []);
  return { refreshing, onRefresh };
}
