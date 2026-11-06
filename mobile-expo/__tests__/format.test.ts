import * as fmt from '@/core/format';
import * as v from '@/core/validation';

const now = new Date(2026, 10, 6, 12, 0);
const ago = (ms: number) => new Date(now.getTime() - ms).toISOString();

describe('format', () => {
  it('relative', () => {
    expect(fmt.relative(ago(10_000), now)).toBe("À l'instant");
    expect(fmt.relative(ago(5 * 60_000), now)).toBe('Il y a 5 min');
    expect(fmt.relative(ago(3 * 3_600_000), now)).toBe('Il y a 3 h');
    expect(fmt.relative(ago(30 * 3_600_000), now)).toBe('Hier');
    expect(fmt.relative(ago(3 * 86_400_000), now)).toBe('Il y a 3 j');
    expect(fmt.relative(new Date(2026, 0, 15).toISOString(), now)).toBe('15 janv.');
    expect(fmt.relative(new Date(2025, 0, 15).toISOString(), now)).toBe('15 janv. 2025');
  });

  it('chat et dayLabel', () => {
    expect(fmt.chat(new Date(2026, 10, 6, 9, 5).toISOString(), now)).toBe('09:05');
    expect(fmt.chat(new Date(2026, 10, 5, 9, 5).toISOString(), now)).toBe('Hier');
    expect(fmt.dayLabel(new Date(2026, 10, 6, 8).toISOString(), now)).toBe("Aujourd'hui");
    expect(fmt.longDate(new Date(2026, 10, 6).toISOString())).toBe('Vendredi 6 novembre 2026');
  });

  it('plural', () => {
    expect(fmt.plural(1, 'membre')).toBe('1 membre');
    expect(fmt.plural(3, 'membre')).toBe('3 membres');
  });
});

describe('validation', () => {
  it('email et mot de passe', () => {
    expect(v.email('')).toBe("L'adresse email est obligatoire.");
    expect(v.email('abc')).toBe("L'adresse email n'est pas valide.");
    expect(v.email('a@b.co')).toBeNull();
    expect(v.password('court1')).toMatch(/au moins 8/);
    expect(v.password('abcdefgh')).toMatch(/chiffre/);
    expect(v.password('Demo123!')).toBeNull();
  });

  it('length et collect', () => {
    expect(v.length('a', 'Le nom', 2, 40)).toBe('Le nom doit contenir au moins 2 caractères.');
    expect(v.collect({ a: null, b: 'x' })).toEqual({ b: 'x' });
  });

  it('recherche insensible aux accents', () => {
    expect(v.matches('Montréal', 'montreal')).toBe(true);
    expect(v.matches('Cotonou', 'paris')).toBe(false);
  });
});
