<template>
  <div class="auth-container">
    <h2>AuthService Demo</h2>
    
    <div v-if="authenticated">
      <div class="card success">
        <h3>{{ userInfo ? userInfo.displayName : 'Authenticated' }}</h3>
        <p><strong>UserId:</strong> {{ authData.userId }}</p>
        
        <div v-if="userInfo">
          <p><strong>Emails:</strong></p>
          <ul class="info-list">
            <li v-for="email in userInfo.emails" :key="email.email">
              {{ email.email }}
              <span v-if="email.isPrimary" class="badge primary-badge">Primary</span>
              <span v-if="email.isVerified" class="badge verified-badge">Verified</span>
            </li>
          </ul>
          
          <p><strong>Linked Providers:</strong></p>
          <ul class="info-list" v-if="userInfo.providers.length">
            <li v-for="p in userInfo.providers" :key="p.provider">
              {{ p.provider }}
            </li>
          </ul>
          <p v-else class="muted">No third-party providers linked.</p>
          
          <p><strong>Password:</strong> {{ userInfo.hasPassword ? 'Set' : 'Not set' }}</p>
        </div>
      </div>
      
      <div class="card form-container">
        <h3>Account Settings</h3>
        
        <div class="form-group">
          <label>Add/Set Password:</label>
          <div style="display: flex; gap: 10px;">
            <input type="password" v-model="addPasswordForm.password" placeholder="New Password" />
            <button class="btn secondary" @click="handleAddPassword" :disabled="loading || !addPasswordForm.password">
              Save Password
            </button>
          </div>
        </div>

        <div class="divider">or</div>
        
        <button type="button" class="btn github-btn" @click="handleGithubBind" :disabled="loading">
          Bind GitHub Account
        </button>
        <button type="button" class="btn google-btn" @click="handleGoogleBind" :disabled="loading">
          Bind Google Account
        </button>
      </div>

      <button @click="logout" class="btn danger" style="margin-top: 20px;">Logout</button>
    </div>

    <div v-else>
      <div class="tabs">
        <button :class="{ active: mode === 'login' }" @click="mode = 'login'">Login</button>
        <button :class="{ active: mode === 'register' }" @click="mode = 'register'">Register</button>
      </div>

      <div class="card form-container">
        <!-- Register Form -->
        <form v-if="mode === 'register'" @submit.prevent="handleRegister">
          <div class="form-group">
            <label>Display Name:</label>
            <input type="text" v-model="registerForm.displayName" required />
          </div>
          <div class="form-group">
            <label>Email:</label>
            <input type="email" v-model="registerForm.email" required />
          </div>
          <div class="form-group">
            <label>Password:</label>
            <input type="password" v-model="registerForm.password" required />
          </div>
          <button type="submit" class="btn primary" :disabled="loading">
            {{ loading ? 'Registering...' : 'Register' }}
          </button>
        </form>

        <!-- Login Form -->
        <form v-if="mode === 'login'" @submit.prevent="handleLogin">
          <div class="form-group">
            <label>Email:</label>
            <input type="email" v-model="loginForm.email" required />
          </div>
          <div class="form-group">
            <label>Password:</label>
            <input type="password" v-model="loginForm.password" required />
          </div>
          <button type="submit" class="btn primary" :disabled="loading">
            {{ loading ? 'Loging in...' : 'Login' }}
          </button>
          
          <div class="divider">or</div>
          
          <button type="button" class="btn github-btn" @click="handleGithubLogin" :disabled="loading">
            Login with GitHub
          </button>
          <button type="button" class="btn google-btn" @click="handleGoogleLogin" :disabled="loading">
            Login with Google
          </button>
        </form>
      </div>

      <div v-if="error" class="error-msg">{{ error }}</div>
      <div v-if="successMsg" class="success-msg">{{ successMsg }}</div>
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
      // Read the API URL from Vite environment variables (.env files)
      apiUrl: import.meta.env.VITE_API_URL || '/api/v1/auth'
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
    handleGithubLogin() {
      const redirectUrl = window.location.origin + window.location.pathname;
      window.location.href = `${this.apiUrl}/github/login?redirectUrl=${encodeURIComponent(redirectUrl)}`;
    },
    handleGithubBind() {
      const redirectUrl = window.location.origin + window.location.pathname;
      const token = this.authData.accessToken;
      window.location.href = `${this.apiUrl}/github/login?redirectUrl=${encodeURIComponent(redirectUrl)}&token=${encodeURIComponent(token)}`;
    },
    handleGoogleLogin() {
      const redirectUrl = window.location.origin + window.location.pathname;
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
    }
  },
  async mounted() {
    // Check for GitHub callback token from the backend redirect
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    const userId = urlParams.get('userId');
    const error = urlParams.get('error');
    
    if (token && userId) {
      this.authData = {
        accessToken: token,
        userId: userId,
      };
      this.authenticated = true;
      this.successMsg = 'OAuth login successful!';
      await this.fetchUserInfo();
      // Clean up URL parameters
      window.history.replaceState({}, document.title, window.location.pathname);
    } else if (error) {
      this.error = `OAuth login failed: ${error}`;
      window.history.replaceState({}, document.title, window.location.pathname);
    }
  }
}
</script>

<style>
.auth-container {
  width: 400px;
  background: white;
  padding: 30px;
  border-radius: 8px;
  box-shadow: 0 4px 10px rgba(0,0,0,0.1);
}

h2 {
  text-align: center;
  margin-top: 0;
  color: #333;
}

.tabs {
  display: flex;
  margin-bottom: 20px;
}

.tabs button {
  flex: 1;
  padding: 10px;
  cursor: pointer;
  background: #f0f0f0;
  border: none;
  border-bottom: 2px solid transparent;
  font-size: 16px;
  transition: all 0.3s;
}

.tabs button.active {
  background: white;
  border-bottom: 2px solid #007bff;
  font-weight: bold;
  color: #007bff;
}

.form-group {
  margin-bottom: 15px;
}

.form-group label {
  display: block;
  margin-bottom: 5px;
  color: #555;
  font-size: 14px;
}

.form-group input {
  width: 100%;
  padding: 10px;
  border: 1px solid #ccc;
  border-radius: 4px;
  box-sizing: border-box;
}

.btn {
  width: 100%;
  padding: 10px;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 16px;
}

.btn.primary { background: #007bff; }
.btn.primary:disabled { background: #6c757d; cursor: not-allowed; }
.btn.danger { background: #dc3545; }
.btn.github-btn { background: #24292e; color: white; margin-top: 10px; }
.btn.github-btn:hover { background: #1b1f23; }
.btn.google-btn { background: #4285f4; color: white; margin-top: 10px; }
.btn.google-btn:hover { background: #3367d6; }

.divider {
  text-align: center;
  margin: 15px 0;
  color: #888;
  position: relative;
}
.divider::before, .divider::after {
  content: "";
  position: absolute;
  top: 50%;
  width: 40%;
  height: 1px;
  background-color: #ddd;
}
.divider::before { left: 0; }
.divider::after { right: 0; }

.error-msg {
  color: #dc3545;
  margin-top: 15px;
  text-align: center;
  font-size: 14px;
}

.success-msg {
  color: #28a745;
  margin-top: 15px;
  text-align: center;
  font-size: 14px;
}

.card.success {
  background: #d4edda;
  padding: 15px;
  border-radius: 4px;
  border: 1px solid #c3e6cb;
  margin-bottom: 20px;
}

.card.success p { margin: 5px 0; }

.info-list {
  list-style: none;
  padding: 0;
  margin: 5px 0 10px;
}

.info-list li {
  padding: 4px 0;
  font-size: 14px;
}

.badge {
  display: inline-block;
  padding: 1px 6px;
  border-radius: 3px;
  font-size: 11px;
  margin-left: 6px;
}

.primary-badge { background: #007bff; color: white; }
.verified-badge { background: #28a745; color: white; }
.muted { color: #888; font-size: 13px; }

textarea {
  width: 100%;
  height: 80px;
  margin-top: 5px;
  box-sizing: border-box;
  resize: vertical;
  background: #eee;
}
</style>