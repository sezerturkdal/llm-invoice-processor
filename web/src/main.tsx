import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { currentUserKey } from './api/auth'
import { ApiError } from './api/client'
import { Layout } from './components/Layout'
import { RequireAuth, RequireRole } from './components/RequireAuth'
import './index.css'
import { InvoiceDetailPage } from './pages/InvoiceDetailPage'
import { InvoiceListPage } from './pages/InvoiceListPage'
import { LoginPage } from './pages/LoginPage'
import { SettingsPage } from './pages/SettingsPage'
import { UsagePage } from './pages/UsagePage'
import { UsersPage } from './pages/UsersPage'

// Any 401 means the session is gone (expired, signed out elsewhere, account disabled):
// forget the user, and RequireAuth sends them to the login page.
const onError = (error: Error) => {
  if (error instanceof ApiError && error.status === 401) {
    queryClient.setQueryData(currentUserKey, null)
  }
}

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError }),
  mutationCache: new MutationCache({ onError }),
  defaultOptions: {
    queries: {
      // Don't hammer the API with retries on 4xx; one retry covers a brief network blip.
      retry: (failureCount, error) => !(error instanceof ApiError && error.status < 500) && failureCount < 1,
      refetchOnWindowFocus: true,
    },
  },
})

const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: (
      <RequireAuth>
        <Layout />
      </RequireAuth>
    ),
    children: [
      { path: '/', element: <InvoiceListPage /> },
      { path: '/invoices/:id', element: <InvoiceDetailPage /> },
      {
        element: <RequireRole role="Admin" />,
        children: [
          { path: '/usage', element: <UsagePage /> },
          { path: '/users', element: <UsersPage /> },
          { path: '/settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
