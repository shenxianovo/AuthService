import { createI18n } from 'vue-i18n'
import { AuthError, ErrorResponse } from '@/api/client'
import en from './en'
import type { MessageSchema } from './en'
import zhCN from './zh-CN'

export type Locale = 'en' | 'zh-CN'

const STORAGE_KEY = 'locale'

function detectLocale(): Locale {
  const stored = localStorage.getItem(STORAGE_KEY)
  if (stored === 'en' || stored === 'zh-CN') return stored
  return navigator.language?.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en'
}

export const i18n = createI18n<[MessageSchema], Locale, false>({
  legacy: false,
  locale: detectLocale(),
  fallbackLocale: 'en',
  messages: {
    en,
    'zh-CN': zhCN,
  },
})

export function setLocale(locale: Locale) {
  i18n.global.locale.value = locale
  localStorage.setItem(STORAGE_KEY, locale)
  document.documentElement.lang = locale
}

document.documentElement.lang = i18n.global.locale.value

/**
 * Localize an API error for display. Errors thrown by the generated client are
 * ErrorResponse instances carrying a stable AuthError code (ADR-021); codes
 * with a translation entry render localized, anything else (admin-only codes,
 * network failures, non-API errors) falls back to the English message from the
 * backend, then to the caller's fallback text.
 */
export function translateApiError(e: unknown, fallback: string): string {
  if (e instanceof ErrorResponse && e.code) {
    const key = `errors.${e.code}`
    // Admin-only codes are deliberately untranslated — fall through to message.
    if (e.code !== AuthError.None && i18n.global.te(key)) {
      return i18n.global.t(key)
    }
  }
  const message = e instanceof ErrorResponse ? e.message : e instanceof Error ? e.message : null
  return message || fallback
}
