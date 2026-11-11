import { ApiDataSource } from '@/data/api/apiDataSource';

type Call = { url: string; init: RequestInit };

function fakeFetch(status: number, body?: unknown) {
  const calls: Call[] = [];
  const impl = (async (url: string, init: RequestInit) => {
    calls.push({ url, init });
    return {
      ok: status >= 200 && status < 300,
      status,
      json: async () => {
        if (body === undefined) throw new Error('vide');
        return body;
      },
    } as Response;
  }) as unknown as typeof fetch;
  return { impl, calls };
}

describe('ApiDataSource', () => {
  it('ajoute le jeton et construit les routes', async () => {
    const f = fakeFetch(200, { items: [], totalCount: 0, skip: 10 });
    const api = new ApiDataSource('http://localhost:5080/', f.impl);
    api.setToken('abc');
    await api.getFeed(10, 5);
    expect(f.calls[0].url).toBe('http://localhost:5080/api/feed?skip=10&take=5');
    expect((f.calls[0].init.headers as Record<string, string>).Authorization).toBe('Bearer abc');
  });

  it('message métier de ProblemDetails', async () => {
    const api = new ApiDataSource('http://x', fakeFetch(400, { title: 'Bad', detail: 'Email ou mot de passe incorrect.' }).impl);
    await expect(api.login('a@b.co', 'x')).rejects.toThrow('Email ou mot de passe incorrect.');
  });

  it('401 et 500', async () => {
    await expect(new ApiDataSource('http://x', fakeFetch(401).impl).getFeed(0, 10)).rejects.toThrow(/session a expiré/);
    await expect(new ApiDataSource('http://x', fakeFetch(503).impl).getFeed(0, 10)).rejects.toThrow(/serveur a rencontré/);
  });

  it('serveur injoignable', async () => {
    const failing = (async () => { throw new TypeError('Network request failed'); }) as unknown as typeof fetch;
    await expect(new ApiDataSource('http://x', failing).getCommunities()).rejects.toThrow(/Impossible de joindre le serveur/);
  });

  it('corps JSON pour les écritures', async () => {
    const f = fakeFetch(200, { id: 'p1' });
    await new ApiDataSource('http://x', f.impl).createPost({ content: 'Salut', communityId: null, imagePath: null, visibility: 'Public' });
    expect(f.calls[0].init.method).toBe('POST');
    expect(JSON.parse(f.calls[0].init.body as string)).toMatchObject({ content: 'Salut', visibility: 'Public' });
  });
});

describe('Compte de revue Apple / Google', () => {
  it('421 avec redirection : erreur dédiée portant l\'adresse de revue', async () => {
    const api = new ApiDataSource('https://api.monbenin.com', fakeFetch(421, {
      title: 'Compte de démonstration', detail: 'Ce compte de démonstration nécessite la dernière version de l\'application.',
      redirect: 'https://review-api.monbenin.com/',
    }).impl);
    await expect(api.login('appreview@monbenin.com', 'x')).rejects.toMatchObject({
      name: 'ReviewRedirectError', url: 'https://review-api.monbenin.com', status: 421,
    });
  });

  it('redirection non https refusée (message seulement)', async () => {
    const api = new ApiDataSource('https://api.monbenin.com', fakeFetch(421, { detail: 'Mettez à jour.', redirect: 'ftp://ailleurs' }).impl);
    await expect(api.login('a@b.co', 'x')).rejects.toMatchObject({ name: 'AppError', message: 'Mettez à jour.' });
  });

  it('API de revue : pas de compte de démo public, code fixe', () => {
    const api = new ApiDataSource('https://review-api.monbenin.com', fakeFetch(200).impl, undefined, undefined, { review: true });
    expect(api.isReview).toBe(true);
    expect(api.hasDemoAccount).toBe(false);
    expect(api.demoVerificationCode).toBe('123456');
  });
});
