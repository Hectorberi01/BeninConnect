import { useMemo, useRef } from 'react';
import { Animated, PanResponder } from 'react-native';

const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v));

export type ZoomOptions = {
  max?: number;
  /** Déplacement maximal (de part et d'autre du centre) pour un zoom donné. */
  bounds: (scale: number) => { x: number; y: number };
  /** Zoom actif ou non (bloque le défilement entre les images). */
  onZoomChange?: (zoomed: boolean) => void;
  /** Glisser vers le bas sans zoom : fermer. */
  onSwipeDown?: () => void;
};

/** Pincer pour zoomer, glisser pour déplacer, double appui pour zoomer ou revenir. Sans module natif. */
export function useZoomPan(options: ZoomOptions) {
  const opts = useRef(options);
  opts.current = options;
  const scale = useRef(new Animated.Value(1)).current;
  const x = useRef(new Animated.Value(0)).current;
  const y = useRef(new Animated.Value(0)).current;
  const st = useRef({ s: 1, x: 0, y: 0, mode: null as null | 'pan' | 'pinch', d0: 1, s0: 1, x0: 0, y0: 0, px: 0, py: 0, moved: false, lastTap: 0 }).current;

  return useMemo(() => {
    const fit = (s: number, nx: number, ny: number) => {
      const b = opts.current.bounds(s);
      return [clamp(nx, -b.x, b.x), clamp(ny, -b.y, b.y)] as const;
    };
    const set = (s: number, nx: number, ny: number, animated = false) => {
      st.s = s; st.x = nx; st.y = ny;
      if (!animated) {
        scale.setValue(s); x.setValue(nx); y.setValue(ny);
        return;
      }
      Animated.parallel([scale, x, y].map((v, i) => Animated.spring(v, { toValue: [s, nx, ny][i]!, useNativeDriver: true, bounciness: 0 }))).start();
    };
    const zoomed = () => opts.current.onZoomChange?.(st.s > 1.01);

    const responder = PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponder: () => true,
      onPanResponderTerminationRequest: () => st.s <= 1.01 && st.mode !== 'pinch',
      onPanResponderGrant: () => { st.moved = false; },
      onPanResponderMove: (e) => {
        const touches = e.nativeEvent.touches;
        const max = opts.current.max ?? 4;
        if (touches.length >= 2) {
          const d = Math.hypot(touches[0]!.pageX - touches[1]!.pageX, touches[0]!.pageY - touches[1]!.pageY);
          if (st.mode !== 'pinch') Object.assign(st, { mode: 'pinch', d0: d || 1, s0: st.s, x0: st.x, y0: st.y });
          const s = clamp((st.s0 * d) / st.d0, 0.8, max);
          const [nx, ny] = fit(s, (st.x0 * s) / st.s0, (st.y0 * s) / st.s0);
          st.moved = true;
          set(s, nx, ny);
          return;
        }
        const t = touches[0];
        if (!t) return;
        if (st.mode !== 'pan') Object.assign(st, { mode: 'pan', x0: st.x, y0: st.y, px: t.pageX, py: t.pageY });
        const dx = t.pageX - st.px;
        const dy = t.pageY - st.py;
        if (Math.abs(dx) > 4 || Math.abs(dy) > 4) st.moved = true;
        if (st.s > 1.01) {
          const [nx, ny] = fit(st.s, st.x0 + dx, st.y0 + dy);
          set(st.s, nx, ny);
        } else if (opts.current.onSwipeDown && dy > 0 && dy > Math.abs(dx)) {
          st.y = dy;
          y.setValue(dy);
        } else if (!opts.current.onSwipeDown) {
          const [nx, ny] = fit(st.s, st.x0 + dx, st.y0 + dy);
          set(st.s, nx, ny);
        }
      },
      onPanResponderRelease: () => {
        const wasPinch = st.mode === 'pinch';
        st.mode = null;
        if (!st.moved && !wasPinch) {
          const now = Date.now();
          if (now - st.lastTap < 300) {
            st.lastTap = 0;
            if (st.s > 1.01) set(1, ...fit(1, 0, 0), true);
            else set(2.5, ...fit(2.5, 0, 0), true);
            zoomed();
            return;
          }
          st.lastTap = now;
        }
        if (st.s <= 1.01 && opts.current.onSwipeDown) {
          if (st.y > 120) { opts.current.onSwipeDown(); return; }
          set(1, 0, 0, true);
        } else if (st.s < 1) {
          set(1, ...fit(1, 0, 0), true);
        }
        zoomed();
      },
      onPanResponderTerminate: () => {
        st.mode = null;
        if (st.s <= 1.01) set(1, ...fit(1, 0, 0), true);
      },
    });

    return {
      handlers: responder.panHandlers,
      style: { transform: [{ translateX: x }, { translateY: y }, { scale }] },
      /** Zoom et décalage actuels. */
      current: () => ({ scale: st.s, x: st.x, y: st.y }),
      reset: () => { set(1, 0, 0); zoomed(); },
    };
  }, [scale, x, y, st]);
}
