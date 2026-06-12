/**
 * Vite plugin: mock API server for frontend-only development.
 *
 * Activated when environment variable VITE_MOCK=true.
 * Intercepts /api/v1/auth/* requests and returns fake JSON responses
 * so the frontend can be developed without running the backend.
 *
 * Usage:
 *   VITE_MOCK=true npx vite   (or npm run dev:mock)
 */

import type { Plugin, Connect } from 'vite'
import { MOCK_TOKENS, MOCK_USER_INFO, MOCK_API_KEYS } from './data'

type Handler = (
  req: Connect.IncomingMessage & { body?: any },
  res: import('http').ServerResponse,
  url: URL
) => void

function json(res: import('http').ServerResponse, data: unknown, status = 200) {
  res.writeHead(status, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify(data))
}

function noContent(res: import('http').ServerResponse) {
  res.writeHead(204)
  res.end()
}

/** Collect a JSON request body (if any). */
function parseBody(req: Connect.IncomingMessage): Promise<any> {
  return new Promise((resolve) => {
    const chunks: Buffer[] = []
    req.on('data', (c: Buffer) => chunks.push(c))
    req.on('end', () => {
      if (chunks.length === 0) { resolve({}); return }
      try { resolve(JSON.parse(Buffer.concat(chunks).toString())) }
      catch { resolve({}) }
    })
  })
}

// ─── Route table ───────────────────────────────────────────────────────────

const routes: Array<{ method: string; pattern: RegExp; handler: Handler }> = []

function route(method: string, path: string, handler: Handler) {
  // Convert /api/v1/auth/:param style to regex
  const pattern = new RegExp('^' + path.replace(/:[^/]+/g, '[^/]+') + '$')
  routes.push({ method: method.toUpperCase(), pattern, handler })
}

// ─── Auth endpoints ────────────────────────────────────────────────────────

route('POST', '/api/v1/auth/register', (_req, res) => {
  json(res, MOCK_TOKENS)
})

route('POST', '/api/v1/auth/login', (_req, res) => {
  json(res, MOCK_TOKENS)
})

route('POST', '/api/v1/auth/refresh', (_req, res) => {
  json(res, {
    ...MOCK_TOKENS,
    accessToken: 'mock-refreshed-token-' + Date.now(),
    expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
  })
})

route('POST', '/api/v1/auth/exchange', (_req, res) => {
  json(res, MOCK_TOKENS)
})

route('POST', '/api/v1/auth/logout', (_req, res) => {
  noContent(res)
})

// ─── Password reset / change ───────────────────────────────────────────────

// Anti-enumeration contract: always 204, regardless of the email.
route('POST', '/api/v1/auth/forgot-password', (_req, res) => {
  noContent(res)
})

route('POST', '/api/v1/auth/reset-password', (req, res) => {
  // Use token "expired" to exercise the error path in mock mode.
  if (req.body?.token === 'expired') {
    json(res, { message: 'Invalid or expired reset token.' }, 400)
    return
  }
  noContent(res)
})

route('POST', '/api/v1/auth/change-password', (req, res) => {
  // Use current password "wrong" to exercise the error path in mock mode.
  if (req.body?.currentPassword === 'wrong') {
    json(res, { message: 'Invalid credentials.' }, 401)
    return
  }
  noContent(res)
})

// ─── User / profile ────────────────────────────────────────────────────────

route('GET', '/api/v1/auth/me', (_req, res) => {
  json(res, MOCK_USER_INFO)
})

route('POST', '/api/v1/auth/add-password', (_req, res) => {
  json(res, null)
})

route('DELETE', '/api/v1/auth/unlink-provider', (_req, res) => {
  json(res, null)
})

// ─── Email ─────────────────────────────────────────────────────────────────

route('POST', '/api/v1/auth/email/send-code', (_req, res) => {
  json(res, null)
})

route('POST', '/api/v1/auth/email/verify', (_req, res) => {
  json(res, null)
})

route('POST', '/api/v1/auth/email', (_req, res) => {
  json(res, null)
})

route('DELETE', '/api/v1/auth/email/:email', (_req, res) => {
  json(res, null)
})

route('PUT', '/api/v1/auth/email/:email/primary', (_req, res) => {
  json(res, null)
})

// ─── API Keys ──────────────────────────────────────────────────────────────

route('GET', '/api/v1/apikeys', (_req, res) => {
  json(res, MOCK_API_KEYS)
})

route('POST', '/api/v1/apikeys', (_req, res) => {
  json(res, {
    id: crypto.randomUUID(),
    name: 'New Key',
    key: 'ak_MockPfx1_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk',
    createdAt: new Date().toISOString(),
  }, 201)
})

route('DELETE', '/api/v1/apikeys/:id', (_req, res) => {
  noContent(res)
})

route('POST', '/api/v1/apikeys/exchange', (_req, res) => {
  json(res, {
    accessToken: 'mock-apikey-token-' + Date.now(),
    expiresIn: 900,
  })
})

// ─── OAuth redirects (simulate redirect to callback with code) ─────────────

route('GET', '/api/v1/auth/github/login', (_req, res) => {
  // In real flow this redirects to GitHub. In mock, redirect back with a fake code.
  res.writeHead(302, { Location: '/callback?code=mock-github-code' })
  res.end()
})

route('GET', '/api/v1/auth/google/login', (_req, res) => {
  res.writeHead(302, { Location: '/callback?code=mock-google-code' })
  res.end()
})

// ─── Plugin export ─────────────────────────────────────────────────────────

export default function mockApiPlugin(): Plugin {
  return {
    name: 'mock-api',
    configureServer(server) {
      server.middlewares.use(async (req, res, next) => {
        const url = new URL(req.url ?? '/', 'http://localhost')
        const method = (req.method ?? 'GET').toUpperCase()

        const matched = routes.find(
          (r) => r.method === method && r.pattern.test(url.pathname)
        )

        if (!matched) {
          next()
          return
        }

        // Parse body for POST/PUT/DELETE
        if (method !== 'GET') {
          ;(req as any).body = await parseBody(req)
        }

        // Small delay to simulate network
        await new Promise((r) => setTimeout(r, 150))

        matched.handler(req as any, res, url)
      })
    },
  }
}
