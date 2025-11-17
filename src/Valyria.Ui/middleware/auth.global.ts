export default defineNuxtRouteMiddleware((to) => {
  const authStore = useAuthStore()

  // Public routes that don't require authentication
  const publicRoutes = ['/']

  // Check if the route requires authentication
  const requiresAuth = !publicRoutes.includes(to.path)

  if (requiresAuth && !authStore.isAuthenticated) {
    // Redirect to login page if not authenticated
    return navigateTo('/?auth=failed')
  }

  // Check for admin routes
  const adminRoutes = ['/admin']
  const isAdminRoute = adminRoutes.some((route) => to.path.startsWith(route))

  if (isAdminRoute && authStore.user) {
    const userRoles = authStore.user.roles.map((r) => r.name)
    const allowedRoles = ['Admin', 'Admin - Lists']

    // For lists page, both Admin and Admin - Lists are allowed
    if (to.path === '/admin/lists') {
      if (!userRoles.some((role) => allowedRoles.includes(role))) {
        return navigateTo('/')
      }
    } else {
      // For other admin pages, only Admin is allowed
      if (!userRoles.includes('Admin')) {
        return navigateTo('/')
      }
    }
  }
})
