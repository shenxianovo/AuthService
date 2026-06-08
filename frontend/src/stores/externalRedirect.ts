import { ref } from 'vue'

const externalRedirect = ref<string | null>(null)

export function useExternalRedirect() {
  function init() {
    const params = new URLSearchParams(window.location.search)
    const redirect = params.get('redirect')
    if (redirect) {
      externalRedirect.value = redirect
      sessionStorage.setItem('externalRedirect', redirect)
    } else {
      const stored = sessionStorage.getItem('externalRedirect')
      if (stored) externalRedirect.value = stored
    }
  }

  function clear() {
    externalRedirect.value = null
    sessionStorage.removeItem('externalRedirect')
  }

  return { externalRedirect, init, clear }
}
