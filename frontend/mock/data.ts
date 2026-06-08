/**
 * Mock data for frontend-only development.
 * Used by the Vite mock plugin when VITE_MOCK=true.
 */

export const MOCK_USER_ID = '01961234-abcd-7000-8000-000000000001'

export const MOCK_TOKENS = {
  accessToken: 'mock-access-token-xxxxxxxx',
  refreshToken: 'mock-refresh-token-yyyyyyyy',
  expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
  userId: MOCK_USER_ID,
}

export const MOCK_USER_INFO = {
  id: MOCK_USER_ID,
  username: 'demouser',
  displayName: 'Demo User',
  hasPassword: true,
  emails: [
    { email: 'demo@example.com', isPrimary: true, isVerified: true },
    { email: 'alt@example.com', isPrimary: false, isVerified: false },
  ],
  providers: [
    { provider: 'Github', providerUserId: '12345', createdAt: '2025-01-15T10:00:00Z' },
  ],
}

export const MOCK_API_KEYS = [
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
    lastUsedAt: null,
    isRevoked: true,
  },
]
