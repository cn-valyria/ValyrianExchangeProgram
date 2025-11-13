# Valyrian Exchange Program - UI

A modern Nuxt 3 application for managing the Valyrian Exchange Program, built with Vue 3, Pinia, Nuxt UI, and Tailwind CSS.

## Tech Stack

- **Framework**: Nuxt 3.15.4
- **UI Library**: Nuxt UI 3.1.0 (Tailwind CSS + Headless UI)
- **State Management**: Pinia 2.3.0
- **Language**: TypeScript 5.7.2
- **Build Tool**: Vite 6.0.5
- **Testing**: Vitest 2.1.8

## Design System

This application uses **Nuxt UI**, a modern component library built on:
- **Tailwind CSS** for utility-first styling
- **Headless UI** for accessible components
- **Heroicons** for beautiful icons
- Full dark mode support
- Responsive design out of the box

## Getting Started

### Install Dependencies
```bash
npm install --legacy-peer-deps
```

### Environment Variables
Create a `.env` file:
```env
NUXT_PUBLIC_API_URL=http://localhost:5000
```

### Run Development Server
```bash
npm run dev
```

The app will be available at `http://localhost:3000`

## Available Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run generate` - Generate static site
- `npm run preview` - Preview production build
- `npm run lint` - Run linting
- `npm run test` - Run tests

## Project Status

### ✅ Completed
- Nuxt 2 → Nuxt 3 migration
- Vuex → Pinia migration
- Buefy/PrimeVue → Nuxt UI redesign
- Modern landing page with gradient hero
- All core components redesigned
- All admin dialog components redesigned
- Dashboard components redesigned
- Authentication system with custom middleware

### 🔄 In Progress
- 5 admin pages need Bulma → Tailwind conversion
- See `REDESIGN_STATUS.md` for detailed status

## Documentation

- **Redesign Status**: [REDESIGN_STATUS.md](./REDESIGN_STATUS.md) - Current UI redesign progress
- **Migration Summary**: [MIGRATION_SUMMARY.md](./MIGRATION_SUMMARY.md) - Nuxt 2→3 migration details
- **Nuxt UI Docs**: https://ui.nuxt.com

## Key Features

- **Modern UI**: Beautiful, responsive design with Nuxt UI
- **Dark Mode**: Full dark mode support throughout the app
- **Authentication**: Custom JWT-based auth with token refresh
- **Role-Based Access**: Admin and user roles with permissions
- **Account Management**: CRUD operations for game accounts
- **Transaction Management**: Aid-based and manual transactions
- **Dashboard**: Real-time statistics and data visualization

## Quick Start Guide

1. **Login**: Use Nation ID or Ruler Name + Unique Code
2. **View Account**: See your balance, aid list, and transaction history
3. **Admin Access**: Admin users can manage accounts, transactions, and lists

## Contributing

See component examples in `REDESIGN_STATUS.md` for Nuxt UI patterns.

## License

Proprietary - Valyrian Exchange Program
