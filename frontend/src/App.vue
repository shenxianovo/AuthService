<template>
  <div class="auth-container">
    <h2>AuthService Demo</h2>
    
    <div v-if="authenticated">
      <div class="card success">
        <h3>Authenticated Successfully!</h3>
        <p><strong>UserId:</strong> {{ authData.userId }}</p>
        <p><strong>Access Token:</strong></p>
        <textarea readonly :value="authData.accessToken"></textarea>
      </div>
      <button @click="logout" class="btn danger">Logout</button>
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
      registerForm: {
        displayName: '',
        email: '',
        password: ''
      },
      loginForm: {
        email: '',
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
      } catch (err) {
        this.error = err.message;
      } finally {
        this.loading = false;
      }
    },
    logout() {
      this.authenticated = false;
      this.authData = null;
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
      this.successMsg = 'GitHub login successful!';
      // Clean up URL parameters
      window.history.replaceState({}, document.title, window.location.pathname);
    } else if (error) {
      this.error = `GitHub login failed: ${error}`;
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

textarea {
  width: 100%;
  height: 80px;
  margin-top: 5px;
  box-sizing: border-box;
  resize: vertical;
  background: #eee;
}
</style>