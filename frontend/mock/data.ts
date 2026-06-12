/**
 * Mock data for frontend-only development.
 * Used by the Vite mock plugin when VITE_MOCK=true.
 *
 * Every constant is typed against the NSwag-generated contract (client.ts),
 * so `npm run typecheck` doubles as a contract test: when backend DTOs change
 * and the client is regenerated, stale mock shapes fail to compile.
 */

import type { IApiKeyListItem, IAuthResponse, IUserInfoResponse } from '../src/api/client'

/** JSON wire shape of a generated DTO: Dates serialize to ISO strings, methods don't exist. */
type Wire<T> = T extends Date
  ? string
  : T extends (infer U)[]
    ? Wire<U>[]
    : T extends object
      ? { [K in keyof T as T[K] extends Function ? never : K]: Wire<T[K]> }
      : T

export const MOCK_USER_ID = '01961234-abcd-7000-8000-000000000001'

export const MOCK_TOKENS: Wire<IAuthResponse> = {
  userId: MOCK_USER_ID,
  username: 'demouser',
  accessToken: 'mock-access-token-xxxxxxxx',
  refreshToken: 'mock-refresh-token-yyyyyyyy',
  expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
}

export const MOCK_USER_INFO: Wire<IUserInfoResponse> = {
  userId: MOCK_USER_ID,
  displayName: 'Demo User',
  createdAt: '2025-01-10T08:00:00Z',
  hasPassword: true,
  emails: [
    { email: 'demo@example.com', isPrimary: true, isVerified: true },
    { email: 'alt@example.com', isPrimary: false, isVerified: false },
  ],
  providers: [
    { provider: 'Github', linkedAt: '2025-01-15T10:00:00Z' },
  ],
}

export const MOCK_API_KEYS: Wire<IApiKeyListItem>[] = [
  {
    id: '01961234-abcd-7000-8000-000000000010',
    name: 'Heartbeat Agent',
    prefix: 'ak_AbCdEfGh_***',
    createdAt: '2025-03-01T08:00:00Z',
    lastUsedAt: '2025-06-01T12:30:00Z',
    isRevoked: false,
  },
  {
    id: '01961234-abcd-7000-8000-000000000011',
    name: 'Old Key',
    prefix: 'ak_XyZwVuTs_***',
    createdAt: '2024-12-01T08:00:00Z',
    lastUsedAt: undefined,
    isRevoked: true,
  },
]
