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

    // Define allowed roles based on the route
    let allowedRoles: string[] = []

    if (to.path === '/admin/settings') {
      // Only Super Admin can access settings
      allowedRoles = ['Super Admin']
    } else if (to.path === '/admin/lists') {
      // Lists accessible by Super Admin, Admin - Lists, and Admin (Read-only)
      allowedRoles = ['Super Admin', 'Admin - Lists', 'Admin (Read-only)']
    } else if (to.path.startsWith('/admin')) {
      // Other admin pages accessible by Super Admin and Admin (Read-only)
      allowedRoles = ['Super Admin', 'Admin (Read-only)']
    }

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
