import { createRouter, createWebHistory } from 'vue-router'
import { authStore } from '@/stores/auth'

import AuthLayout from '@/layouts/AuthLayout.vue'
import DashboardLayout from '@/layouts/DashboardLayout.vue'

import LoginPage from '@/pages/LoginPage.vue'
import RegisterPage from '@/pages/RegisterPage.vue'
import VerifyEmailPage from '@/pages/VerifyEmailPage.vue'
import OAuthCallbackPage from '@/pages/OAuthCallbackPage.vue'
import ForgotPasswordPage from '@/pages/ForgotPasswordPage.vue'
import ResetPasswordPage from '@/pages/ResetPasswordPage.vue'

import ProfilePage from '@/pages/dashboard/ProfilePage.vue'
import EmailsPage from '@/pages/dashboard/EmailsPage.vue'
import ProvidersPage from '@/pages/dashboard/ProvidersPage.vue'
import ApiKeysPage from '@/pages/dashboard/ApiKeysPage.vue'
import SecurityPage from '@/pages/dashboard/SecurityPage.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      redirect: '/dashboard',
    },
    {
      path: '/callback',
      component: OAuthCallbackPage,
    },
    {
      path: '/',
      component: AuthLayout,
      children: [
        { path: 'login', name: 'login', component: LoginPage },
        { path: 'register', name: 'register', component: RegisterPage },
        { path: 'verify-email', name: 'verify-email', component: VerifyEmailPage },
        { path: 'forgot-password', name: 'forgot-password', component: ForgotPasswordPage },
        { path: 'reset-password', name: 'reset-password', component: ResetPasswordPage },
      ],
    },
    {
      path: '/dashboard',
      component: DashboardLayout,
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: '/dashboard/profile' },
        { path: 'profile', name: 'profile', component: ProfilePage },
        { path: 'emails', name: 'emails', component: EmailsPage },
        { path: 'providers', name: 'providers', component: ProvidersPage },
        { path: 'api-keys', name: 'api-keys', component: ApiKeysPage },
        { path: 'security', name: 'security', component: SecurityPage },
      ],
    },
  ],
})

router.beforeEach((to) => {
  const isAuthenticated = authStore.isAuthenticated.value

  if (to.meta.requiresAuth && !isAuthenticated) {
    return { name: 'login', query: to.query }
  }

  // Redirect authenticated users away from login/register
  if ((to.name === 'login' || to.name === 'register') && isAuthenticated) {
    return { name: 'profile' }
  }
})

export default router
