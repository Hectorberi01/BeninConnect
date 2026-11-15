import { capitalizeFirst, countryCode, matchCity, placeCoords, suggestCities } from '@/data/cities';

describe('villes et pays saisis', () => {
  it('met la première lettre en majuscule', () => {
    expect(capitalizeFirst('cotonou')).toBe('Cotonou');
    expect(capitalizeFirst('élancourt')).toBe('Élancourt');
    expect(capitalizeFirst('aix-en-provence')).toBe('Aix-en-provence');
    expect(capitalizeFirst('')).toBe('');
  });

  it('reconnaît un pays par son nom, une variante ou son code', () => {
    expect(countryCode('benin')).toBe('BJ');
    expect(countryCode('Cote d’Ivoire')).toBe('CI');
    expect(countryCode('rdc')).toBe('CD');
    expect(countryCode('fr')).toBe('FR');
    expect(countryCode('Atlantide')).toBeNull();
  });

  it('retrouve une ville sans tenir compte des accents ni des tirets', () => {
    expect(matchCity('porto novo')?.name).toBe('Porto-Novo');
    expect(matchCity('Geneve', 'CH')?.name).toBe('Genève');
    expect(suggestCities('cot').map((c) => c.name)).toContain('Cotonou');
  });

  it("place une ville hors liste sur la ville principale du pays", () => {
    expect(placeCoords('Lyon', 'FR')).toEqual({ lat: 45.764, lng: 4.8357 });
    expect(placeCoords('Pobè', 'BJ')).toEqual({ lat: 6.3703, lng: 2.3912 });
    expect(placeCoords('Pobè', null)).toBeNull();
  });
});
