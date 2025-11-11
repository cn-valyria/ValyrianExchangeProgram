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
    const userRole = authStore.user.role
    const allowedRoles = ['Admin', 'Admin - Lists']

    // For lists page, both Admin and Admin - Lists are allowed
    if (to.path === '/admin/lists') {
      if (!allowedRoles.includes(userRole)) {
        return navigateTo('/')
      }
    } else {
      // For other admin pages, only Admin is allowed
      if (userRole !== 'Admin') {
        return navigateTo('/')
      }
    }
  }
})
