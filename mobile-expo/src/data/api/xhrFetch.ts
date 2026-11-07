/**
 * fetch minimal basé sur XMLHttpRequest, pour les envois de fichiers.
 * Le fetch d'Expo (expo/fetch) refuse les fichiers { uri, name, type } du FormData React Native ;
 * XMLHttpRequest les lit nativement.
 */
export const xhrFetch = ((url: string, init: RequestInit = {}) =>
  new Promise<Response>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(init.method ?? 'GET', url);
    for (const [key, value] of Object.entries((init.headers ?? {}) as Record<string, string>)) xhr.setRequestHeader(key, value);
    xhr.onload = () => {
      const text = xhr.responseText ?? '';
      resolve({
        ok: xhr.status >= 200 && xhr.status < 300,
        status: xhr.status,
        json: async () => JSON.parse(text),
        text: async () => text,
      } as Response);
    };
    xhr.onerror = () => reject(new TypeError('Network request failed'));
    xhr.ontimeout = xhr.onerror;
    xhr.onabort = () => reject(new DOMException('Aborted', 'AbortError'));
    init.signal?.addEventListener('abort', () => xhr.abort());
    xhr.send(init.body as XMLHttpRequestBodyInit);
  })) as unknown as typeof fetch;
