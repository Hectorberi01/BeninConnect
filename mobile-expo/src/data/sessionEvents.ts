/** Session refusée par le serveur (401 alors qu'un jeton était envoyé). */
let handler: (() => void) | null = null;

export function onSessionExpired(fn: (() => void) | null) {
  handler = fn;
}

export function notifySessionExpired() {
  handler?.();
}
