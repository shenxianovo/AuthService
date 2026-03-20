<template>
  <div class="page">
    <div class="auth-card">
      <!-- Header -->
      <div class="header">
        <h1>喵~</h1>
        <p v-if="externalRedirect" class="subtitle">
          Sign in to continue to <strong>{{ externalRedirectHost }}</strong>
        </p>
        <p v-else-if="!authenticated" class="subtitle">Sign in to your account</p>
      </div>

      <!-- Authenticated View -->
      <div v-if="authenticated" class="auth-content">
        <div class="profile-section">
          <div class="avatar">{{ userInitial }}</div>
          <div class="profile-info">
            <h2>{{ userInfo ? userInfo.displayName : 'User' }}</h2>
            <p class="user-id">{{ authData.userId }}</p>
          </div>
        </div>

        <div v-if="userInfo" class="details-section">
          <!-- Emails -->
          <div class="detail-group">
            <div class="detail-label">Emails</div>
            <div v-for="email in userInfo.emails" :key="email.email" class="detail-item">
              <span>{{ email.email }}</span>
              <div class="badge-group">
                <span v-if="email.isPrimary" class="badge badge-primary">Primary</span>
                <span v-if="email.isVerified" class="badge badge-success">Verified</span>
              </div>
            </div>
          </div>

          <!-- Providers -->
          <div class="detail-group">
            <div class="detail-label">Linked Accounts</div>
            <div v-if="userInfo.providers.length" class="provider-list">
              <div v-for="p in userInfo.providers" :key="p.provider" class="provider-chip">
                <span class="provider-icon" :class="p.provider.toLowerCase()">{{ providerIcon(p.provider) }}</span>
                {{ p.provider }}
              </div>
            </div>
            <p v-else class="muted">No accounts linked yet</p>
          </div>

          <!-- Password Status -->
          <div class="detail-group">
            <div class="detail-label">Password</div>
            <div class="detail-item">
              <span :class="userInfo.hasPassword ? 'status-set' : 'status-unset'">
                {{ userInfo.hasPassword ? 'Set' : 'Not set' }}
              </span>
            </div>
          </div>
        </div>

        <!-- Account Settings -->
        <div class="settings-section">
          <h3>Account Settings</h3>

          <div class="input-row">
            <input type="password" v-model="addPasswordForm.password" placeholder="New password" class="input" />
            <button class="btn btn-secondary" @click="handleAddPassword" :disabled="loading || !addPasswordForm.password">
              Set
            </button>
          </div>

          <div class="oauth-divider"><span>Link accounts</span></div>

          <div class="oauth-buttons">
            <button class="btn btn-github" @click="handleGithubBind" :disabled="loading">
              <svg class="icon" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
              GitHub
            </button>
            <button class="btn btn-google" @click="handleGoogleBind" :disabled="loading">
              <svg class="icon" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
              Google
            </button>
          </div>
        </div>

        <button @click="logout" class="btn btn-danger">Sign out</button>
      </div>

      <!-- Login / Register View -->
      <div v-else class="auth-content">
        <div class="tabs">
          <button :class="{ active: mode === 'login' }" @click="mode = 'login'">Sign in</button>
          <button :class="{ active: mode === 'register' }" @click="mode = 'register'">Sign up</button>
        </div>

        <!-- OAuth Buttons (shown for both modes) -->
        <div class="oauth-buttons">
          <button class="btn btn-github" @click="handleGithubLogin" :disabled="loading">
            <svg class="icon" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>
            Continue with GitHub
          </button>
          <button class="btn btn-google" @click="handleGoogleLogin" :disabled="loading">
            <svg class="icon" viewBox="0 0 24 24"><path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 01-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/><path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/><path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/><path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/></svg>
            Continue with Google
          </button>
        </div>

        <div class="oauth-divider"><span>or</span></div>

        <!-- Register Form -->
        <form v-if="mode === 'register'" @submit.prevent="handleRegister">
          <div class="form-group">
            <input type="text" v-model="registerForm.displayName" placeholder="Display name" class="input" required />
          </div>
          <div class="form-group">
            <input type="email" v-model="registerForm.email" placeholder="Email address" class="input" required />
          </div>
          <div class="form-group">
            <input type="password" v-model="registerForm.password" placeholder="Password" class="input" required />
          </div>
          <button type="submit" class="btn btn-primary" :disabled="loading">
            {{ loading ? 'Creating account...' : 'Create account' }}
          </button>
        </form>

        <!-- Login Form -->
        <form v-if="mode === 'login'" @submit.prevent="handleLogin">
          <div class="form-group">
            <input type="email" v-model="loginForm.email" placeholder="Email address" class="input" required />
          </div>
          <div class="form-group">
            <input type="password" v-model="loginForm.password" placeholder="Password" class="input" required />
          </div>
          <button type="submit" class="btn btn-primary" :disabled="loading">
            {{ loading ? 'Signing in...' : 'Sign in' }}
          </button>
        </form>

        <div v-if="error" class="message error">{{ error }}</div>
        <div v-if="successMsg" class="message success">{{ successMsg }}</div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  data() {
    return {
      mode: 'login', // 'login' or 'register'
      loading: false,
      error: null,
      successMsg: null,
      authenticated: false,
      authData: null,
      userInfo: null,
      registerForm: {
        displayName: '',
        email: '',
        password: ''
      },
      loginForm: {
        email: '',
        password: ''
      },
      addPasswordForm: {
        password: ''
      },
      externalRedirect: null, // external app redirect URL
      // Read the API URL from Vite environment variables (.env files)
      apiUrl: import.meta.env.VITE_API_URL || '/api/v1/auth'
    }
  },
  computed: {
    externalRedirectHost() {
      try {
        return new URL(this.externalRedirect).host;
      } catch {
        return this.externalRedirect;
      }
    },
    userInitial() {
      if (this.userInfo?.displayName) {
        return this.userInfo.displayName.charAt(0).toUpperCase();
      }
      return '?';
    }
  },
  methods: {
    async handleRegister() {
      this.resetMessages();
      this.loading = true;
      try {
        const response = await fetch(`${this.apiUrl}/register`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(this.registerForm)
        });
        
        const data = await response.json();
        
        if (!response.ok) {
           throw new Error(data.message || 'Registration failed');
        }

        this.successMsg = 'Registration successful! You can now login.';
        this.mode = 'login';
        this.loginForm.email = this.registerForm.email;
      } catch (err) {
        this.error = err.message;
      } finally {
        this.loading = false;
      }
    },
    async handleLogin() {
      this.resetMessages();
      this.loading = true;
      try {
        const response = await fetch(`${this.apiUrl}/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(this.loginForm)
        });
        
        const data = await response.json();
        
        if (!response.ok) {
           throw new Error(data.message || 'Login failed');
        }

        this.authData = data;
        this.authenticated = true;
        if (this.externalRedirect) {
          this.redirectToExternal(data);
          return;
        }
        await this.fetchUserInfo();
      } catch (err) {
        this.error = err.message;
      } finally {
        this.loading = false;
      }
    },
    logout() {
      this.authenticated = false;
      this.authData = null;
      this.userInfo = null;
      this.resetMessages();
      // Remove query parameters if any (from github callback)
      window.history.replaceState({}, document.title, window.location.pathname);
    },
    resetMessages() {
      this.error = null;
      this.successMsg = null;
    },
    providerIcon(name) {
      const icons = { Github: '\u2B24', Google: '\u2B24' };
      return icons[name] || '\u2B24';
    },
    handleGithubLogin() {
      const redirectUrl = this.externalRedirect || (window.location.origin + window.location.pathname);
      window.location.href = `${this.apiUrl}/github/login?redirectUrl=${encodeURIComponent(redirectUrl)}`;
    },
    handleGithubBind() {
      const redirectUrl = window.location.origin + window.location.pathname;
      const token = this.authData.accessToken;
      window.location.href = `${this.apiUrl}/github/login?redirectUrl=${encodeURIComponent(redirectUrl)}&token=${encodeURIComponent(token)}`;
    },
    handleGoogleLogin() {
      const redirectUrl = this.externalRedirect || (window.location.origin + window.location.pathname);
      window.location.href = `${this.apiUrl}/google/login?redirectUrl=${encodeURIComponent(redirectUrl)}`;
    },
    handleGoogleBind() {
      const redirectUrl = window.location.origin + window.location.pathname;
      const token = this.authData.accessToken;
      window.location.href = `${this.apiUrl}/google/login?redirectUrl=${encodeURIComponent(redirectUrl)}&token=${encodeURIComponent(token)}`;
    },
    async handleAddPassword() {
      this.resetMessages();
      this.loading = true;
      try {
        const response = await fetch(`${this.apiUrl}/add-password`, {
          method: 'POST',
          headers: { 
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${this.authData.accessToken}`
          },
          body: JSON.stringify(this.addPasswordForm)
        });
        
        const data = await response.json();
        
        if (!response.ok) {
           throw new Error(data.message || 'Failed to add password');
        }

        this.successMsg = 'Password successfully added/updated!';
        this.addPasswordForm.password = '';
        await this.fetchUserInfo();
      } catch (err) {
        this.error = err.message;
      } finally {
        this.loading = false;
      }
    },
    async fetchUserInfo() {
      try {
        const response = await fetch(`${this.apiUrl}/me`, {
          headers: { 'Authorization': `Bearer ${this.authData.accessToken}` }
        });
        if (response.ok) {
          this.userInfo = await response.json();
        }
      } catch {
        // ignore fetch error
      }
    },
    redirectToExternal(authData) {
      const url = new URL(this.externalRedirect);
      url.searchParams.set('token', authData.accessToken);
      url.searchParams.set('userId', authData.userId.toString());
      window.location.href = url.toString();
    }
  },
  async mounted() {
    const urlParams = new URLSearchParams(window.location.search);
    
    // Check for external redirect mode (?redirect=https://blog.example.com/callback)
    const redirect = urlParams.get('redirect');
    if (redirect) {
      this.externalRedirect = redirect;
    }

    // Check for OAuth callback with one-time authorization code
    const authCode = urlParams.get('code');
    const error = urlParams.get('error');
    
    if (authCode) {
      // Clean up URL immediately to prevent code reuse/leaking
      window.history.replaceState({}, document.title, window.location.pathname);
      try {
        const response = await fetch(`${this.apiUrl}/exchange`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code: authCode })
        });
        const data = await response.json();
        if (!response.ok) {
          throw new Error(data.message || 'Code exchange failed');
        }
        this.authData = data;
        this.authenticated = true;
        this.successMsg = 'OAuth login successful!';
        await this.fetchUserInfo();
      } catch (err) {
        this.error = err.message;
      }
    } else if (error) {
      this.error = `OAuth login failed: ${error}`;
      window.history.replaceState({}, document.title, window.location.pathname);
    }
  }
}
</script>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }

.page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f0f1f3;
  padding: 20px;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.auth-card {
  width: 100%;
  max-width: 420px;
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

/* Header */
.header {
  text-align: center;
  padding: 32px 32px 0;
}

.header h1 {
  font-size: 22px;
  font-weight: 700;
  color: #1a1a2e;
  margin-bottom: 4px;
}

.subtitle {
  font-size: 13px;
  color: #888;
  margin-top: 4px;
}

.subtitle strong {
  color: #333;
}

/* Content */
.auth-content {
  padding: 24px 32px 32px;
}

/* Tabs */
.tabs {
  display: flex;
  gap: 0;
  margin-bottom: 24px;
  background: #f5f5f7;
  border-radius: 10px;
  padding: 3px;
}

.tabs button {
  flex: 1;
  padding: 10px;
  border: none;
  background: transparent;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  color: #888;
  cursor: pointer;
  transition: all 0.2s;
}

.tabs button.active {
  background: #fff;
  color: #1a1a2e;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  font-weight: 600;
}

/* Form */
.form-group {
  margin-bottom: 14px;
}

.input {
  width: 100%;
  padding: 12px 14px;
  border: 1.5px solid #e0e0e0;
  border-radius: 10px;
  font-size: 14px;
  transition: border-color 0.2s;
  outline: none;
  background: #fafafa;
}

.input:focus {
  border-color: #333;
  background: #fff;
}

/* Buttons */
.btn {
  width: 100%;
  padding: 12px;
  border: none;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-primary {
  background: #1a1a2e;
  color: #fff;
}

.btn-primary:hover:not(:disabled) {
  background: #2d2d44;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  transform: translateY(-1px);
}

.btn-secondary {
  background: #1a1a2e;
  color: #fff;
  width: auto;
  padding: 10px 20px;
  white-space: nowrap;
  flex-shrink: 0;
}

.btn-danger {
  background: transparent;
  color: #dc3545;
  border: 1.5px solid #dc3545;
  margin-top: 16px;
}

.btn-danger:hover {
  background: #dc3545;
  color: #fff;
}

.btn-github {
  background: #24292e;
  color: #fff;
}

.btn-github:hover:not(:disabled) {
  background: #1b1f23;
}

.btn-google {
  background: #fff;
  color: #333;
  border: 1.5px solid #e0e0e0;
}

.btn-google:hover:not(:disabled) {
  background: #f8f8f8;
  border-color: #ccc;
}

.icon {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
}

/* OAuth section */
.oauth-buttons {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.oauth-divider {
  display: flex;
  align-items: center;
  margin: 20px 0;
  color: #aaa;
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 1px;
}

.oauth-divider::before,
.oauth-divider::after {
  content: "";
  flex: 1;
  height: 1px;
  background: #e8e8e8;
}

.oauth-divider span {
  padding: 0 14px;
}

/* Messages */
.message {
  margin-top: 16px;
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 13px;
  text-align: center;
}

.message.error {
  background: #fff0f0;
  color: #dc3545;
  border: 1px solid #ffcdd2;
}

.message.success {
  background: #f0fff4;
  color: #28a745;
  border: 1px solid #c8e6c9;
}

/* Profile */
.profile-section {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 24px;
  padding-bottom: 20px;
  border-bottom: 1px solid #f0f0f0;
}

.avatar {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: #1a1a2e;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  font-weight: 700;
  flex-shrink: 0;
}

.profile-info h2 {
  font-size: 18px;
  font-weight: 700;
  color: #1a1a2e;
}

.user-id {
  font-size: 11px;
  color: #aaa;
  font-family: 'SF Mono', Monaco, 'Cascadia Code', monospace;
  word-break: break-all;
}

/* Details */
.details-section {
  margin-bottom: 24px;
}

.detail-group {
  margin-bottom: 16px;
}

.detail-label {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #999;
  margin-bottom: 6px;
}

.detail-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 0;
  font-size: 14px;
  color: #333;
}

.badge-group {
  display: flex;
  gap: 4px;
}

.badge {
  padding: 2px 8px;
  border-radius: 20px;
  font-size: 10px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.badge-primary {
  background: #eef0f2;
  color: #555;
}

.badge-success {
  background: #e8f5e9;
  color: #4caf50;
}

.provider-list {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.provider-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: #f5f5f7;
  border-radius: 20px;
  font-size: 13px;
  font-weight: 500;
  color: #333;
}

.provider-icon {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  font-size: 0;
}

.provider-icon.github { background: #24292e; }
.provider-icon.google { background: #4285f4; }

.status-set { color: #4caf50; font-weight: 500; }
.status-unset { color: #999; }

.muted {
  color: #aaa;
  font-size: 13px;
}

/* Settings */
.settings-section {
  border-top: 1px solid #f0f0f0;
  padding-top: 20px;
  margin-bottom: 8px;
}

.settings-section h3 {
  font-size: 15px;
  font-weight: 600;
  color: #1a1a2e;
  margin-bottom: 16px;
}

.input-row {
  display: flex;
  gap: 10px;
  margin-bottom: 16px;
}

.input-row .input {
  flex: 1;
}
</style>