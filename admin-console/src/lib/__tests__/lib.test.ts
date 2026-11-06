import { describe, expect, it } from 'vitest'
import { qs } from '../api'
import { fmtBytes, fmtRelative, initials } from '../format'
import { decisionsFor, has } from '../permissions'

describe('permissions', () => {
  it('vérifie un droit', () => {
    expect(has(['DashboardView', 'ExportsDownload'], 'ExportsDownload')).toBe(true)
    expect(has(['DashboardView'], 'AdminsManage')).toBe(false)
    expect(has(undefined, 'DashboardView')).toBe(false)
  })

  it('décisions selon la cible (mêmes règles que l’API)', () => {
    expect(decisionsFor('Post', false).map((d) => d.value)).toEqual(['dismiss', 'hide', 'delete', 'suspend'])
    expect(decisionsFor('Comment', false).map((d) => d.value)).toEqual(['dismiss', 'delete', 'suspend'])
    expect(decisionsFor('User', false).map((d) => d.value)).toEqual(['dismiss', 'suspend'])
    expect(decisionsFor('Community', false).map((d) => d.value)).toEqual(['dismiss'])
    expect(decisionsFor('Community', true).map((d) => d.value)).toEqual(['dismiss', 'delete'])
  })
})

describe('format', () => {
  it('tailles, dates relatives, initiales', () => {
    expect(fmtBytes(512)).toBe('512 o')
    expect(fmtBytes(1536)).toBe('1,5 Ko')
    const now = new Date('2026-11-06T12:00:00Z')
    expect(fmtRelative('2026-11-06T11:55:00Z', now)).toBe('il y a 5 min')
    expect(fmtRelative('2026-11-04T12:00:00Z', now)).toBe('il y a 2 j')
    expect(initials('sèna', 'houngbo')).toBe('SH')
  })

  it('paramètres de requête sans valeurs vides', () => {
    expect(qs({ search: 'paris', status: '', skip: 0, take: 25, x: undefined })).toBe('?search=paris&skip=0&take=25')
    expect(qs({})).toBe('')
  })
})
