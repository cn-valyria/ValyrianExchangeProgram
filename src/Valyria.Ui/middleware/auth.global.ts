export default defineNuxtRouteMiddleware((to) => {
  const authStore = useAuthStore()
  const toast = useToast()

  // Public routes that don't require authentication
  const publicRoutes = ['/', '/login', '/how-it-works']

  // Check if the route requires authentication
  const requiresAuth = !publicRoutes.includes(to.path)

  if (requiresAuth && !authStore.isAuthenticated) {
    // Redirect to login page if not authenticated
    return navigateTo('/login?auth=failed')
  }

  // Check for admin routes
  const adminRoutes = ['/admin']
  const isAdminRoute = adminRoutes.some((route) => to.path.startsWith(route))

  if (isAdminRoute && authStore.user) {
    const userRoles = authStore.user.roles.map((r) => r.name)
    const allowedRoles = to.path === '/admin/lists' 
      ? ['Super Admin', 'Admin - Lists']
      : ['Super Admin']

    if (!userRoles.some((role) => allowedRoles.includes(role))) {
      console.warn('Tried to access an admin page without the admin role.')
      toast.add({
        color: 'error',
        title: 'Access Denied',
        description: 'You do not have permission to access this page.',
        duration: 5000
      })
      return navigateTo('/')
    }
  }
})
