import { formatDateInput, notificationHref, parseLocalDateTime, validateEvent, validatePost } from '@/features/rules';

describe('règles des écrans', () => {
  it('publication', () => {
    expect(validatePost('  ', 'Public', null)).toBe('Écrivez quelque chose avant de publier.');
    expect(validatePost('ab', 'Public', null)).toMatch(/au moins 3/);
    expect(validatePost('Bonjour', 'CommunityMembers', null)).toMatch(/Choisissez une communauté/);
    expect(validatePost('Bonjour', 'CommunityMembers', 'c1')).toBeNull();
    expect(validatePost('x'.repeat(2001), 'Public', null)).toMatch(/2000/);
  });

  it('dates saisies', () => {
    const d = parseLocalDateTime('13/11/2026', '18:30')!;
    expect([d.getFullYear(), d.getMonth(), d.getDate(), d.getHours(), d.getMinutes()]).toEqual([2026, 10, 13, 18, 30]);
    expect(parseLocalDateTime('31/02/2026', '18:00')).toBeNull();
    expect(parseLocalDateTime('demain', '18:00')).toBeNull();
    expect(formatDateInput(new Date(2026, 0, 5))).toBe('05/01/2026');
  });

  it('événement', () => {
    const now = new Date(2026, 10, 6);
    const ok = {
      title: 'Afterwork diaspora', description: 'Une soirée pour se rencontrer et échanger.', category: 'Networking',
      city: 'Lyon', address: '1 rue X', date: '13/11/2026', time: '18:00', duration: '2', capacity: '',
    };
    expect(validateEvent(ok, now)).toEqual({});
    const bad = validateEvent({ ...ok, date: '01/11/2026', duration: '0', capacity: '1' }, now);
    expect(Object.keys(bad).sort()).toEqual(['capacity', 'date', 'duration']);
  });

  it('cible des notifications', () => {
    const n = { id: '1', userId: 'u', category: 'Social' as const, title: '', body: '', createdAt: '', isRead: false };
    expect(notificationHref({ ...n, target: 'Post', targetId: 'p1' })).toBe('/post/p1');
    expect(notificationHref({ ...n, target: 'Event', targetId: 'e1' })).toBe('/events/e1');
    expect(notificationHref({ ...n, target: 'None', targetId: null })).toBeNull();
  });
});
