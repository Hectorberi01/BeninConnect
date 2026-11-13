import { compareVersions } from '@/core/version';

describe('compareVersions', () => {
  it('compare les versions numériquement', () => {
    expect(compareVersions('1.1.1', '1.1.0')).toBeGreaterThan(0);
    expect(compareVersions('1.1.0', '1.1.1')).toBeLessThan(0);
    expect(compareVersions('1.10.0', '1.9.9')).toBeGreaterThan(0);
    expect(compareVersions('1.2', '1.2.0')).toBe(0);
  });
});
